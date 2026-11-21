using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Clothing;

/// <summary>
/// Mono: Whether we should hide our HUD from other players WITHOUT using the stealth component
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HudScramblerComponent : Component
{
}
