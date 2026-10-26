using System.Text.RegularExpressions;

namespace Content.Server._Andromeda.Chat.Managers;

public sealed partial class ChatSanitizationManager
{
    // Andromeda start: Brasilian netspeak laughs
    // TODO: Make this in a partial class most likely
    /// <summary>
    /// Builds a regex meant to be used for chatsan;
    /// Catches words that consist of only the passed characters, as long as they're above the minimum characters number passed.
    /// </summary>
    /// <param name="characters">Characters to look for</param>
    /// <param name="minCharacters">Minimum characters the word needs to have</param>
    /// <param name="emoteKey">Emote key to return alongside built regex</param>
    /// <returns>Tuple containing built regex and emote key</returns>
    private static (Regex regex, string emoteKey) AnyCharacterEntry(string characters, int minCharacters, string emoteKey)
    {
        // no need for shorthand escaping
        var pattern = new Regex(
            $@"^[{characters}]{{{minCharacters},}}+\b",
            RegexOptions.RightToLeft | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        return (pattern, emoteKey);
    }
    // Andromeda end
}
