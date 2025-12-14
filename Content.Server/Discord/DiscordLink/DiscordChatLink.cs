using System.Linq;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Discord.WebSocket;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.Discord.DiscordLink;

public sealed class DiscordChatLink : IPostInjectInit
{
    [Dependency] private readonly DiscordLink _discordLink = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private ulong? _oocChannelId;
    private ulong? _adminChannelId;
    private ulong? _ahelpChannelId;
    private ulong? _deadChatChannelId;

    // Track ahelp threads for each user
    private readonly Dictionary<NetUserId, ulong> _ahelpThreads = new();

    public void Initialize()
    {
        _discordLink.OnMessageReceived += OnMessageReceived;

        _configurationManager.OnValueChanged(CCVars.OocDiscordChannelId, OnOocChannelIdChanged, true);
        _configurationManager.OnValueChanged(CCVars.AdminChatDiscordChannelId, OnAdminChannelIdChanged, true);
        _configurationManager.OnValueChanged(CCVars.AhelpDiscordChannelId, OnAhelpChannelIdChanged, true);
        _configurationManager.OnValueChanged(CCVars.DeadChatDiscordChannelId, OnDeadChatChannelIdChanged, true);
    }

    public void Shutdown()
    {
        _discordLink.OnMessageReceived -= OnMessageReceived;

        _configurationManager.UnsubValueChanged(CCVars.OocDiscordChannelId, OnOocChannelIdChanged);
        _configurationManager.UnsubValueChanged(CCVars.AdminChatDiscordChannelId, OnAdminChannelIdChanged);
        _configurationManager.UnsubValueChanged(CCVars.AhelpDiscordChannelId, OnAhelpChannelIdChanged);
        _configurationManager.UnsubValueChanged(CCVars.DeadChatDiscordChannelId, OnDeadChatChannelIdChanged);
    }

    private void OnOocChannelIdChanged(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            _oocChannelId = null;
            return;
        }

        _oocChannelId = ulong.Parse(channelId);
    }

    private void OnAdminChannelIdChanged(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            _adminChannelId = null;
            return;
        }

        _adminChannelId = ulong.Parse(channelId);
    }

    private void OnAhelpChannelIdChanged(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            _ahelpChannelId = null;
            return;
        }

        _ahelpChannelId = ulong.Parse(channelId);
    }

    private void OnDeadChatChannelIdChanged(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            _deadChatChannelId = null;
            return;
        }

        _deadChatChannelId = ulong.Parse(channelId);
    }

    private void OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot)
            return;

        var contents = message.Content.ReplaceLineEndings(" ");

        if (message.Channel.Id == _oocChannelId)
        {
            // Get Discord user info for better formatting
            _ = Task.Run(async () =>
            {
                var (displayName, _, _) = await _discordLink.GetDiscordUserInfoAsync(message.Author.Id);

                // Fallback to basic Discord username if lookup failed
                if (displayName == "Unknown")
                {
                    displayName = message.Author.GlobalName ?? message.Author.Username;
                }

                _taskManager.RunOnMainThread(() => _chatManager.SendHookOOC(displayName, contents));
            });
        }
        else if (message.Channel.Id == _adminChannelId)
        {
            // Get Discord user info for better formatting
            _ = Task.Run(async () =>
            {
                var (displayName, _, _) = await _discordLink.GetDiscordUserInfoAsync(message.Author.Id);

                // Fallback to basic Discord username if lookup failed
                if (displayName == "Unknown")
                {
                    displayName = message.Author.GlobalName ?? message.Author.Username;
                }

                _taskManager.RunOnMainThread(() => _chatManager.SendHookAdmin(displayName, contents));
            });
        }
        else if (message.Channel.Id == _deadChatChannelId)
        {
            // Get Discord user info for better formatting
            _ = Task.Run(async () =>
            {
                var (displayName, _, _) = await _discordLink.GetDiscordUserInfoAsync(message.Author.Id);

                // Fallback to basic Discord username if lookup failed
                if (displayName == "Unknown")
                {
                    displayName = message.Author.GlobalName ?? message.Author.Username;
                }

                _taskManager.RunOnMainThread(() => _chatManager.SendHookDead(displayName, contents));
            });
        }
        else if (_ahelpThreads.ContainsValue(message.Channel.Id))
        {
            // Find the user ID for this thread
            var userId = _ahelpThreads.FirstOrDefault(x => x.Value == message.Channel.Id).Key;
            if (userId != default)
            {
                // Check for "ao:" prefix to determine if message should be admin-only
                var adminOnly = false;
                var processedContents = contents;
                
                if (contents.StartsWith("ao:", StringComparison.OrdinalIgnoreCase))
                {
                    adminOnly = true;
                    processedContents = contents[3..].TrimStart(); // Remove "ao:" prefix and trim whitespace
                }

                // Get Discord user info for better formatting
                _ = Task.Run(async () =>
                {
                    var (displayName, roleTitle, roleColor) = await _discordLink.GetDiscordUserInfoAsync(message.Author.Id);

                    // Format the author name with role and color
                    var formattedAuthor = FormatDiscordAuthor(displayName, roleTitle, roleColor);

                    _taskManager.RunOnMainThread(() => _chatManager.SendHookAhelp(userId, formattedAuthor, processedContents, adminOnly));
                });
            }
        }
    }

    public async void SendMessage(string message, string author, ChatChannel channel)
    {
        var channelId = channel switch
        {
            ChatChannel.OOC => _oocChannelId,
            ChatChannel.AdminChat => _adminChannelId,
            ChatChannel.Dead => _deadChatChannelId,
            _ => throw new InvalidOperationException("Channel not linked to Discord."),
        };

        if (channelId == null)
        {
            // Configuration not set up. Ignore.
            return;
        }

        // Since we use AllowedMentions.None, we don't need to escape @ symbols
        // Only escape < and / to prevent unwanted formatting and embeds
        message = message.Replace("<", "\\<").Replace("/", "\\/");

        try
        {
            await _discordLink.SendMessageAsync(channelId.Value, $"**{channel.GetString()}**: `{author}`: {message}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending Discord message: {e}");
        }
    }

    /// <summary>
    /// Sends an ahelp message to Discord, creating a thread if necessary.
    /// </summary>
    public async void SendAhelpMessage(NetUserId userId, string playerName, string author, string message, bool adminOnly = false, int? roundId = null, string? characterName = null)
    {
        if (_ahelpChannelId == null)
        {
            // Configuration not set up. Ignore.
            return;
        }

        try
        {
            // Format the message with admin-only indicator if needed
            var adminOnlyPrefix = adminOnly ? "(Admin Only) " : "";
            var formattedMessage = $"{adminOnlyPrefix}`{author}`: {message}";

            // Check if we already have a thread for this user
            if (!_ahelpThreads.TryGetValue(userId, out var threadId))
            {
                // Create a new thread for this ahelp
                var newThreadId = await _discordLink.CreateAhelpThreadAsync(_ahelpChannelId.Value, userId, playerName, formattedMessage, roundId, characterName);
                if (newThreadId.HasValue)
                {
                    _ahelpThreads[userId] = newThreadId.Value;
                }
                return; // Initial message already sent when creating thread
            }

            // Send message to existing thread
            await _discordLink.SendThreadMessageAsync(threadId, formattedMessage);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending ahelp Discord message: {e}");
        }
    }

    /// <summary>
    /// Closes an ahelp thread for a user.
    /// </summary>
    public void CloseAhelpThread(NetUserId userId)
    {
        _ahelpThreads.Remove(userId);
    }

    /// <summary>
    /// Formats a Discord author name with role title and color.
    /// </summary>
    private string FormatDiscordAuthor(string displayName, string? roleTitle, uint? roleColor)
    {
        // Add role title if available
        if (!string.IsNullOrEmpty(roleTitle))
        {
            // Add color formatting if available - escape square brackets to avoid markup conflicts
            if (roleColor.HasValue && roleColor.Value != 0)
            {
                var colorHex = $"#{roleColor.Value:X6}";
                return $"[color={colorHex}]\\[{roleTitle}\\] {displayName}[/color]";
            }
            else
            {
                return $"\\[{roleTitle}\\] {displayName}";
            }
        }

        // No role, just return the display name
        return displayName;
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("discord.chat");
    }
}
