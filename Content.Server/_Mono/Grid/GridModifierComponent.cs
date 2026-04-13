using Content.Shared._Mono.Grid;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Grid;

[RegisterComponent]
public sealed partial class GridModifierComponent : Component
{
    [DataField]
    public List<ProtoId<GridModificationPrototype>> Modifications = [];
}
