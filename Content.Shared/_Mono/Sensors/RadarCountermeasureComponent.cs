using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Sensors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RadarCountermeasureComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 40f;

    /// 0 does nothing 1 fully blocks
    [DataField, AutoNetworkedField]
    public float Strength = 0.85f;

    [DataField, AutoNetworkedField]
    public float MaxEffectRange = 800f;

    [DataField, AutoNetworkedField]
    public float MinHalfAngleDegrees = 8f;

    [DataField, AutoNetworkedField]
    public bool Active = true;
}
