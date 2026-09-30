using Content.Server._NF.GameRule;
using Content.Shared._Mono.Grid;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.GameRule.Components;

[RegisterComponent]
public sealed partial class ApplyGridModifierToStationComponent : Component
{
    /// <summary>
    /// The modifiers to apply to each POI.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<PointOfInterestPrototype>, List<ProtoId<GridModificationPrototype>>> Modifiers;
}