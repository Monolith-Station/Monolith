using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Content.Shared.CCVar;
using Content.Client.CharacterInfo;
using static Content.Client.CharacterInfo.CharacterInfoSystem;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController : IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;

    private static readonly Regex StartDoubleQuote = new("\"$");
    private static readonly Regex EndDoubleQuote = new("^\"|(?<=^@)\"");
    private static readonly Regex StartAtSign = new("^@");


    private readonly List<string> _highlights = new();
    private readonly List<string> _autoFillRawKeywords = new();

    private string? _highlightsColor;
    private bool _autoFillHighlightsEnabled;
    private bool _charInfoIsAttach = false;
    private string? _currentCharacterName;


    public event Action<string>? HighlightsUpdated;
    public event Action<string>? AutoFillUpdated;

    private void InitializeHighlights()
    {
        _config.OnValueChanged(CCVars.ChatAutoFillHighlights, (value) => { _autoFillHighlightsEnabled = value; }, true);
        _config.OnValueChanged(CCVars.ChatHighlightsColor, (value) => { _highlightsColor = value; }, true);

        var cvar = _config.GetCVar(CCVars.ChatHighlights);
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += OnCharacterUpdated;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= OnCharacterUpdated;
    }

    private void UpdateAutoFillHighlights()
    {
        _currentCharacterName = null;
        _autoFillRawKeywords.Clear();
        _highlights.Clear();
        AutoFillUpdated?.Invoke("");
        HighlightsUpdated?.Invoke("");

        _charInfoIsAttach = true;
        _characterInfo.RequestCharacterInfo();
    }

    /// <summary>
    ///     Saves the user's custom keywords for the given character into the CVar,
    ///     then rebuilds the active highlight list.
    ///     Called when the user submits keywords from the popup.
    /// </summary>
    public void UpdateHighlights(string customKeywords)
    {
        if (_currentCharacterName == null)
        {
            return;
        }

        SaveCustomKeywords(_currentCharacterName, customKeywords);
        RebuildHighlightsList(customKeywords);
        HighlightsUpdated?.Invoke(customKeywords);
    }

    private void OnCharacterUpdated(CharacterData data)
    {

        if (!_charInfoIsAttach)
        {
            return;
        }

        var (_, job, _, _, entityName) = data;
        _currentCharacterName = entityName;


        _autoFillRawKeywords.Clear();
        var autoFillDisplay = "";

        if (_autoFillHighlightsEnabled)
        {

            // Special rules for names can be added here

            var nameHighlights = "@" + entityName;

            // Split on spaces first (handles any number of space-separated words).
            if (nameHighlights.Contains(' '))
                nameHighlights = nameHighlights.Replace(" ", "\n@");

            // Handle hyphenated tokens: single hyphen splits normally (e.g. "First-Last"),
            // multiple hyphens use the lizard rule — keep only first and last part (e.g. "Eats-The-Food" → @Eats @Food).
            var dashParts = nameHighlights.Split('-');
            if (dashParts.Length == 2)
                nameHighlights = nameHighlights.Replace("-", "\n@");
            else if (dashParts.Length > 2)
                nameHighlights = dashParts[0] + "\n@" + dashParts[^1];


            foreach (var token in nameHighlights.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                _autoFillRawKeywords.Add(token);
            }

            var jobKey = job.Replace(' ', '-').ToLower();

            if (_loc.TryGetString($"highlights-{jobKey}", out var jobMatches))
            {
                foreach (var jobWord in jobMatches.Replace(", ", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    _autoFillRawKeywords.Add(jobWord);
            }
            autoFillDisplay = string.Join("\n", _autoFillRawKeywords);
        }

        var customKeywords = LoadCustomKeywords(entityName);

        RebuildHighlightsList(customKeywords);

        AutoFillUpdated?.Invoke(autoFillDisplay);
        HighlightsUpdated?.Invoke(customKeywords);
        _charInfoIsAttach = false;
    }


    private string LoadCustomKeywords(string characterName)
    {
        var cvar = _config.GetCVar(CCVars.ChatHighlights);
        if (string.IsNullOrEmpty(cvar))
            return "";

        // Shouldn't be needed as no-one should have data by the time this rolls out but we never know
        if (!cvar.Contains("||"))
        {
            return cvar;
        }

        foreach (var line in cvar.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var sep = line.IndexOf("||", StringComparison.Ordinal);
            if (sep < 0)
                continue;

            if (line[..sep] == characterName)
                return DecodeKeywords(line[(sep + 2)..]);
        }

        return "";
    }


    private void SaveCustomKeywords(string characterName, string customKeywords)
    {
        var cvar = _config.GetCVar(CCVars.ChatHighlights);

        var entries = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(cvar) && cvar.Contains("||"))
        {
            foreach (var line in cvar.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var sep = line.IndexOf("||", StringComparison.Ordinal);
                if (sep < 0)
                    continue;
                entries[line[..sep]] = line[(sep + 2)..];
            }
        }

        entries[characterName] = EncodeKeywords(customKeywords);

        var sb = new StringBuilder();
        foreach (var (name, encoded) in entries)
            sb.Append(name).Append("||").Append(encoded).Append('\n');

        var newValue = sb.ToString().TrimEnd('\n');
        _config.SetCVar(CCVars.ChatHighlights, newValue);
        _config.SaveToFile();
    }

    private static string EncodeKeywords(string raw)
    {
        return raw
            .Replace(@"\", @"\\")
            .Replace("|", @"\|")
            .Replace("\n", @"\n");
    }

    private static string DecodeKeywords(string encoded)
    {
        var sb = new StringBuilder(encoded.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            if (encoded[i] == '\\' && i + 1 < encoded.Length)
            {
                switch (encoded[i + 1])
                {
                    case 'n':  sb.Append('\n'); i++; break;
                    case '|':  sb.Append('|');  i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    default:   sb.Append(encoded[i]); break;
                }
            }
            else
            {
                sb.Append(encoded[i]);
            }
        }
        return sb.ToString();
    }

    private void RebuildHighlightsList(string customRaw)
    {
        _highlights.Clear();

        foreach (var raw in _autoFillRawKeywords)
            _highlights.Add(ProcessKeyword(raw));

        if (!string.IsNullOrWhiteSpace(customRaw))
        {
            foreach (var raw in customRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                _highlights.Add(ProcessKeyword(raw));
        }

        _highlights.Sort((x, y) => y.Length.CompareTo(x.Length));
    }

    private string ProcessKeyword(string raw)
    {
        // Replace every "\" to prevent "\n", "\0", etc. in regex.
        var keyword = raw.Replace(@"\", @"\\");

        // Escape special regex characters.
        keyword = Regex.Escape(keyword);

        // Re-escape "[" for markup-adjacent positions.
        keyword = keyword.Replace(@"\[", @"\\\[");

        // words in double quotes will only be hightlighted as unique, not within another word
        if (keyword.Any(c => c == '"'))
        {
            keyword = StartDoubleQuote.Replace(keyword, "(?!\\w)");
            keyword = EndDoubleQuote.Replace(keyword, "(?<!\\w)");
        }

        // Make it so it doesn't hightlight your own name when you are talking
        keyword = StartAtSign.Replace(keyword, @"(?<=\[BubbleContent\].*|""\[/color\].*)");

        return keyword;
    }
}
