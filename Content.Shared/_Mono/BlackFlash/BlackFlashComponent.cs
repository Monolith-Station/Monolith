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
    [DataField, AutoNetworkedField]
    public bool EmptyHandedOnly = false;

    [DataField]
    public EntProtoId Action = "ActionBlackFlash";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public TimeSpan HitCooldown = TimeSpan.FromMinutes(1);

    [DataField, AutoNetworkedField]
    public TimeSpan MissCooldown = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 2.5f;

    [DataField, AutoNetworkedField]
    public float StaminaCost = 30f;

    [DataField, AutoNetworkedField]
    public TimeSpan Hitstop = TimeSpan.FromSeconds(0.2);

    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float ThrowDistance = 15f;

    [DataField, AutoNetworkedField]
    public float ThrowSpeed = 25f;

    [DataField, AutoNetworkedField]
    public EntProtoId HitEffect = "EffectBlackFlash";

    [DataField, AutoNetworkedField]
    public EntProtoId MissEffect = "EffectBlackFlashWhiff";

    [DataField, AutoNetworkedField]
    public SoundSpecifier? HitSound = new SoundPathSpecifier("/Audio/_Mono/Items/black_flash.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? MissSound = new SoundPathSpecifier("/Audio/_Mono/Items/black_flash_fumble.ogg");
}
