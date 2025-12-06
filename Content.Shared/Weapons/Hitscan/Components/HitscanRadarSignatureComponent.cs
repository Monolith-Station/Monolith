using Content.Shared.Weapons.Ranged;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Indicate an entity has a radar signature.
/// Placed on the laser entity being shot, not the gun itself.
/// Contains radar visual related datafields that can be copied over to HitscanRadarComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanRadarSignatureComponent : Component, IShootable
{
    /// <summary>
    /// Color that gets shown on the radar screen for the hitscan line.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("radarColor")]
    public Color RadarColor = Color.Magenta;

    /// <summary>
    /// Thickness of the line drawn on the radar.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("lineThickness")]
    public float LineThickness = 1.0f;

    /// <summary>
    /// Controls whether this hitscan line is visible on radar.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("enabled")]
    public bool Enabled = true;

    /// <summary>
    /// Time this hitscan radar blip should remain visible before being automatically removed.
    /// </summary>
    [DataField]
    public float LifeTime = 0.5f;
}
