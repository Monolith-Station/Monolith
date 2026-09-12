using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Planets.Shields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEShieldGeneratorComponent : Component
{

    [DataField, AutoNetworkedField]
    public CEShieldGeneratorStage Stage = CEShieldGeneratorStage.Charging;

    [DataField]
    public float Charge;

    [DataField]
    public float MaxCharge = 240_000_000_000f;

    [DataField]
    public float DrawRate = 2_000_000_000f;

    [DataField]
    public float MaxDrawRate = 2_000_000_000f;

    [DataField]
    public bool MoonOnly;

    [DataField]
    public TimeSpan SpinupTime = TimeSpan.FromSeconds(79);

    [DataField]
    public TimeSpan SpinupEndsAt;

    [DataField]
    public float SpinupDamageThreshold = 100f;

    [DataField]
    public float SpinupDamageTaken;
    public bool Detonating;

    [DataField]
    public TimeSpan CooldownTime = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan CooldownEndsAt;

    [DataField]
    public SoundSpecifier ChargeSound = new SoundPathSpecifier("/Audio/_CE/Explosions/shieldcharge.ogg");

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_CE/Explosions/shieldgenfire.ogg",
        AudioParams.Default.WithMaxDistance(60f));

    [DataField]
    public SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/_CE/Explosions/shieldfail.ogg");

    [DataField]
    public float FireShakeDuration = 4f;

    [DataField]
    public float FireShakeAmplitude = 1.5f;

    [DataField]
    public TimeSpan FireFlashDuration = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public EntityUid? ChargeSoundStream;

    [ViewVariables]
    public TimeSpan NextUiUpdate;

    [ViewVariables]
    public EntityUid? ShieldedPlanet;

    [DataField]
    public string BeamOverlay = "Planetary";

    public readonly List<EntityUid> BeamControls = new();
}

[Serializable, NetSerializable]
public enum CEShieldGeneratorStage : byte
{
    Charging,
    SpinningUp,
    Active,
    Cooldown,
}

[Serializable, NetSerializable]
public enum CEShieldGeneratorVisuals : byte
{
    Stage,
}

[Serializable, NetSerializable]
public enum CEShieldGeneratorVisualLayers : byte
{
    Indicator,
    Energy,
}
