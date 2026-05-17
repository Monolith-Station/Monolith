using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    /// <summary>
    /// Adds an ammo entity to a BallisticAmmoProvider (Mono - entire method)
    /// </summary>
    public void AddBallisticAmmo(Entity<BallisticAmmoProviderComponent?> ent, EntityUid ammoEntity)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;
        ent.Comp.Entities.Add(ammoEntity);
        DirtyField(ent, ent.Comp, nameof(BallisticAmmoProviderComponent.Entities));
    }

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component.Entities.Count > 0)
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.Entities));

            Containers.Remove(existing, component.Container);
			ent = existing; //Mono: Sound bugfix
            EnsureShootable(existing);
        }
        else if (component.UnspawnedCount > 0 && !component.InfiniteUnspawned) // Mono - no ammo generator
        {
            component.UnspawnedCount--;
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.UnspawnedCount));
            ent = Spawn(component.Proto, coordinates);
            EnsureShootable(ent.Value);
        }

        if (ent != null)
            EjectCartridge(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }
}
