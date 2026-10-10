using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace EntityPrototypeParser;

class Program
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string PrototypesPath = Path.Combine(RepositoryRoot, "Resources", "Prototypes");
    private static string LocaleOutputPath = Path.Combine(RepositoryRoot, "Resources", "Locale", "pt-BR"); // Default to pt-BR for backward compatibility
    private static string TargetLocale = "pt-BR"; // Default locale

    private static int _totalPrototypes;
    private static int _totalFiles;
    private static int _abstractPrototypesSkipped;
    private static int _abstractPrototypesIncluded;
    private static int _filesWithoutDescription;
    private static int _localizationKeyRefsSkipped;
    private static int _emptyFilesSkipped;
    private static int _backedUpFiles;
    private static int _preservedTranslations;

    // Dictionaries for prototype tracking and inheritance
    private static readonly Dictionary<string, PrototypeData> _abstractPrototypes = new();
    private static readonly Dictionary<string, YamlMappingNode> _allPrototypeNodes = new();
    private static List<PrototypeTypeConfig> _typeConfigs = LoadTypeConfigs();

    // Blacklist of overly-generic abstract prototypes that should never provide inherited values
    private static readonly HashSet<string> _blacklistedGenericPrototypes = new()
    {
        "BaseItem"  // Has name="item" which is too generic for inheritance
    };

    // Casing conflict mapping: lowercase -> capitalized
    private static readonly Dictionary<string, string> _casingNormalizationMap = new();

    static int Main(string[] args)
    {
        // Parse command-line arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--locale":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        TargetLocale = args[++i];
                        LocaleOutputPath = Path.Combine(RepositoryRoot, "Resources", "Locale", TargetLocale);
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --locale requires a value (e.g., --locale en-US)");
                        return 1;
                    }
                    break;
                case "--en-us":
                    TargetLocale = "en-US";
                    LocaleOutputPath = Path.Combine(RepositoryRoot, "Resources", "Locale", "en-US");
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
                default:
                    Console.Error.WriteLine($"Error: Unknown argument: {args[i]}");
                    PrintHelp();
                    return 1;
            }
        }

        Console.WriteLine($"Prototype Parser for {TargetLocale} Locale");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        if (!Directory.Exists(PrototypesPath))
        {
            Console.Error.WriteLine($"Error: Prototypes directory not found: {PrototypesPath}");
            return 1;
        }

        // Build casing normalization map
        BuildCasingNormalizationMap(LocaleOutputPath);

        // Backup existing entities.ftl files for pt-BR locale (translation preservation)
        if (TargetLocale == "pt-BR")
        {
            Console.WriteLine("Backing up existing entities.ftl files...");
            BackupExistingEntitiesFtl(LocaleOutputPath);
            Console.WriteLine();
        }

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(LocaleOutputPath);

        // Parse all prototype files and group prototypes by fork folder
        var prototypesByTarget = new Dictionary<string, List<PrototypeData>>();
        var forkFolders = new HashSet<string>();

        // Pass 1: Collect all prototypes (without resolving inheritance)
        Console.WriteLine("Pass 1: Collecting all prototypes...");
        CollectAllPrototypes(PrototypesPath, prototypesByTarget, forkFolders);
        Console.WriteLine($"  Collected {_allPrototypeNodes.Count} prototypes");

        // Pass 2: Resolve inheritance for all prototypes
        Console.WriteLine();
        Console.WriteLine("Pass 2: Resolving inheritance...");
        ResolveAllInheritance(prototypesByTarget);
        Console.WriteLine("  Inheritance resolution complete");

        // Pass 3: Generate fluent files
        Console.WriteLine();
        Console.WriteLine("Generating fluent files...");
        Console.WriteLine();

        // All prototype types share the "ent-" localization namespace, and the same id
        // commonly exists as several types (entity + stack + material, etc.). Claim every
        // id globally so each key is emitted only once; entity prototypes win because the
        // engine resolves entity names as ent-{id}.
        var claimedKeys = new HashSet<string>(StringComparer.Ordinal);
        var claimedByTarget = new Dictionary<string, List<PrototypeData>>();

        var allPrototypes = prototypesByTarget
            .SelectMany(kv => kv.Value.Select(p => (TargetPath: kv.Key, Prototype: p)))
            // Nameless prototypes produce no output, so they must not claim the id
            .Where(e => !string.IsNullOrEmpty(e.Prototype.Name) && !IsLocalizationKeyReference(e.Prototype.Name))
            .OrderBy(e => e.Prototype.Type.Equals("entity", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(e => e.TargetPath, StringComparer.Ordinal)
            .ThenBy(e => e.Prototype.SourceFile, StringComparer.Ordinal)
            .ThenBy(e => e.Prototype.Id, StringComparer.Ordinal);

        foreach (var entry in allPrototypes)
        {
            if (!claimedKeys.Add($"ent-{entry.Prototype.Id}"))
                continue;

            if (!claimedByTarget.TryGetValue(entry.TargetPath, out var list))
                claimedByTarget[entry.TargetPath] = list = new List<PrototypeData>();

            list.Add(entry.Prototype);
        }

        // Files whose only entries lost the claim must not linger on disk
        foreach (var targetPath in prototypesByTarget.Keys)
        {
            if (!claimedByTarget.ContainsKey(targetPath) && File.Exists(targetPath))
            {
                File.Delete(targetPath);
                Console.WriteLine($"  Deleted: {Path.GetRelativePath(LocaleOutputPath, targetPath)} (all entries duplicates)");
            }
        }

        foreach (var (targetPath, prototypes) in claimedByTarget.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            GenerateFluentFile(targetPath, prototypes);
        }

        // Preserve existing translations for pt-BR locale
        if (TargetLocale == "pt-BR")
        {
            Console.WriteLine();
            Console.WriteLine("Preserving existing translations...");
            PreserveExistingTranslations(LocaleOutputPath);
        }

        // Print summary
        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.WriteLine($"  Target locale: {TargetLocale}");
        Console.WriteLine($"  Total prototype files processed: {_totalFiles}");
        Console.WriteLine($"  Total prototypes found: {_totalPrototypes}");
        Console.WriteLine($"  Abstract prototypes skipped (no data): {_abstractPrototypesSkipped - _abstractPrototypesIncluded}");
        Console.WriteLine($"  Abstract prototypes included (with data): {_abstractPrototypesIncluded}");
        Console.WriteLine($"  Prototypes without description: {_filesWithoutDescription}");
        Console.WriteLine($"  Localization key references skipped: {_localizationKeyRefsSkipped}");
        Console.WriteLine($"  Empty files skipped: {_emptyFilesSkipped}");
        if (TargetLocale == "pt-BR")
        {
            Console.WriteLine($"  Files backed up: {_backedUpFiles}");
            Console.WriteLine($"  Translations preserved: {_preservedTranslations}");
        }
        Console.WriteLine($"  Fluent files generated: {prototypesByTarget.Count - _emptyFilesSkipped}");
        Console.WriteLine();
        Console.WriteLine("Done!");

        return 0;
    }

    static void PrintHelp()
    {
        Console.WriteLine("Entity Prototype Parser - Generate FTL localization files from YAML prototypes");
        Console.WriteLine();
        Console.WriteLine("Usage: EntityPrototypeParser [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --locale, -l <locale>  Target locale (e.g., en-US, pt-BR, es-ES)");
        Console.WriteLine("  --en-us               Shortcut for --locale en-US");
        Console.WriteLine("  --help, -h            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  EntityPrototypeParser              # Generate pt-BR (default)");
        Console.WriteLine("  EntityPrototypeParser --en-us      # Generate en-US");
        Console.WriteLine("  EntityPrototypeParser --locale es-ES  # Generate Spanish");
    }

    static void BuildCasingNormalizationMap(string localePath)
    {
        // Build a map of lowercase folder names to their capitalized versions
        // Also check for underscore vs non-underscore conflicts

        if (!Directory.Exists(localePath))
            return;

        var allDirs = Directory.GetDirectories(localePath);
        var dirNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in allDirs)
        {
            var dirName = Path.GetFileName(dir);
            var key = dirName.TrimStart('_').ToLowerInvariant();

            if (!dirNames.ContainsKey(key))
                dirNames[key] = new List<string>();
            dirNames[key].Add(dirName);
        }

        // Build normalization map
        foreach (var (key, variants) in dirNames)
        {
            if (variants.Count == 1)
            {
                // Only one variant, no conflict
                var dirName = variants[0];
                var normalized = NormalizeFolderName(dirName);
                if (normalized != dirName)
                {
                    _casingNormalizationMap[dirName] = normalized;
                }
            }
            else
            {
                // Multiple variants - choose the preferred one
                var preferred = ChoosePreferredVariant(variants);
                foreach (var variant in variants)
                {
                    if (variant != preferred)
                    {
                        _casingNormalizationMap[variant] = preferred;
                    }
                }
            }
        }
    }

    static string NormalizeFolderName(string dirName)
    {
        // Normalize folder name: capitalize first letter, ensure underscore prefix
        var withoutUnderscore = dirName.TrimStart('_');

        // Capitalize first letter
        if (withoutUnderscore.Length > 0)
        {
            withoutUnderscore = char.ToUpper(withoutUnderscore[0]) + withoutUnderscore.Substring(1);
        }

        // Ensure underscore prefix
        return $"_{withoutUnderscore}";
    }

    static string ChoosePreferredVariant(List<string> variants)
    {
        // Choose the variant that starts with underscore and has capitalized first letter
        string? withUnderscore = null;
        string? withoutUnderscore = null;
        string? lowerUnderscore = null;

        foreach (var variant in variants)
        {
            if (variant.StartsWith('_'))
            {
                if (char.IsUpper(variant, 1))
                    withUnderscore = variant;  // _Capitalized
                else
                    lowerUnderscore = variant;  // _lowercase
            }
            else
            {
                withoutUnderscore = variant;  // NoUnderscore
            }
        }

        // Prefer _Capitalized, then _lowercase, then NoUnderscore
        return withUnderscore ?? lowerUnderscore ?? withoutUnderscore ?? variants[0];
    }

    static string NormalizeForkFolder(string dirName)
    {
        // Check if this folder needs normalization
        if (_casingNormalizationMap.TryGetValue(dirName, out var normalized))
        {
            return normalized;
        }
        return dirName;
    }

    static List<PrototypeTypeConfig> LoadTypeConfigs()
    {
        var configPath = Path.Combine(RepositoryRoot, "Tools", "EntityPrototypeParser", "prototype-types.json");
        if (!File.Exists(configPath))
        {
            // Return default config with just entity type
            return new List<PrototypeTypeConfig>
            {
                new() { Name = "entity", Enabled = true, Fields = new[] { "name", "description", "suffix" } }
            };
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            var config = JsonSerializer.Deserialize<ConfigRoot>(json, options);
            var types = config?.Types ?? new List<PrototypeTypeConfig>();
            Console.Error.WriteLine($"Loaded {types.Count} prototype types from {configPath}");
            return types;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to load prototype-types.json: {ex.Message}");
            Console.Error.WriteLine("Using default configuration (entity type only).");
            return new List<PrototypeTypeConfig>
            {
                new() { Name = "entity", Enabled = true, Fields = new[] { "name", "description", "suffix" } }
            };
        }
    }

    static void BackupExistingEntitiesFtl(string localePath)
    {
        var backupDir = Path.Combine(localePath, "_backup");

        // Remove old backup if exists
        if (Directory.Exists(backupDir))
        {
            Directory.Delete(backupDir, recursive: true);
        }
        Directory.CreateDirectory(backupDir);

        // Find all entities.ftl files
        foreach (var ftlFile in Directory.GetFiles(localePath, "entities.ftl", SearchOption.AllDirectories))
        {
            // Skip if already in _backup or _generated
            if (ftlFile.Contains("_backup") || ftlFile.Contains("_generated"))
                continue;

            var relativePath = Path.GetRelativePath(localePath, ftlFile);
            var backupPath = Path.Combine(backupDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(ftlFile, backupPath, true);
            _backedUpFiles++;
            Console.WriteLine($"  Backed up: {relativePath}");
        }

        if (_backedUpFiles == 0)
        {
            Console.WriteLine("  No existing entities.ftl files found to backup.");
        }
    }

    static void PreserveExistingTranslations(string localePath)
    {
        var backupDir = Path.Combine(localePath, "_backup");
        if (!Directory.Exists(backupDir))
        {
            Console.WriteLine("  No backup found, skipping translation preservation.");
            return;
        }

        var generatedDir = Path.Combine(localePath, "_generated");
        if (!Directory.Exists(generatedDir))
        {
            Console.WriteLine("  No generated files found, skipping translation preservation.");
            return;
        }

        // Step 1: Parse ALL backed up entities.ftl files into a single dictionary
        var allBackupTranslations = new Dictionary<string, string>();

        foreach (var backupFile in Directory.GetFiles(backupDir, "*.ftl", SearchOption.AllDirectories))
        {
            var fileTranslations = ParseFtlFile(backupFile);
            foreach (var (key, value) in fileTranslations)
            {
                // If key exists in multiple files, last one wins (could also prioritize by directory)
                allBackupTranslations[key] = value;
            }
            Console.WriteLine($"  Loaded {fileTranslations.Count} translations from backup file.");
        }

        if (allBackupTranslations.Count == 0)
        {
            Console.WriteLine("  No translations found in backup files.");
            return;
        }

        Console.WriteLine($"  Total unique translations to preserve: {allBackupTranslations.Count}");

        // Step 2: Apply translations to ALL generated fluent files
        foreach (var generatedFile in Directory.GetFiles(generatedDir, "*.ftl", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(generatedDir, generatedFile);

            // Parse generated file and apply preserved translations
            var newLines = File.ReadAllLines(generatedFile);
            var modifiedLines = new List<string>();
            var filePreservedCount = 0;

            foreach (var line in newLines)
            {
                var trimmed = line.TrimStart();

                // Check if this line contains a key that exists in backup
                if (TryGetFtlKey(trimmed, out var key))
                {
                    if (allBackupTranslations.TryGetValue(key, out var oldValue))
                    {
                        // Preserve the old translation value
                        modifiedLines.Add(oldValue);
                        filePreservedCount++;
                        _preservedTranslations++;
                        continue;
                    }
                }

                modifiedLines.Add(line);
            }

            // Write the preserved file
            File.WriteAllLines(generatedFile, modifiedLines);

            if (filePreservedCount > 0)
            {
                Console.WriteLine($"  Preserved {filePreservedCount} translations in: {relativePath}");
            }
        }
    }

    static Dictionary<string, string> ParseFtlFile(string filePath)
    {
        var translations = new Dictionary<string, string>();

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.TrimStart();

            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            // Try to extract key and value
            if (TryGetFtlKey(trimmed, out var key))
            {
                translations[key] = line;  // Store the full line to preserve formatting
            }
        }

        return translations;
    }

    static bool TryGetFtlKey(string line, out string key)
    {
        key = string.Empty;

        // Skip comments and empty lines
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            return false;

        // Look for key = value pattern
        var equalIndex = line.IndexOf('=');
        if (equalIndex <= 0)
            return false;

        var beforeEqual = line.Substring(0, equalIndex).TrimEnd();
        key = beforeEqual;

        return true;
    }

    static string PluralizeTypeName(string typeName)
    {
        // Convert prototype type to pluralized filename
        // e.g., "entity" -> "entities.ftl", "alert" -> "alerts.ftl", "species" -> "species.ftl"

        // Special cases
        if (typeName.Equals("entity", StringComparison.OrdinalIgnoreCase))
            return "entities.ftl";
        if (typeName.Equals("species", StringComparison.OrdinalIgnoreCase))
            return "species.ftl";

        // Default: add 's'
        return $"{typeName}s.ftl";
    }

    static void CollectAllPrototypes(
        string dirPath,
        Dictionary<string, List<PrototypeData>> prototypesByTarget,
        HashSet<string> forkFolders,
        string? currentForkFolder = null)
    {
        var dirName = Path.GetFileName(dirPath);

        // Normalize folder name to handle casing conflicts
        var normalizedDirName = NormalizeForkFolder(dirName);

        // Determine if this directory is a fork folder (starts with _)
        // If we're already in a fork folder context, don't change it
        // If we're not, and this dir starts with _, it becomes the new fork context
        string? effectiveForkFolder = currentForkFolder;
        if (currentForkFolder == null && normalizedDirName.StartsWith("_"))
        {
            effectiveForkFolder = normalizedDirName;
            forkFolders.Add(effectiveForkFolder);
        }

        // Check if this is a non-underscore version of a fork folder (merge logic)
        // Only applies if we're not already in a fork folder
        if (currentForkFolder == null && !normalizedDirName.StartsWith("_"))
        {
            var parentDir = Directory.GetParent(dirPath);
            if (parentDir != null)
            {
                var underscoreVersion = Path.Combine(parentDir.FullName, $"_{normalizedDirName}");
                if (Directory.Exists(underscoreVersion))
                {
                    // Merge into the underscore version
                    effectiveForkFolder = $"_{normalizedDirName}";
                    forkFolders.Add(effectiveForkFolder);
                }
            }
        }

        // Process all YAML files in this directory
        foreach (var yamlFile in Directory.GetFiles(dirPath, "*.yml"))
        {
            CollectYamlFile(yamlFile, prototypesByTarget, effectiveForkFolder);
        }

        // Recursively process subdirectories
        foreach (var subDir in Directory.GetDirectories(dirPath))
        {
            var subDirName = Path.GetFileName(subDir);

            // Skip subdirectory if it's a fork folder and we're already in a fork folder
            // (fork folders should be processed separately from root)
            if (currentForkFolder == null && subDirName.StartsWith("_"))
            {
                // Don't recurse into fork folders from root - they'll be processed separately
                CollectAllPrototypes(subDir, prototypesByTarget, forkFolders, subDirName);
            }
            else if (currentForkFolder != null)
            {
                // We're inside a fork folder, recurse with the same context
                CollectAllPrototypes(subDir, prototypesByTarget, forkFolders, currentForkFolder);
            }
            else
            {
                // Normal directory, recurse
                CollectAllPrototypes(subDir, prototypesByTarget, forkFolders, null);
            }
        }
    }

    static void CollectYamlFile(
        string filePath,
        Dictionary<string, List<PrototypeData>> prototypesByTarget,
        string? forkFolder)
    {
        try
        {
            var yamlStream = new YamlStream();
            using var reader = new StreamReader(filePath);
            yamlStream.Load(reader);

            var relativePath = Path.GetRelativePath(PrototypesPath, filePath);
            _totalFiles++;

            foreach (var doc in yamlStream.Documents)
            {
                if (doc.RootNode is not YamlSequenceNode sequenceNode)
                    continue;

                foreach (var node in sequenceNode)
                {
                    if (node is not YamlMappingNode mappingNode)
                        continue;

                    // Check if this is a parseable prototype
                    if (!IsParseablePrototypeNode(mappingNode))
                        continue;

                    // Extract prototype data
                    var prototype = ExtractPrototypeData(mappingNode, relativePath);
                    if (prototype == null)
                    {
                        // Still store the node for potential parent resolution
                        if (mappingNode.Children.TryGetValue(new YamlScalarNode("id"), out var idNode))
                        {
                            var id = (idNode as YamlScalarNode)?.Value?.ToString();
                            if (!string.IsNullOrEmpty(id))
                            {
                                _allPrototypeNodes[id] = mappingNode;
                            }
                        }
                        continue;
                    }

                    // Store the node for potential parent resolution
                    _allPrototypeNodes[prototype.Id] = mappingNode;

                    // Determine target file path based on prototype type
                    var typeName = prototype.Type;
                    var fileName = PluralizeTypeName(typeName); // e.g., "entity" -> "entities.ftl", "alert" -> "alerts.ftl"

                    string targetPath;
                    if (forkFolder != null)
                    {
                        targetPath = Path.Combine(LocaleOutputPath, forkFolder, "_generated", "prototypes", fileName);
                    }
                    else
                    {
                        targetPath = Path.Combine(LocaleOutputPath, "_generated", "prototypes", fileName);
                    }

                    if (!prototypesByTarget.ContainsKey(targetPath))
                    {
                        prototypesByTarget[targetPath] = new List<PrototypeData>();
                    }
                    prototypesByTarget[targetPath].Add(prototype);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing {filePath}: {ex.Message}");
        }
    }

    static bool IsParseablePrototypeNode(YamlMappingNode node)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode))
            return false;

        var typeValue = (typeNode as YamlScalarNode)?.Value;
        if (string.IsNullOrEmpty(typeValue))
            return false;

        // Check if this type is enabled in configuration
        var config = _typeConfigs.FirstOrDefault(t => t.Name == typeValue);
        return config != null && config.Enabled;
    }

    static PrototypeData? ExtractPrototypeData(YamlMappingNode node, string sourceFile)
    {
        // Get id
        if (!node.Children.TryGetValue(new YamlScalarNode("id"), out var idNode))
            return null;

        var id = (idNode as YamlScalarNode)?.Value?.ToString();
        if (string.IsNullOrEmpty(id))
            return null;

        // Get type
        if (!node.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode))
            return null;

        var type = (typeNode as YamlScalarNode)?.Value?.ToString();
        if (string.IsNullOrEmpty(type))
            return null;

        // Get type config to determine which fields to extract
        var typeConfig = _typeConfigs.FirstOrDefault(t => t.Name == type);
        if (typeConfig == null)
            return null;

        // Check if abstract
        bool isAbstract = false;
        if (node.Children.TryGetValue(new YamlScalarNode("abstract"), out var abstractNode))
        {
            if (abstractNode is YamlScalarNode absScalar && absScalar.Value == "true")
            {
                isAbstract = true;
            }
        }

        // Get name
        string? name = null;
        if (typeConfig.Fields.Contains("name") && node.Children.TryGetValue(new YamlScalarNode("name"), out var nameNode))
        {
            name = (nameNode as YamlScalarNode)?.Value?.ToString();

            // Skip if name is a localization key reference
            if (!string.IsNullOrEmpty(name) && IsLocalizationKeyReference(name))
            {
                _localizationKeyRefsSkipped++;
                return null;
            }
        }

        // Get description
        string? description = null;
        if (typeConfig.Fields.Contains("description") && node.Children.TryGetValue(new YamlScalarNode("description"), out var descNode))
        {
            description = (descNode as YamlScalarNode)?.Value?.ToString();

            // Skip if description is a localization key reference
            if (!string.IsNullOrEmpty(description) && IsLocalizationKeyReference(description))
            {
                _localizationKeyRefsSkipped++;
                return null;
            }
        }

        // Get suffix (only for entity type)
        string? suffix = null;
        if (typeConfig.Fields.Contains("suffix") && node.Children.TryGetValue(new YamlScalarNode("suffix"), out var suffixNode))
        {
            suffix = (suffixNode as YamlScalarNode)?.Value?.ToString();
        }

        var prototype = new PrototypeData
        {
            Id = id,
            Type = type,
            Name = name ?? string.Empty,
            Description = description ?? string.Empty,
            Suffix = suffix,
            SourceFile = sourceFile
        };

        if (isAbstract)
        {
            _abstractPrototypes[id] = prototype; // Store for inheritance
            _abstractPrototypesSkipped++;

            // IMPORTANT: Include abstract prototypes with localizable data
            // They need to be in the output so child prototypes can reference them
            if (!string.IsNullOrEmpty(prototype.Name) || !string.IsNullOrEmpty(prototype.Description))
            {
                prototype.IsAbstract = true;
                _abstractPrototypesIncluded++;
                _totalPrototypes++;

                if (string.IsNullOrEmpty(prototype.Description))
                {
                    _filesWithoutDescription++;
                }

                return prototype;
            }

            return null; // Skip abstract prototypes with no localizable data
        }

        _totalPrototypes++;
        if (string.IsNullOrEmpty(prototype.Description))
        {
            _filesWithoutDescription++;
        }

        return prototype;
    }

    static bool IsLocalizationKeyReference(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Localization key references are typically kebab-case (contain hyphens)
        // English text would not normally contain hyphens
        // Exception: compound words like "fire-extinguisher" are valid English
        // But patterns like "xxx-name-yyy" or "xxx-desc-yyy" are clearly localization keys

        // Check for explicit patterns
        if (value.Contains("-name-") || value.Contains("-desc-") || value.Contains("-description-"))
            return true;

        // Simple heuristic: if it's all kebab-case and contains common loc key patterns
        if (value.Contains("-") && (value.Contains("name") || value.Contains("desc")))
            return true;

        return false;
    }

    static void ResolveAllInheritance(Dictionary<string, List<PrototypeData>> prototypesByTarget)
    {
        var totalResolved = 0;
        var nameResolved = 0;
        var descResolved = 0;

        foreach (var (_, prototypes) in prototypesByTarget)
        {
            foreach (var prototype in prototypes)
            {
                // Get the prototype's node from _allPrototypeNodes
                if (!_allPrototypeNodes.TryGetValue(prototype.Id, out var node))
                    continue;

                // Resolve inheritance for name (only if empty)
                if (string.IsNullOrEmpty(prototype.Name))
                {
                    var (inheritedName, _, inheritedFrom) = ResolveInheritance(node);

                    if (!string.IsNullOrEmpty(inheritedName))
                    {
                        prototype.InheritedNameFrom = inheritedFrom;
                        nameResolved++;
                        totalResolved++;
                    }
                }

                // Resolve inheritance for description (only if empty)
                if (string.IsNullOrEmpty(prototype.Description))
                {
                    var (_, inheritedDesc, inheritedFrom) = ResolveInheritance(node);

                    if (!string.IsNullOrEmpty(inheritedDesc))
                    {
                        prototype.InheritedDescriptionFrom = inheritedFrom;
                        descResolved++;
                        totalResolved++;
                    }
                }
            }
        }

        Console.WriteLine($"  Resolved {totalResolved} inherited values ({nameResolved} names, {descResolved} descriptions)");
    }

    static (string? name, string? description, string? inheritedFrom) ResolveInheritance(
        YamlMappingNode node,
        HashSet<string>? visited = null)
    {
        if (visited == null)
            visited = new HashSet<string>();

        // Get id to prevent circular references
        if (!node.Children.TryGetValue(new YamlScalarNode("id"), out var idNode))
            return (null, null, null);

        var id = (idNode as YamlScalarNode)?.Value?.ToString();
        if (string.IsNullOrEmpty(id) || visited.Contains(id))
            return (null, null, null); // Circular reference detected

        visited.Add(id);

        // Extract parent IDs - handle both 'parent' (string) and 'parents' (array)
        var parentIds = ExtractParentIds(node);
        if (parentIds.Count == 0)
            return (null, null, null);

        // Accumulate name and description as we traverse the chain
        string? foundName = null;
        string? foundDesc = null;
        string? nameSource = null;
        string? descSource = null;

        // Breadth-first search through parent hierarchy
        // Queue stores: (parentIds to check, depth level)
        var queue = new Queue<List<string>>();
        queue.Enqueue(parentIds);
        var processedParents = new HashSet<string>(parentIds);

        while (queue.Count > 0)
        {
            var currentLayer = queue.Dequeue();

            // Check all parents in current layer
            foreach (var parentId in currentLayer)
            {
                // Skip if already visited to prevent cycles
                if (visited.Contains(parentId))
                    continue;

                visited.Add(parentId);

                // Check if parent is in _abstractPrototypes
                if (_abstractPrototypes.TryGetValue(parentId, out var parentPrototype))
                {
                    // Skip blacklisted generic prototypes but continue searching
                    if (_blacklistedGenericPrototypes.Contains(parentId))
                    {
                        continue;
                    }

                    // Found parent with data - accumulate name and description
                    if (string.IsNullOrEmpty(foundName) && !string.IsNullOrEmpty(parentPrototype.Name))
                    {
                        foundName = parentPrototype.Name;
                        nameSource = parentId;
                    }
                    if (string.IsNullOrEmpty(foundDesc) && !string.IsNullOrEmpty(parentPrototype.Description))
                    {
                        foundDesc = parentPrototype.Description;
                        descSource = parentId;
                    }
                }

                // Check if parent is in _allPrototypeNodes for further traversal OR data extraction
                if (_allPrototypeNodes.TryGetValue(parentId, out var parentNodeMapping))
                {
                    // Try to extract name/description from this non-abstract parent
                    var parentName = ExtractNameFromNode(parentNodeMapping);
                    var parentDesc = ExtractDescriptionFromNode(parentNodeMapping);

                    // Skip blacklisted prototypes
                    if (_blacklistedGenericPrototypes.Contains(parentId))
                    {
                        continue;
                    }

                    // Accumulate name and description
                    if (string.IsNullOrEmpty(foundName) && !string.IsNullOrEmpty(parentName))
                    {
                        foundName = parentName;
                        nameSource = parentId;
                    }
                    if (string.IsNullOrEmpty(foundDesc) && !string.IsNullOrEmpty(parentDesc))
                    {
                        foundDesc = parentDesc;
                        descSource = parentId;
                    }

                    // Look for grandparents to continue searching
                    var grandParentIds = ExtractParentIds(parentNodeMapping);
                    if (grandParentIds.Count > 0)
                    {
                        // Enqueue unprocessed grandparents for next layer (deduplicated)
                        var nextLayer = grandParentIds
                            .Where(gid => !processedParents.Contains(gid) && !visited.Contains(gid))
                            .Distinct()
                            .ToList();

                        if (nextLayer.Count > 0)
                        {
                            // Mark as processed before enqueueing to prevent re-queuing
                            foreach (var gid in nextLayer)
                            {
                                processedParents.Add(gid);
                            }
                            queue.Enqueue(nextLayer);
                        }
                    }
                }
            }
        }

        // Return whatever we found (may be null if nothing found)
        return (foundName, foundDesc, nameSource);
    }

    static string? ExtractNameFromNode(YamlMappingNode node)
    {
        if (node.Children.TryGetValue(new YamlScalarNode("name"), out var nameNode))
        {
            return (nameNode as YamlScalarNode)?.Value?.ToString();
        }
        return null;
    }

    static string? ExtractDescriptionFromNode(YamlMappingNode node)
    {
        if (node.Children.TryGetValue(new YamlScalarNode("description"), out var descNode))
        {
            return (descNode as YamlScalarNode)?.Value?.ToString();
        }
        return null;
    }

    static List<string> ExtractParentIds(YamlMappingNode node)
    {
        var parentIds = new List<string>();

        // Try 'parents' (array) first
        if (node.Children.TryGetValue(new YamlScalarNode("parents"), out var parentsNode))
        {
            if (parentsNode is YamlSequenceNode parentsSeq)
            {
                foreach (var parentItem in parentsSeq)
                {
                    if (parentItem is YamlScalarNode parentScalar)
                    {
                        var parentId = parentScalar.Value?.ToString();
                        if (!string.IsNullOrEmpty(parentId))
                        {
                            parentIds.Add(parentId);
                        }
                    }
                }
                return parentIds;
            }
        }

        // Try 'parent' (can be either string or sequence)
        if (node.Children.TryGetValue(new YamlScalarNode("parent"), out var parentNode))
        {
            // Handle 'parent' as a sequence (array of parents)
            if (parentNode is YamlSequenceNode parentSeq)
            {
                foreach (var parentItem in parentSeq)
                {
                    if (parentItem is YamlScalarNode parentScalar)
                    {
                        var parentId = parentScalar.Value?.ToString();
                        if (!string.IsNullOrEmpty(parentId))
                        {
                            parentIds.Add(parentId);
                        }
                    }
                }
            }
            // Handle 'parent' as a scalar (single parent)
            else if (parentNode is YamlScalarNode parentScalar)
            {
                var parentId = parentScalar.Value?.ToString();
                if (!string.IsNullOrEmpty(parentId))
                {
                    parentIds.Add(parentId);
                }
            }
        }

        return parentIds;
    }

    static void GenerateFluentFile(string targetPath, List<PrototypeData> prototypes)
    {
        // Sort prototypes by source file, then by ID
        var sortedPrototypes = prototypes
            .OrderBy(e => e.SourceFile)
            .ThenBy(e => e.Id)
            .ToList();

        // Filter out prototypes with no localizable data
        var prototypesWithData = sortedPrototypes
            .Where(e =>
                !string.IsNullOrEmpty(e.Name) &&
                !IsLocalizationKeyReference(e.Name))
            .ToList();

        if (prototypesWithData.Count == 0)
        {
            // Delete existing file if present, skip creation
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            _emptyFilesSkipped++;
            var skipPath = Path.GetRelativePath(LocaleOutputPath, targetPath);
            Console.WriteLine($"  Skipped: {skipPath} (no localizable content)");
            return;
        }

        // Create directory if needed
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(targetPath, false, new UTF8Encoding(false));

        writer.WriteLine("# ====================================================================");
        writer.WriteLine("# Prototype Localization File");
        writer.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine("# Total prototypes: " + prototypesWithData.Count);
        writer.WriteLine("# ====================================================================");
        writer.WriteLine();

        string? currentSourceFile = null;

        foreach (var prototype in prototypesWithData)
        {
            // Determine what to write for each field
            bool hasOwnName = !string.IsNullOrEmpty(prototype.Name);
            bool hasOwnDescription = !string.IsNullOrEmpty(prototype.Description);
            bool hasOwnSuffix = !string.IsNullOrEmpty(prototype.Suffix);

            // Add source file comment when it changes
            if (prototype.SourceFile != currentSourceFile)
            {
                currentSourceFile = prototype.SourceFile;
                writer.WriteLine($"# From: {currentSourceFile}");
                writer.WriteLine();
            }

            // Add comment for abstract prototypes
            if (prototype.IsAbstract)
            {
                writer.WriteLine($"# Abstract prototype (referenceable by children)");
            }

            // Determine name value - use reference if inheriting, otherwise use own name
            string nameValue;
            if (!hasOwnName && prototype.InheritedNameFrom != null)
            {
                // Name is inherited - reference parent's entry
                nameValue = $"{{ent-{prototype.InheritedNameFrom}}}";
            }
            else
            {
                // Use own name (already escaped by EscapeFluentString)
                nameValue = EscapeFluentString(prototype.Name);
            }

            // All types use "ent-" prefix
            writer.WriteLine($"ent-{prototype.Id} = {nameValue}");

            // Only write suffix if prototype has its own suffix value
            // Never inherit suffix from parents
            if (hasOwnSuffix)
            {
                writer.WriteLine($"    .suffix = {EscapeFluentString(prototype.Suffix!)}");
            }

            // Handle description inheritance
            if (hasOwnDescription)
            {
                if (prototype.InheritedDescriptionFrom != null)
                {
                    // Reference parent's .desc attribute
                    writer.WriteLine($"    .desc = {{ent-{prototype.InheritedDescriptionFrom}.desc}}");
                }
                else
                {
                    // Use own description
                    writer.WriteLine($"    .desc = {EscapeFluentString(prototype.Description)}");
                }
            }

            writer.WriteLine();
        }

        // Print summary
        var relativePath = Path.GetRelativePath(LocaleOutputPath, targetPath);
        Console.WriteLine($"  Generated: {relativePath} ({prototypesWithData.Count} prototypes)");
    }

    static string EscapeFluentString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Fluent uses backslash for escaping
        // We need to escape: backslashes, newlines, braces, and quotes
        return input
            .Replace("\\", "\\\\")   // Must be first to avoid double-escaping
            .Replace("\r", "\\r")     // Escape carriage returns
            .Replace("\n", "\\n")     // Escape newlines
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("\"", "\\\"");    // Escape quotes
    }

    static string FindRepositoryRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "global.json")) &&
                Directory.Exists(Path.Combine(dir, "Resources")) &&
                Directory.Exists(Path.Combine(dir, "Content.Shared")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find repository root");
    }

    class PrototypeData
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Suffix { get; init; }
        public required string SourceFile { get; init; }

        // Inheritance tracking fields
        public string? InheritedNameFrom { get; set; }
        public string? InheritedDescriptionFrom { get; set; }
        public bool IsAbstract { get; set; }
    }

    class PrototypeTypeConfig
    {
        public string Name { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public string[] Fields { get; init; } = Array.Empty<string>();
    }

    class ConfigRoot
    {
        public List<PrototypeTypeConfig> Types { get; init; } = new();
    }
}
