using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.PersonalShield;

/// <summary>
/// Mono - New energy shields.
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

    /// <summary>Rendering parameters for the shield field.</summary>
    [DataField, AutoNetworkedField]
    public PersonalShieldVisuals Visuals = new();
}

/// <summary>
/// Tuning for how a <see cref="PersonalShieldComponent"/> behaves, kept together so the
/// shield's numbers sit under one YAML node rather than mixed in with its appearance.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PersonalShieldSettings
{
    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.OUTERCLOTHING;

    /// <summary>How much damage the shield can take before it blows up.</summary>
    [DataField]
    public float MaxCharge = 60f;

    /// <summary>Charge regained per second while the shield is on.</summary>
    [DataField]
    public float RegenRate = 2f;

    /// <summary>
    /// The damage modifier for shield.
    /// </summary>
    [DataField("blockModifier")]
    public DamageModifierSet BlockDamageModifier = new DamageModifierSet();

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


[DataDefinition, Serializable, NetSerializable]
public sealed partial class PersonalShieldVisuals
{
    [DataField] public Color Color = Color.FromHex("#00AAFF").WithAlpha(0.95f);
    [DataField] public float Brightness = 1.0f;
    [DataField] public float MinimumBrightness = 0.25f;
    [DataField] public float PixelGrid = 96f;
    [DataField] public float HexDensity = 4.0f;
    [DataField] public float Scale = 2.2f;
    [DataField] public float CoreFade = 0.85f;
    [DataField] public float FillLevel = 0.08f;
    [DataField] public float LineLevel = 0.50f;
    [DataField] public float RimLevel = 0.75f;
    [DataField] public float AlphaBands = 6f;
    [DataField] public float BreathDepth = 0.08f;
    [DataField] public Vector2 FormOrigin = new(0f, 0f);
    [DataField] public float DamageFlareTime = 0.5f;
    [DataField] public float ShardScale = 5f;
    [DataField] public float ShatterTime = 1.0f;
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

    /// <summary>
    /// Time at which the current damage flare finishes settling.
    /// </summary>
    public TimeSpan DamageFlareUntil;
}
