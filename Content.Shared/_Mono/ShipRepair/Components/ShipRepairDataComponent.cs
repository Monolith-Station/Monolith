using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Mono.ShipRepair.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipRepairDataComponent : Component
{
    /// <summary>
    /// N to use for the NxN chunks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ChunkSize = 5;

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ShipRepairChunk> Chunks = new();

    /// <summary>
    /// A map of index to EntProtoId to not have to store a whole string for each repairable entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> EntityPalette = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ShipRepairChunk
{
    /// <summary>
    /// Flattened array of tiles for this chunk. Access via x + y * size.
    /// </summary>
    [DataField]
    public int[] Tiles = default!;

    /// <summary>
    /// Dictionary of uid -> repair specifier to be able to uniquely refer to entities to repair.
    /// </summary>
    [DataField]
    public Dictionary<int, ShipRepairEntitySpecifier> Entities = new();

    [DataField]
    public int NextUid = 0;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ShipRepairEntitySpecifier
{
    /// <summary>
    /// Index pointing to <see cref="ShipRepairDataComponent.EntityPalette"/>.
    /// </summary>
    [DataField]
    public int ProtoIndex;

    /// <summary>
    /// Grid-relative position.
    /// </summary>
    [DataField]
    public Vector2 LocalPosition;

    [DataField]
    public Angle Rotation;

    /// <summary>
    /// Original entity this was snapshotted from.
    /// </summary>
    [DataField]
    public NetEntity? OriginalEntity;
}
