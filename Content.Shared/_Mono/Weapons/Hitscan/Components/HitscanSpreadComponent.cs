using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Weapons.Hitscan.Components;

/// <summary>
/// Spawns a spread of the hitscans when fired
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedGunSystem))]
public sealed partial class HitscanSpreadComponent : Component
{
    /// <summary>
    /// Hitscan entity
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// How much the ammo spreads when shot, in degrees. Does nothing if count is 0.
    /// </summary>
    [DataField]
    public Angle Spread = Angle.FromDegrees(5);

    /// <summary>
    /// How many prototypes are spawned when shot.
    /// </summary>
    [DataField]
    public int Count = 1;
}
