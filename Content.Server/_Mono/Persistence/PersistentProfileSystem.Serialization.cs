using System.IO;
using System.Linq;
using Content.Shared.Preferences;
using Robust.Shared.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;

    private static DataNode Parse(string data)
    {
        using var reader = new StringReader(data);
        return DataNodeParser.ParseYamlStream(reader).Single().Root;
    }

    public void LoadPersistentData(
        EntityUid uid,
        HumanoidCharacterProfile profile,
        ICommonSession? session)
    {
        var components = LoadComponents(uid, profile.Components);
        var items = LoadItems(uid, profile.Items);
        if (session == null ||
            components.Count == profile.Components.Count && items.Count == profile.Items.Count ||
            !_preferences.TryGetCachedPreferences(session.UserId, out var preferences))
        {
            return;
        }

        var slot = preferences.IndexOfCharacter(profile);
        if (slot < 0)
            return;

        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, components, items));
    }
}
