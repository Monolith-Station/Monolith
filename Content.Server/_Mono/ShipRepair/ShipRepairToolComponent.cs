using Robust.Shared.Audio;

namespace Content.Server._Mono.ShipRepair;

[RegisterComponent]
public sealed partial class ShipRepairToolComponent : Component
{
    [DataField]
    public bool EnableTileRepair = true;

    [DataField]
    public bool EnableEntityRepair = true;

    [DataField]
    public float TileRepairTime = 0.5f;

    // TODO: ask entity how long it wants to spend being repaired
    [DataField]
    public float EntityRepairTime = 2f;

    [DataField]
    public float EntitySearchRadius = 0.5f;

    [DataField]
    public SoundSpecifier? RepairSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
}
