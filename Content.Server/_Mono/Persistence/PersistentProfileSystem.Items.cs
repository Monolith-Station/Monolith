using System.IO;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    public bool TryGetItems(EntityUid uid, out IReadOnlyList<string> items)
    {
        items = [];
        if (!TryGetProfile(uid, out _, out _, out var profile))
            return false;

        items = profile.Items;
        return true;
    }

    public bool AddItem(EntityUid uid, string item)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile) || profile.Items.Contains(item))
            return false;

        var items = new List<string>(profile.Items) { item };
        _preferences.SetProfile(session.UserId, slot,
            profile.WithPersistentData(profile.Flags, profile.Components, items)).GetAwaiter().GetResult();
        return true;
    }

    public bool AddItem(EntityUid uid, EntityUid item)
        => TrySerializeEntity(item, out var data)
            ? AddItem(uid, data)
            : false;

    public bool RemoveItem(EntityUid uid, string item)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile))
            return false;

        var items = new List<string>(profile.Items);
        if (!items.Remove(item))
            return false;

        _preferences.SetProfile(session.UserId, slot,
            profile.WithPersistentData(profile.Flags, profile.Components, items)).GetAwaiter().GetResult();
        return true;
    }

    public bool TrySerializeEntity(EntityUid uid, out string data)
    {
        data = string.Empty;
        using var writer = new StringWriter();
        if (!_mapLoader.TrySaveEntity(uid, writer))
            return false;

        data = Encode(writer.ToString());
        return true;
    }

    public bool TryDeserializeEntity(string data, out EntityUid uid)
    {
        uid = default;
        try
        {
            using var reader = new StringReader(Decode(data));
            if (!_mapLoader.TryLoadEntity(reader, "persistent profile", out var entity))
                return false;

            uid = entity.Value.Owner;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
