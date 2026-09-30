using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities that have this component will do the damage specified to hit targets (Who didn't reflect it).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanBasicDamageComponent : Component
{
    /// <summary>
    /// How much damage the hitscan weapon will do when hitting a target.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage;

    // Mono start
    /// <summary>
    ///     How much of the target's armor to ignore. 0.5 means the damage is affected half as much by armor, I think.
    /// </summary>
    [DataField]
    public float ArmorPenetration;

    /// <summary>
    ///     Ignore all damage resistances the target has.
    /// </summary>
    [DataField]
    public bool IgnoreResistances = false;
    // Mono end
}
