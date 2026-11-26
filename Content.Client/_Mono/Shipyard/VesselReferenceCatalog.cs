using System.Linq;
using Content.Shared._Mono.Shipyard;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.Shipyard;

public sealed class VesselReferenceCatalog
{
    private readonly IPrototypeManager _protos;
    private readonly ILocalizationManager _loc;
    private readonly IResourceManager _resources;

    public VesselReferenceCatalog(
        IPrototypeManager protos,
        ILocalizationManager loc,
        IResourceManager resources)
    {
        _protos = protos;
        _loc = loc;
        _resources = resources;
    }

    public List<VesselReferenceEntry> Build()
    {
        var list = new List<VesselReferenceEntry>();

        foreach (var vessel in _protos.EnumeratePrototypes<VesselPrototype>())
        {
            var entry = new VesselReferenceEntry
            {
                Id = vessel.ID,
                Name = vessel.Name,
                Description = vessel.Description,
                Price = vessel.Price,
                Shipyard = LocalizeShipyardGroup(vessel.Group.ToString()),
                SizeClass = vessel.Category.ToString(),
                EquipmentLoaded = false,
            };

            if (vessel.Classes.Count > 0)
            {
                foreach (var cls in vessel.Classes)
                {
                    var raw = cls.ToString();
                    var key = $"shipyard-console-class-{raw}";
                    entry.Classes.Add(_loc.TryGetString(key, out var locCls) ? locCls : raw);
                }
            }

            list.Add(entry);
        }

        return list
            .OrderBy(e => e.Shipyard, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Classes.FirstOrDefault() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // only load shit once for performance
    public void EnsureEquipment(VesselReferenceEntry entry)
    {
        if (entry.EquipmentLoaded)
            return;

        entry.EquipmentByCategory.Clear();

        if (!_protos.TryIndex<VesselPrototype>(entry.Id, out var vessel))
        {
            entry.EquipmentLoaded = true;
            return;
        }

        var categories = _protos
            .EnumeratePrototypes<VesselReferenceCategoryPrototype>()
            .OrderBy(c => c.Priority)
            .ToList();

        CollectEquipmentFromYaml(vessel, entry, categories);
        entry.EquipmentLoaded = true;
    }

    private void CollectEquipmentFromYaml(
        VesselPrototype vessel,
        VesselReferenceEntry entry,
        List<VesselReferenceCategoryPrototype> categories)
    {
        if (vessel.ShuttlePath == default)
            return;

        if (!_resources.TryContentFileRead(vessel.ShuttlePath, out var stream))
            return;

        string text;
        using (stream)
        using (var reader = new System.IO.StreamReader(stream))
            text = reader.ReadToEnd();

        var protoCounts = CountProtosByEntityChildren(text);

        var byCategory = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (protoId, count) in protoCounts)
        {
            if (count <= 0)
                continue;

            if (!_protos.TryIndex<EntityPrototype>(protoId, out var entProto))
                continue;

            foreach (var cat in categories)
            {
                if (!EntityMatchesCategory(entProto, cat))
                    continue;

                if (!byCategory.TryGetValue(cat.Name, out var nameCounts))
                {
                    nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    byCategory[cat.Name] = nameCounts;
                }

                var displayName = ResolveEntityName(entProto);
                nameCounts[displayName] = nameCounts.GetValueOrDefault(displayName) + count;
            }
        }

        entry.EquipmentByCategory.Clear();
        foreach (var (catName, nameCounts) in byCategory)
        {
            entry.EquipmentByCategory[catName] = nameCounts
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Value > 1 ? $"{kv.Key} {kv.Value}x" : kv.Key)
                .ToList();
        }
    }

    private static Dictionary<string, int> CountProtosByEntityChildren(string text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split('\n');

        string? currentProto = null;
        var inEntities = false;
        var entitiesIndent = -1;
        var count = 0;

        static int IndentOf(string line)
        {
            var i = 0;
            while (i < line.Length && line[i] == ' ')
                i++;
            return i;
        }

        void Commit()
        {
            if (currentProto == null)
                return;

            var n = count > 0 ? count : 1;
            result[currentProto] = result.GetValueOrDefault(currentProto) + n;

            currentProto = null;
            inEntities = false;
            entitiesIndent = -1;
            count = 0;
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var indent = IndentOf(line);
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("proto:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("- proto:", StringComparison.OrdinalIgnoreCase))
            {
                Commit();

                var value = trimmed.Contains('-')
                    ? trimmed.Substring(trimmed.IndexOf("proto:", StringComparison.OrdinalIgnoreCase) + 6)
                    : trimmed.Substring(6);
                currentProto = value.Trim().Trim('"');
                inEntities = false;
                entitiesIndent = -1;
                count = 0;
                continue;
            }

            if (currentProto == null)
                continue;

            if (trimmed.StartsWith("entities:", StringComparison.OrdinalIgnoreCase))
            {
                inEntities = true;
                entitiesIndent = indent;
                continue;
            }

            if (!inEntities)
                continue;

            if (indent <= entitiesIndent && !trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                if (!trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    inEntities = false;
                }
                continue;
            }

            if (trimmed.StartsWith("- uid:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-uid:", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        Commit();
        return result;
    }

    private static bool EntityMatchesCategory(EntityPrototype entProto, VesselReferenceCategoryPrototype cat)
    {
        foreach (var compName in cat.ComponentTypes)
        {
            if (string.IsNullOrWhiteSpace(compName))
                continue;

            if (entProto.Components.ContainsKey(compName))
                return true;
        }

        return false;
    }

    private string ResolveEntityName(EntityPrototype entProto)
    {
        if (!string.IsNullOrEmpty(entProto.Name) &&
            _loc.TryGetString(entProto.Name, out var loc))
            return loc;

        if (!string.IsNullOrEmpty(entProto.Name))
            return entProto.Name;

        return entProto.ID;
    }

    private string LocalizeShipyardGroup(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return _loc.GetString("vessel-reference-shipyard-other");

        var key = $"vessel-reference-shipyard-{group}";
        return _loc.TryGetString(key, out var localized) ? localized : group;
    }
}
