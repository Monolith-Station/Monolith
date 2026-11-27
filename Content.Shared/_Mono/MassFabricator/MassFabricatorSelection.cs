using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.MassFabricator;

[Serializable, NetSerializable]
public sealed class MassFabricatorEraseEvent(NetEntity fabricator, NetEntity[] plans) : EntityEventArgs
{
    public readonly NetEntity Fabricator = fabricator;
    public readonly NetEntity[] Plans = plans;
}

[Serializable, NetSerializable]
public sealed class MassFabricatorPlaceEvent(NetEntity fabricator, NetCoordinates coordinates, Angle rotation,
    ProtoId<MassFabricatorBlueprintPrototype> blueprint, string target) : EntityEventArgs
{
    public readonly NetEntity Fabricator = fabricator;
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly Angle Rotation = rotation;
    public readonly ProtoId<MassFabricatorBlueprintPrototype> Blueprint = blueprint;
    public readonly string Target = target;
}

[Serializable, NetSerializable]
public sealed class MassFabricatorSelectEvent(NetEntity fabricator,
    ProtoId<MassFabricatorBlueprintPrototype>? blueprint, string? target) : EntityEventArgs
{
    public readonly NetEntity Fabricator = fabricator;
    public readonly ProtoId<MassFabricatorBlueprintPrototype>? Blueprint = blueprint;
    public readonly string? Target = target;
}
