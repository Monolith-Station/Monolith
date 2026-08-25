using System.IO;
using Content.Shared.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public void LoadItems(EntityUid uid, IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            if (!TryDeserializeEntity(item, out var itemUid))
            {
                _sawmill.Error($"Failed to load a persistent item for {ToPrettyString(uid)}");
                continue;
            }

            GiveItem(uid, itemUid);
        }
    }

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
        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, profile.Components, items));
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

        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, profile.Components, items));
        return true;
    }

    public bool TrySerializeEntity(EntityUid uid, out string data)
    {
        data = string.Empty;
        using var writer = new StringWriter();
        if (!_mapLoader.TrySaveEntity(uid, writer))
            return false;

        data = writer.ToString();
        return true;
    }

    public bool TryDeserializeEntity(string data, out EntityUid uid)
    {
        uid = default;
        try
        {
            using var reader = new StringReader(data);
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

    private void GiveItem(EntityUid uid, EntityUid item)
    {
        if (TryComp<ClothingComponent>(item, out var clothing) && _inventory.TryGetSlots(uid, out var slots))
        {
            foreach (var slot in slots)
            {
                if ((slot.SlotFlags & clothing.Slots) != 0 &&
                    _inventory.TryEquip(uid, item, slot.Name, silent: true, force: true))
                {
                    return;
                }
            }
        }

        _hands.PickupOrDrop(uid, item, checkActionBlocker: false, dropNear: true);
    }
}
