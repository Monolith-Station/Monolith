using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.ShipRepair;

[RegisterComponent]
public sealed partial class ShipRepairToolComponent : Component
{
    [DataField]
    public bool EnableTileRepair = true;

    [DataField]
    public bool EnableEntityRepair = true;

    [DataField]
    public float RepairTimeMultiplier = 1f;

    [DataField]
    public float TileRepairTime = 0.5f;

    [DataField]
    public int TileRepairCost = 1;

    [DataField]
    public float EntitySearchRadius = 0.5f;

    [DataField]
    public SoundSpecifier? RepairSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");

    [ViewVariables]
    public List<DoAfterId> DoAfters = new();

    [DataField]
    public EntProtoId ConstructEffect = "EffectRCDConstruct1";
}
