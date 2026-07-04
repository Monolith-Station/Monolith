using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Ships.Components;

/// <summary>
/// This is used for storing the ID of the VesselPrototype for a ship.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VesselComponent : Component
{
    [DataField]
    public ProtoId<VesselPrototype> VesselId { get; set; }
}
