using Robust.Shared.GameStates;

namespace Content.Shared._Mono.BlackFlash;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackFlashEffectComponent : Component
{
    [DataField]
    public float Scale = 1.1f;

    [DataField]
    public float Intensity = 1f;

    [DataField]
    public float Duration = 0.75f;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public float Seed;
}
