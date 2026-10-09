using Robust.Shared.GameStates;

namespace Content.Shared._Mono.BlackFlash;

[RegisterComponent, NetworkedComponent]
public sealed partial class BlackFlashLastHitComponent : Component
{
    [DataField]
    public float CurrentDamageMultiplier = 2.5f;
}
