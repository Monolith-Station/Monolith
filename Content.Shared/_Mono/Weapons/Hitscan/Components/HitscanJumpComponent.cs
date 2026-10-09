using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Weapons.Hitscan.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanJumpComponent : Component
{
    /// <summary>
    /// How much times do we jump from target-to-target?
    /// </summary>
    [DataField]
    public int Count = 3;

    [DataField]
    public float Range = 10;

    /// <summary>
    /// Entities that were already hit by hitscan (Or fired it)
    /// </summary>
    [DataField]
    public HashSet<EntityUid> IgnoredEntities = [];
};
