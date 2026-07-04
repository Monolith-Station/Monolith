using Robust.Shared.GameStates;

namespace Content.Shared._DV.Weapons.Ranged.Components;

/// <summary>
/// Added to weapons to allow bypass functionality of Content.Shared._DV.Weapons.Ranged.Components.PlayerAccuracyModifierComponent
/// Particularly useful for preventing Oni accuracy malus in crushers
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkipPlayerAccuracyModifierComponent : Component
{
}
