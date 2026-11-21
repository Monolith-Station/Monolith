using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Ships;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipShieldColliderComponent : Component
{
    [AutoNetworkedField] public EntityUid? Grid;
    [AutoNetworkedField] public EntityUid? Shield;
}
