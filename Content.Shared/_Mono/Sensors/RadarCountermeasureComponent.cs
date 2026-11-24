using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Sensors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RadarCountermeasureComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 40f;

    /// <summary>
    /// 0 does nothing, 1 fully blocks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Strength = 0.85f;

    [DataField, AutoNetworkedField]
    public float MaxEffectRange = 800f;

    [DataField, AutoNetworkedField]
    public float MinHalfAngleDegrees = 8f;

    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// How many ARS-only false contacts this cloud presents.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DecoyContacts = 1;

    /// <summary>
    /// Spread of decoy contact offsets around the cloud center.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DecoySpread = 25f;

    /// <summary>
    /// Effective RCS multiplier used only for ARS range checks against this cloud.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DecoyRadarSize = 40f;
}
