using System.Text.RegularExpressions;
using Robust.Shared.Maths;

namespace Content.Shared._Forge.Silicons.StationAi;

public static class StationAiCustomizationValidator
{
    public const int MaxNameLength = 32;

    private static readonly Regex RestrictedNameRegex = new("[^\\u0400-\\u04FFa-zA-Z0-9' -]");
    private static readonly Regex NameCaseRegex = new(@"^(?<word>\w)|\b(?<word>\w)(?=\w*$)");

    public static bool TryNormalizeName(
        string input,
        bool restrictedNames,
        bool icNameCase,
        out string name)
    {
        name = input.Trim();
        if (name.Length is 0 or > MaxNameLength)
            return false;

        if (restrictedNames)
        {
            var restricted = RestrictedNameRegex.Replace(name, string.Empty).Trim();
            if (restricted != name)
                return false;
        }

        if (icNameCase)
            name = NameCaseRegex.Replace(name, match => match.Groups["word"].Value.ToUpperInvariant());

        return name.Length is > 0 and <= MaxNameLength;
    }

    public static bool TryNormalizeNamePart(
        string input,
        string forceNamePrefix,
        bool restrictedNames,
        bool icNameCase,
        out string namePart,
        out string fullName)
    {
        fullName = string.Empty;
        if (!TryNormalizeName(input, restrictedNames, icNameCase, out namePart))
            return false;

        fullName = CombineName(forceNamePrefix, namePart);
        return fullName.Length <= MaxNameLength;
    }

    public static string CombineName(string forceNamePrefix, string namePart)
    {
        var prefix = forceNamePrefix.Trim();
        var name = namePart.Trim();
        return prefix.Length == 0 ? name : $"{prefix} {name}";
    }

    public static string GetEditableNamePart(string fullName, string forceNamePrefix)
    {
        var name = fullName.Trim();
        var prefix = forceNamePrefix.Trim();
        if (prefix.Length == 0)
            return name;

        var requiredPrefix = $"{prefix} ";
        return name.StartsWith(requiredPrefix, StringComparison.Ordinal)
            ? name[requiredPrefix.Length..]
            : name;
    }

    public static bool TryNormalizeColor(Color input, out Color color)
    {
        color = Color.White;
        if (!float.IsFinite(input.R) ||
            !float.IsFinite(input.G) ||
            !float.IsFinite(input.B) ||
            !float.IsFinite(input.A))
        {
            return false;
        }

        color = new Color(
            Math.Clamp(input.R, 0f, 1f),
            Math.Clamp(input.G, 0f, 1f),
            Math.Clamp(input.B, 0f, 1f),
            1f);
        return true;
    }
}
