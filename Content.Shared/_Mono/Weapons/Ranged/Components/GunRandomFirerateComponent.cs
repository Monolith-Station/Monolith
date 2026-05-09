using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Weapons.Ranged.Components;

/// <summary>
/// Applies a random multiplier to time until next shot every time this gun fires.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunRandomFirerateComponent : Component
{
    /// <summary>
    /// Whether to apply the multiplier as reload time.
    /// If true, we will roll for a multiplier for how long to reload.
    /// If false, we will roll for a multiplier of how fast we reload.
    /// </summary>
    [DataField]
    public bool AsTime = true;

    [DataField(required: true)]
    public float MinMul = 1f;

    [DataField(required: true)]
    public float MaxMul = 1f;
}

