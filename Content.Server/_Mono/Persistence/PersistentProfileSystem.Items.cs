using System.IO;
using System.Linq;
using Content.Shared._Mono.Persistence;
using Content.Shared.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Preferences;

namespace Content.Server._Mono.Persistence;

public sealed partial class PersistentProfileSystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private List<PersistentProfileItem> LoadItems(EntityUid uid, IEnumerable<PersistentProfileItem> items)
    {
        var kept = new List<PersistentProfileItem>();
        foreach (var item in items)
        {
            if (!TryDeserializeEntity(item.Data, out var itemUid))
            {
                _sawmill.Error($"Failed to load a persistent item for {ToPrettyString(uid)}");
                continue;
            }

            if (item.Sticky)
            {
                RemComp<PersistAtRoundEndComponent>(itemUid);
                kept.Add(item);
            }

            GiveItem(uid, itemUid);
        }

        return kept;
    }

    public bool TryGetItems(EntityUid uid, out IReadOnlyList<PersistentProfileItem> items)
    {
        items = [];
        if (!TryGetProfile(uid, out _, out _, out var profile))
            return false;

        items = profile.Items;
        return true;
    }

    private bool AddItem(EntityUid uid, PersistentProfileItem item)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile) ||
            profile.Items.Any(entry => entry.Data == item.Data))
            return false;

        var items = new List<PersistentProfileItem>(profile.Items) { item };
        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, profile.Components, items));
        return true;
    }

    public bool AddItem(EntityUid uid, EntityUid item, bool sticky = false)
    {
        if (!TrySerializeEntity(item, out var data))
            return false;

        return AddItem(uid, new PersistentProfileItem(data, sticky));
    }

    public bool SaveRoundEndItems(EntityUid uid, IEnumerable<EntityUid> entities)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile))
            return false;

        var items = new List<PersistentProfileItem>(profile.Items);
        foreach (var entity in entities)
        {
            var sticky = false;
            if (TryComp(entity, out PersistAtRoundEndComponent? persistence) &&
                (persistence.Sticky || persistence.Once))
            {
                sticky = persistence.Sticky;
                RemComp(entity, persistence);
            }

            if (!TrySerializeEntity(entity, out var data) || items.Any(item => item.Data == data))
                continue;

            items.Add(new PersistentProfileItem(data, sticky));
        }

        if (items.Count == profile.Items.Count)
            return false;

        SaveProfile(session, slot, profile.WithPersistentData(profile.Flags, profile.Components, items));
        return true;
    }

    public bool RemoveItem(EntityUid uid, string item)
    {
        if (!TryGetProfile(uid, out var session, out var slot, out var profile))
            return false;

        var items = new List<PersistentProfileItem>(profile.Items);
        var entry = items.FirstOrDefault(value => value.Data == item);
        if (entry == null)
            return false;

        items.Remove(entry);

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
