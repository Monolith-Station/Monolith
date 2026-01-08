using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Mono.ForceParent;

[RegisterComponent, NetworkedComponent]
public sealed partial class ForceParentComponent : Component
{
    [DataField]
    public EntityCoordinates Position;
}
