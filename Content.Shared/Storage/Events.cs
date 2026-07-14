using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Storage;

[Serializable, NetSerializable]
public sealed partial class AreaPickupDoAfterEvent : DoAfterEvent
{
    [DataField("entities", required: true)]
    public IReadOnlyList<NetEntity> Entities = default!;

    private AreaPickupDoAfterEvent()
    {
    }

    public AreaPickupDoAfterEvent(List<NetEntity> entities)
    {
        Entities = entities;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class InsertItemIntoStorageDoAfterEvent : DoAfterEvent
{
    [DataField("toInsert", required: true)]
    public NetEntity ToInsert;

    [DataField("location")]
    public ItemStorageLocation? Location;

    private InsertItemIntoStorageDoAfterEvent()
    {
    }

    public InsertItemIntoStorageDoAfterEvent(NetEntity toInsert, ItemStorageLocation? location = null)
    {
        ToInsert = toInsert;
        Location = location;
    }

    public override DoAfterEvent Clone() => this;
}
