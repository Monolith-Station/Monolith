using Robust.Shared.GameStates;

namespace Content.Shared._Mono.BlackFlash;

/// <summary>
/// Sits on the weapon (or the user themselves, if unarmed) while the Black Flash is armed.
/// Stays put until a melee swing resolves it one way or the other.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackFlashArmedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid User;

    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
