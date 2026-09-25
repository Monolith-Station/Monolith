using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.MassFabricator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MassFabricatorEyeComponent : Component
{
    [AutoNetworkedField]
    public EntityUid User;

    [AutoNetworkedField]
    public Color Color;

    [AutoNetworkedField]
    public Vector2 BodyPosition;

    [AutoNetworkedField]
    public EntityUid? BodyMap;
}
