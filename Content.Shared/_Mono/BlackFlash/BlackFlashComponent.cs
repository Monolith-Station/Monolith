using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.BlackFlash;

/// <summary>
/// Grants the Black Flash action. Arm it, land a melee hit inside the window, and the target eats
/// a multiplied hit and gets launched.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackFlashComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionBlackFlash";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public TimeSpan HitCooldown = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan MissCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public float DamageMultiplier = 2.5f;

    [DataField]
    public float StaminaCost = 30f;

    [DataField]
    public TimeSpan Hitstop = TimeSpan.FromSeconds(0.2);

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4);

    [DataField]
    public float ThrowDistance = 14f;

    [DataField]
    public float ThrowSpeed = 28f;

    [DataField]
    public EntProtoId HitEffect = "EffectBlackFlash";

    [DataField]
    public EntProtoId MissEffect = "EffectBlackFlashWhiff";

    [DataField]
    public SoundSpecifier? HitSound = new SoundPathSpecifier("/Audio/_Mono/Items/black_flash.ogg");

    [DataField]
    public SoundSpecifier? MissSound = new SoundPathSpecifier("/Audio/_Mono/Items/black_flash_fumble.ogg");
}
