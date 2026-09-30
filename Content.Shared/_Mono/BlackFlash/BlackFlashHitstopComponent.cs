using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.BlackFlash;

/// <summary>
/// Moments before disaster:
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackFlashHitstopComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LaunchAt;

    [DataField, AutoNetworkedField]
    public Vector2 Direction;

    [DataField, AutoNetworkedField]
    public float Distance;

    [DataField, AutoNetworkedField]
    public float Speed;

    [DataField, AutoNetworkedField]
    public EntityUid? User;
}
