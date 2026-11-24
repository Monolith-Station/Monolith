using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._Mono.Persistence;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using YamlDotNet.Core;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    [Dependency] private IServerDbManager _panelDb = default!;

    internal void SendClipboardYaml(ICommonSession session, string yaml) =>
        RaiseNetworkEvent(new PersistenceClipboardYaml(yaml), session);

    internal async Task<(NetUserId? User, PersistenceUserResult Result)> ReadPanelUser(string username)
    {
        if (_players.TryGetSessionByUsername(username, out var session) &&
            _preferences.TryGetCachedPreferences(session.UserId, out var cached))
            return (session.UserId, Summarize(session.Name, cached));

        var user = await _panelDb.GetPlayerRecordByUserName(username);
        if (user == null)
            return (null, new PersistenceUserResult(null, []));

        var preferences = _preferences.TryGetCachedPreferences(user.UserId, out cached)
            ? cached
            : await _panelDb.GetPlayerPreferencesAsync(user.UserId, CancellationToken.None);
        return (user.UserId, Summarize(user.LastSeenUserName, preferences));
    }

    internal Task<PersistenceEntriesUpdated> AddPanelEntry(NetUserId user, string characterName, AddPersistenceEntry entry)
    {
        if (entry.Blob.Length > AddPersistenceEntry.MaxBlobLength || string.IsNullOrWhiteSpace(entry.Blob) ||
            !(entry.Item ? IsEntityBlob(entry.Blob) : TryDeserializeComponent(entry.Blob, out _)))
            return Task.FromResult(new PersistenceEntriesUpdated(null, Loc.GetString("persistence-panel-entry-invalid")));

        return UpdatePanelProfile(user, entry.Slot, characterName, profile =>
            (entry.Item
                ? profile.WithPersistentData(profile.Flags, profile.Components, [.. profile.Items, new(entry.Blob, entry.Sticky)])
                : profile.WithPersistentData(profile.Flags, [.. profile.Components, new(entry.Blob, entry.Sticky)], profile.Items), null));
    }

    internal Task<PersistenceEntriesUpdated> RemovePanelItem(NetUserId user, int slot, string characterName, int index,
        PersistenceEntry expected) =>
        UpdatePanelProfile(user, slot, characterName, profile =>
        {
            var items = new List<PersistentProfileItem>(profile.Items);
            if (index >= items.Count || items[index].Data != expected.Blob || items[index].Sticky != expected.Sticky)
                return (null, "persistence-panel-item-changed");

            items.RemoveAt(index);
            return (profile.WithPersistentData(profile.Flags, profile.Components, items), null);
        });

    private async Task<PersistenceEntriesUpdated> UpdatePanelProfile(NetUserId user, int slot, string characterName,
        Func<HumanoidCharacterProfile, (HumanoidCharacterProfile? Updated, string? Error)> update)
    {
        _players.TryGetSessionById(user, out var session);
        var cached = _preferences.TryGetCachedPreferences(user, out var preferences);
        if (!cached)
            preferences = await _panelDb.GetPlayerPreferencesAsync(user, CancellationToken.None);

        if (preferences == null || !preferences.Characters.TryGetValue(slot, out var character) ||
            character is not HumanoidCharacterProfile profile || profile.Name != characterName)
            return new(null, Loc.GetString("persistence-panel-entry-character-changed"));

        var (updated, error) = update(profile);
        if (updated == null)
            return new(SummarizeCharacter(slot, profile), Loc.GetString(error!));

        if (session != null && !ServerPreferencesManager.ShouldStorePrefs(session.Channel.AuthType))
        {
            if (!cached || preferences.SelectedCharacterIndex != slot)
                return new(null, Loc.GetString("persistence-panel-entry-character-unavailable"));

            await _preferences.SetProfile(user, slot, updated);
        }
        else
        {
            await _panelDb.SaveCharacterSlotAsync(user, updated, slot);
            if (_players.TryGetSessionById(user, out var current) && _preferences.TryGetCachedPreferences(user, out _))
                await _preferences.RefreshPreferencesAsync(current, CancellationToken.None);
        }

        return new(SummarizeCharacter(slot, updated));
    }

    private static bool IsEntityBlob(string blob)
    {
        try
        {
            return Parse(blob) is MappingDataNode root &&
                root.TryGet<MappingDataNode>("meta", out var meta) &&
                meta.TryGet<ValueDataNode>("category", out var type) && type.Value == "Entity" &&
                root.TryGet<SequenceDataNode>("entities", out var entities) && entities.Count > 0;
        }
        catch (Exception e) when (e is YamlException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static PersistenceUserResult Summarize(string username, PlayerPreferences? preferences) =>
        new(username, preferences?.Characters.OrderBy(pair => pair.Key)
            .Where(pair => pair.Value is HumanoidCharacterProfile)
            .Select(pair => SummarizeCharacter(pair.Key, (HumanoidCharacterProfile) pair.Value)).ToArray() ?? []);

    private static PersistenceCharacter SummarizeCharacter(int slot, HumanoidCharacterProfile profile) =>
        new(slot, profile.WithPersistentData([], [], []), profile.Flags.ToArray(),
            profile.Items.Select(item => new PersistenceEntry(ReadEntryName(item.Data, true), item.Sticky, item.Data)).ToArray(),
            profile.Components.Select(comp => new PersistenceEntry(ReadEntryName(comp.Data, false), comp.Sticky, comp.Data)).ToArray());

    private static string? ReadEntryName(string blob, bool item)
    {
        try
        {
            var node = Parse(blob);
            if (!item)
                return node is SequenceDataNode { Count: 1 } components && components[0] is MappingDataNode component &&
                    component.TryGet<ValueDataNode>("type", out var type) ? type.Value : null;

            if (node is not MappingDataNode root || !root.TryGet<SequenceDataNode>("entities", out var groups))
                return null;

            var rootIds = new[] { "orphans", "nullspace" }
                .Where(key => root.TryGet<SequenceDataNode>(key, out _))
                .SelectMany(key => root.Cast<SequenceDataNode>(key)).OfType<ValueDataNode>()
                .Select(id => id.Value).ToHashSet();
            return groups.OfType<MappingDataNode>().FirstOrDefault(group =>
                    group.TryGet<SequenceDataNode>("entities", out var entries) && entries.OfType<MappingDataNode>()
                        .Any(entity => entity.TryGet<ValueDataNode>("uid", out var uid) && rootIds.Contains(uid.Value)))
                ?.TryGet<ValueDataNode>("proto", out var proto) == true ? proto.Value : null;
        }
        catch (Exception e) when (e is YamlException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
