using Robust.Shared.Map;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun when it would like to take the specified amount of ammo.
/// </summary>
public class TakeAmmoEvent : EntityEventArgs // Mono: unseal
{
    public readonly EntityUid? User;
    public readonly int Shots;
    public List<(EntityUid? Entity, IShootable Shootable)> Ammo;

    /// <summary>
    /// If no ammo returned what is the reason for it?
    /// </summary>
    public string? Reason;

    /// <summary>
    /// Coordinates to spawn the ammo at.
    /// </summary>
    public EntityCoordinates Coordinates;

    // Frontier: better revolver reloading
    /// <summary>
    /// Does this event represent an intent to fire, or to safely remove ammo from an entity?
    /// </summary>
    public bool WillBeFired;
    // End Frontier

    /// <summary>
    /// Monolith
    /// If true, causes this event to only return the ammo that will be fired next without side effects beyond potentially spawning an entity.
    /// If an entity has been spawned, it is guaranteed it will not be in a container.
    /// </summary>
    public bool CheckOnly = false;

    public TakeAmmoEvent(int shots, List<(EntityUid? Entity, IShootable Shootable)> ammo, EntityCoordinates coordinates, EntityUid? user, bool willBeFired = false, bool checkOnly = false) // Frontier: add willBeFired
    {
        Shots = shots;
        Ammo = ammo;
        Coordinates = coordinates;
        User = user;
        CheckOnly = checkOnly; // Mono
        WillBeFired = willBeFired = checkOnly; // Frontier // Mono
    }
}
