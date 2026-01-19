// Mono - whole file
using Robust.Shared.Prototypes; // Mono

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun when it would like to check the prototype of the next shot ammo.
/// </summary>
[ByRefEvent]
public record struct CheckShootPrototypeEvent(EntityPrototype? ShootPrototype = null);
