using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.StarSystem;

/// <summary>
/// Added to a star system map to paint starlight onto tiles that can see the star.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarLightComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public float Intensity = 2f;

    /// <summary>
    /// Floor applied outside the star's reach so deep space isn't pitch black.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color AmbientFloor = Color.FromHex("#0A0A14");

    /// <summary>
    /// Light range in world units per solar radius.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RangeFactor = 30000f;

    [DataField, AutoNetworkedField]
    public float RadiusFactor = 500f;

    [DataField, AutoNetworkedField]
    public float Falloff = 2f;

    /// <inheritdoc cref="Robust.Shared.GameObjects.SharedPointLightComponent.CurveFactor"/>
    [DataField, AutoNetworkedField]
    public float CurveFactor;

    [DataField, AutoNetworkedField]
    public float ShadowLength = 48f;

    [DataField, AutoNetworkedField]
    public float ShadowStrength = 1f;
}
