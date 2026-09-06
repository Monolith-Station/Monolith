using System.Threading.Tasks;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("persistence");
    }

    public bool TryGetFlags(EntityUid uid, out IReadOnlyList<string> flags)
    {
        flags = [];
        if (!TryGetProfile(uid, out _, out _, out var profile))
            return false;

        flags = profile.Flags;
        return true;
    }

    public bool AddFlag(EntityUid uid, string key)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile) || profile.Flags.Contains(key))
            return false;

        var flags = new List<string>(profile.Flags) { key };
        SaveProfile(session, slot, profile.WithPersistentData(flags, profile.Components, profile.Items));
        return true;
    }

    public bool RemoveFlag(EntityUid uid, string key)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile))
            return false;

        var flags = new List<string>(profile.Flags);
        if (!flags.Remove(key))
            return false;

        SaveProfile(session, slot, profile.WithPersistentData(flags, profile.Components, profile.Items));
        return true;
    }

    private bool TryGetProfile(
        EntityUid uid,
        out ICommonSession session,
        out int slot,
        out HumanoidCharacterProfile profile)
    {
        session = default!;
        slot = default;
        profile = default!;

        if (!_players.TryGetSessionByEntity(uid, out var foundSession) ||
            !_preferences.TryGetCachedPreferences(foundSession.UserId, out var preferences) ||
            preferences.SelectedCharacter is not HumanoidCharacterProfile humanoid)
        {
            return false;
        }

        session = foundSession;
        slot = preferences.SelectedCharacterIndex;
        profile = humanoid;
        return true;
    }

    private void SaveProfile(ICommonSession session, int slot, HumanoidCharacterProfile profile)
    {
        _ = SaveProfileAsync();

        async Task SaveProfileAsync()
        {
            try
            {
                await _preferences.SetProfile(session.UserId, slot, profile);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to save persistent data for {session.UserId}: {e}");
            }
        }
    }
}
