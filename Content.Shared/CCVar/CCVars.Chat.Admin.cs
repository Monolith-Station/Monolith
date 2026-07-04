using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     The discord channel ID to send admin chat messages to (also receive them). This requires the Discord Integration to be enabled and configured.
    /// </summary>
    public static readonly CVarDef<string> AdminChatDiscordChannelId =
        CVarDef.Create("admin.chat_discord_channel_id", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     Mono: The discord channel ID to send ahelp messages to (also receive them). This should be a forum channel for thread support. This requires the Discord Integration to be enabled and configured.
    /// </summary>
    public static readonly CVarDef<string> AhelpDiscordChannelId =
        CVarDef.Create("admin.ahelp_discord_channel_id", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     Mono: The discord channel ID to send dead chat messages to (also receive them). This requires the Discord Integration to be enabled and configured.
    /// </summary>
    public static readonly CVarDef<string> DeadChatDiscordChannelId =
        CVarDef.Create("admin.dead_chat_discord_channel_id", string.Empty, CVar.SERVERONLY);
}
