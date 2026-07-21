using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.PersonalShield;

/// <summary>
/// Mono: New energy shields. Act as a wall until they break.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PersonalShieldComponent : Component
{
    /// <summary>
    /// Settings of the shield itself; stuff like how much damage it takes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public PersonalShieldSettings Shield = new();

    /// <summary>
    /// Current state of the shield. We put it here so the component is """clean"""
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public PersonalShieldRuntime Runtime;

    /// <summary>Is the shield actually on?</summary>
    public bool IsUp => Runtime.Form >= 1f && Runtime.Shatter <= 0f;

    #region Appearance

    /// <summary>Field tint. The alpha scales the strength of the whole effect.</summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#00AAFF").WithAlpha(0.95f);

    /// <summary>
    /// Multiplies the field's RGB brightness.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Brightness = 1.0f;

    /// <summary>
    /// How pixellated the shield itself is.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PixelGrid = 96f;

    /// <summary>
    /// How many hex cells span the sprite.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HexDensity = 4.0f;

    /// <summary>
    /// How much bigger than the wearer's hitbox the bubble is drawn.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Scale = 2.2f;

    /// <summary>
    /// How much the field hollows out toward the middle of the dome. Starch wanted this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CoreFade = 0.85f;

    /// <summary>Strength of the wash inside the shell.</summary>
    [DataField, AutoNetworkedField]
    public float FillLevel = 0.08f;

    /// <summary>Strength of the hex cell borders.</summary>
    [DataField, AutoNetworkedField]
    public float LineLevel = 0.50f;

    /// <summary>Strength of the glow along the dome's limb.</summary>
    [DataField, AutoNetworkedField]
    public float RimLevel = 0.75f;

    /// <summary>
    /// How many alpha bands the field is posterised into.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AlphaBands = 6f;

    /// <summary>
    /// Depth of the slow pulse over the field.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BreathDepth = 0.08f;

    /// <summary>
    /// Where the spin-up crawl sweeps out from.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 FormOrigin = new(0f, 0f); // The Center

    /// <summary>
    /// Scale of the noise when the shield shuts off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShardScale = 5f;

    /// <summary>
    /// How long the shatter animation plays for when the shield breaks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShatterTime = 1.0f;

    #endregion
}

/// <summary>
/// Tuning for how a <see cref="PersonalShieldComponent"/> behaves, kept together so the
/// shield's numbers sit under one YAML node rather than mixed in with its appearance.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PersonalShieldSettings
{
    /// <summary>How much damage the shield can take before it blows up.</summary>
    [DataField]
    public float MaxCharge = 60f;

    /// <summary>Charge regained per second while the shield is on.</summary>
    [DataField]
    public float RegenRate = 2f;

    /// <summary>
    /// How long the shield takes to spin up.
    /// </summary>
    [DataField]
    public float SpinupTime = 4f;

    /// <summary>
    /// How long the shield stays dead after fracturing.
    /// </summary>
    [DataField]
    public float BreakCooldown = 10f;

    /// <summary>
    /// Battery charge drawn per second while the shield is running. If it runs out, no more shield.
    /// </summary>
    [DataField]
    public float PowerDraw = 1.5f;
}


[Serializable, NetSerializable]
public struct PersonalShieldRuntime
{
    /// <summary>Damage capacity remaining.</summary>
    public float Charge;

    /// <summary>
    /// How far the field has crawled in.
    /// </summary>
    public float Form;

    /// <summary>
    /// Progress of the destruction fadeout.
    /// </summary>
    public float Shatter;

    /// <summary>
    /// Seconds left before a fractured shield may start spinning up again.
    /// </summary>
    public float Offline;
}
