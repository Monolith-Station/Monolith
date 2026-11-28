using System.Linq;
using System.Text.Json;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Administration.DeadChatShadowban;

/// <summary>
/// Tracks which players are shadowbanned from dead chat.
/// A shadowbanned player's dead chat messages are only ever sent back to themselves.
/// Shadowbans are kept in memory, which survives round restarts, and saved to a JSON file
/// in the server's data directory, which survives server restarts.
/// </summary>
public sealed partial class DeadChatShadowbanManager
{
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ILogManager _log = default!;

    private static readonly ResPath SavePath = new("/deadchat_shadowbans.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<NetUserId, DeadChatShadowbanEntry> _shadowbans = new();

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Set if the save file exists but couldn't be read, so we never overwrite it and lose the bans in it.
    /// </summary>
    private bool _loadFailed;

    public void Initialize()
    {
        _sawmill = _log.GetSawmill("deadchat.shadowban");
        Load();
    }

    public bool IsShadowbanned(NetUserId player)
    {
        return _shadowbans.ContainsKey(player);
    }

    public bool IsShadowbanned(ICommonSession session)
    {
        return IsShadowbanned(session.UserId);
    }

    /// <summary>
    /// All current shadowbans, oldest first.
    /// </summary>
    public IEnumerable<DeadChatShadowbanEntry> GetShadowbans()
    {
        return _shadowbans.Values.OrderBy(e => e.CreatedAt);
    }

    /// <summary>
    /// Shadowbans a player from dead chat. Takes effect immediately.
    /// </summary>
    /// <returns>False if they were already shadowbanned.</returns>
    public bool AddShadowban(NetUserId player, string username, string adminName, string reason)
    {
        if (_shadowbans.ContainsKey(player))
            return false;

        _shadowbans[player] = new DeadChatShadowbanEntry
        {
            UserId = player.UserId,
            Username = username,
            AdminName = adminName,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
        };

        Save();
        return true;
    }

    /// <summary>
    /// Removes a player's dead chat shadowban. Takes effect immediately.
    /// </summary>
    /// <returns>False if they were not shadowbanned.</returns>
    public bool RemoveShadowban(NetUserId player)
    {
        if (!_shadowbans.Remove(player))
            return false;

        Save();
        return true;
    }

    private void Load()
    {
        if (!_resources.UserData.TryReadAllText(SavePath, out var json))
            return;

        try
        {
            var entries = JsonSerializer.Deserialize<List<DeadChatShadowbanEntry>>(json, JsonOptions) ?? new();
            foreach (var entry in entries)
            {
                _shadowbans[new NetUserId(entry.UserId)] = entry;
            }

            _sawmill.Info($"Loaded {_shadowbans.Count} dead chat shadowban(s).");
        }
        catch (JsonException e)
        {
            // Don't overwrite a file we couldn't read. Admins can fix it by hand.
            _loadFailed = true;
            _sawmill.Error($"Failed to read {SavePath}, dead chat shadowbans will not be loaded or saved until it is fixed: {e.Message}");
        }
    }

    private void Save()
    {
        if (_loadFailed)
        {
            _sawmill.Error($"Not saving dead chat shadowbans because {SavePath} could not be read at startup. Changes this session will be lost on restart.");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(_shadowbans.Values.OrderBy(e => e.CreatedAt).ToList(), JsonOptions);
            _resources.UserData.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save dead chat shadowbans to {SavePath}: {e}");
        }
    }
}

public sealed class DeadChatShadowbanEntry
{
    public Guid UserId { get; set; }

    /// <summary>
    /// The player's username when the shadowban was applied.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The admin who applied the shadowban, or "Server" if applied from the console.
    /// </summary>
    public string AdminName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
