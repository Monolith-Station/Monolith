using System.Linq;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Discord;
using Discord.WebSocket;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;
using LogMessage = Discord.LogMessage;

namespace Content.Server.Discord.DiscordLink;

/// <summary>
/// Represents the arguments for the <see cref="DiscordLink.OnCommandReceived"/> event.
/// </summary>
public sealed class CommandReceivedEventArgs
{
    /// <summary>
    /// The command that was received. This is the first word in the message, after the bot prefix.
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// The arguments to the command. This is everything after the command
    /// </summary>
    public string Arguments { get; init; } = string.Empty;
    /// <summary>
    /// Information about the message that the command was received from. This includes the message content, author, etc.
    /// Use this to reply to the message, delete it, etc.
    /// </summary>
    public SocketMessage Message { get; init; } = default!;
}

/// <summary>
/// Handles the connection to Discord and provides methods to interact with it.
/// </summary>
public sealed partial class DiscordLink : IPostInjectInit
{
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    /// <summary>
    ///    The Discord client. This is null if the bot is not connected.
    /// </summary>
    /// <remarks>
    ///     This should not be used directly outside of DiscordLink. So please do not make it public. Use the methods in this class instead.
    /// </remarks>
    private DiscordSocketClient? _client;
    private ISawmill _sawmill = default!;
    private ISawmill _sawmillLog = default!;

    private ulong _guildId;
    private string _botToken = string.Empty;

    public string BotPrefix = default!;
    /// <summary>
    /// If the bot is currently connected to Discord.
    /// </summary>
    public bool IsConnected => _client != null;

    #region Events

    /// <summary>
    ///     Event that is raised when a command is received from Discord.
    /// </summary>
    public event Action<CommandReceivedEventArgs>? OnCommandReceived;
    /// <summary>
    ///     Event that is raised when a message is received from Discord. This is raised for every message, including commands.
    /// </summary>
    public event Action<SocketMessage>? OnMessageReceived;

    public void RegisterCommandCallback(Action<CommandReceivedEventArgs> callback, string command)
    {
        OnCommandReceived += args =>
        {
            if (args.Command == command)
                callback(args);
        };
    }

    #endregion

    public void Initialize()
    {
        _configuration.OnValueChanged(CCVars.DiscordGuildId, OnGuildIdChanged, true);
        _configuration.OnValueChanged(CCVars.DiscordPrefix, OnPrefixChanged, true);

        if (_configuration.GetCVar(CCVars.DiscordToken) is not { } token || token == string.Empty)
        {
            _sawmill.Info("No Discord token specified, not connecting.");
            return;
        }

        // If the Guild ID is empty OR the prefix is empty, we don't want to connect to Discord.
        if (_guildId == 0 || BotPrefix == string.Empty)
        {
            // This is a warning, not info, because it's a configuration error.
            // It is valid to not have a Discord token set which is why the above check is an info.
            // But if you have a token set, you should also have a guild ID and prefix set.
            _sawmill.Warning("No Discord guild ID or prefix specified, not connecting.");
            return;
        }

        _client = new DiscordSocketClient(new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds
                             | GatewayIntents.GuildMembers
                             | GatewayIntents.GuildMessages
                             | GatewayIntents.MessageContent
                             | GatewayIntents.DirectMessages,
        });
        _client.Log += Log;
        _client.MessageReceived += OnCommandReceivedInternal;
        _client.MessageReceived += OnMessageReceivedInternal;

        _botToken = token;
        // Since you cannot change the token while the server is running / the DiscordLink is initialized,
        // we can just set the token without updating it every time the cvar changes.

        _client.Ready += () =>
        {
            _sawmill.Info("Discord client ready.");
            return Task.CompletedTask;
        };

        Task.Run(async () =>
        {
            try
            {
                await LoginAsync(token);
            }
            catch (Exception e)
            {
                _sawmill.Error("Failed to connect to Discord!", e);
            }
        });
    }

    public async Task Shutdown()
    {
        if (_client != null)
        {
            _sawmill.Info("Disconnecting from Discord.");

            // Unsubscribe from the events.
            _client.MessageReceived -= OnCommandReceivedInternal;
            _client.MessageReceived -= OnMessageReceivedInternal;

            await _client.LogoutAsync();
            await _client.StopAsync();
            await _client.DisposeAsync();
            _client = null;
        }

        _configuration.UnsubValueChanged(CCVars.DiscordGuildId, OnGuildIdChanged);
        _configuration.UnsubValueChanged(CCVars.DiscordPrefix, OnPrefixChanged);
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("discord.link");
        _sawmillLog = _logManager.GetSawmill("discord.link.log");
    }

    private void OnGuildIdChanged(string guildId)
    {
        _guildId = ulong.TryParse(guildId, out var id) ? id : 0;
    }

    private void OnPrefixChanged(string prefix)
    {
        BotPrefix = prefix;
    }

    private async Task LoginAsync(string token)
    {
        DebugTools.Assert(_client != null);
        DebugTools.Assert(_client.LoginState == LoginState.LoggedOut);

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();


        _sawmill.Info("Connected to Discord.");
    }

    private string FormatLog(LogMessage msg)
    {
        return msg.Exception is null
            ? $"{msg.Source}: {msg.Message}"
            : $"{msg.Source}: {msg.Message}\n{msg.Exception}";
    }

    private Task Log(LogMessage msg)
    {
        var logLevel = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Fatal,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Debug
        };

        _sawmillLog.Log(logLevel, FormatLog(msg));
        return Task.CompletedTask;
    }

    private Task OnCommandReceivedInternal(SocketMessage message)
    {
        var content = message.Content;
        // If the message doesn't start with the bot prefix, ignore it.
        if (!content.StartsWith(BotPrefix))
            return Task.CompletedTask;

        // Split the message into the command and the arguments.
        var trimmedInput = content[BotPrefix.Length..].Trim();
        var firstSpaceIndex = trimmedInput.IndexOf(' ');

        string command, arguments;

        if (firstSpaceIndex == -1)
        {
            command = trimmedInput;
            arguments = string.Empty;
        }
        else
        {
            command = trimmedInput[..firstSpaceIndex];
            arguments = trimmedInput[(firstSpaceIndex + 1)..].Trim();
        }

        // Raise the event!
        OnCommandReceived?.Invoke(new CommandReceivedEventArgs
        {
            Command = command,
            Arguments = arguments,
            Message = message,
        });
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedInternal(SocketMessage message)
    {
        OnMessageReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    #region Proxy methods

    /// <summary>
    /// Sends a message to a Discord channel with the specified ID. Without any mentions.
    /// </summary>
    public async Task SendMessageAsync(ulong channelId, string message)
    {
        if (_client == null)
        {
            return;
        }

        var channel = _client.GetChannel(channelId) as IMessageChannel;
        if (channel == null)
        {
            _sawmill.Error("Tried to send a message to Discord but the channel {Channel} was not found.", channel);
            return;
        }

        await channel.SendMessageAsync(message, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    /// Creates a thread in a forum channel for an ahelp conversation.
    /// </summary>
    public async Task<ulong?> CreateAhelpThreadAsync(ulong forumChannelId, NetUserId userId, string playerName, string initialMessage, int? roundId = null, string? characterName = null)
    {
        if (_client == null)
        {
            return null;
        }

        var channel = _client.GetChannel(forumChannelId) as IForumChannel;
        if (channel == null)
        {
            _sawmill.Error("Tried to create ahelp thread but the forum channel {Channel} was not found.", forumChannelId);
            return null;
        }

        try
        {
            // Build thread title with Round ID and character name
            var titleParts = new List<string>();

            if (roundId.HasValue)
                titleParts.Add($"R{roundId.Value}");

            if (!string.IsNullOrEmpty(characterName))
                titleParts.Add(characterName);

            titleParts.Add($"{playerName} ({userId})");

            var threadName = string.Join(" - ", titleParts);

            // Since we use AllowedMentions.None, we don't need to escape @ symbols
            // Only escape < and / to prevent unwanted formatting
            var sanitizedMessage = initialMessage.Replace("<", "\\<").Replace("/", "\\/");

            var thread = await channel.CreatePostAsync(threadName, ThreadArchiveDuration.OneDay, null, sanitizedMessage, allowedMentions: AllowedMentions.None);
            return thread.Id;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while creating ahelp thread: {e}");
            return null;
        }
    }

    /// <summary>
    /// Gets Discord user information including nickname and top role.
    /// </summary>
    public async Task<(string displayName, string? roleTitle, uint? roleColor)> GetDiscordUserInfoAsync(ulong userId)
    {
        if (_client == null || _guildId == 0)
        {
            return ("Unknown", null, null);
        }

        try
        {
            var guild = _client.GetGuild(_guildId);
            if (guild == null)
            {
                return ("Unknown", null, null);
            }

            var user = guild.GetUser(userId);
            if (user == null)
            {
                return ("Unknown", null, null);
            }

            var displayName = user.DisplayName; // This gets nickname if set, otherwise username

            // Get the highest role (excluding @everyone)
            var topRole = user.Roles
                .Where(r => !r.IsEveryone)
                .OrderByDescending(r => r.Position)
                .FirstOrDefault();

            var roleTitle = topRole?.Name;
            var roleColor = topRole?.Color.RawValue;

            return (displayName, roleTitle, roleColor);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while getting Discord user info: {e}");
            return ("Unknown", null, null);
        }
    }

    /// <summary>
    /// Sends a message to a Discord thread with the specified ID. Without any mentions.
    /// </summary>
    public async Task SendThreadMessageAsync(ulong threadId, string message)
    {
        if (_client == null)
        {
            return;
        }

        var thread = _client.GetChannel(threadId) as IMessageChannel;
        if (thread == null)
        {
            _sawmill.Error("Tried to send a message to Discord thread but the thread {Thread} was not found.", threadId);
            return;
        }

        try
        {
            // Since we use AllowedMentions.None, we don't need to escape @ symbols
            // Only escape < and / to prevent unwanted formatting
            var sanitizedMessage = message.Replace("<", "\\<").Replace("/", "\\/");
            await thread.SendMessageAsync(sanitizedMessage, allowedMentions: AllowedMentions.None);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending message to Discord thread: {e}");
        }
    }

    #endregion
}
