using Robust.Shared.GameStates;

namespace Content.Shared._Mono.BlackFlash;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackFlashArmedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid User;

    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
