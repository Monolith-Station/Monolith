// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Mono.AmmoLoader;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._Lua.AmmoLoader;

public static class AmmoLoaderCapacity
{
    public static int GetStoredAmmoUnitCount(IEntityManager entMan, AmmoLoaderComponent component)
    {
        var count = 0;
        foreach (var contained in component.Container.ContainedEntities) count += GetStoredAmmoUnitCount(entMan, contained);
        return count;
    }

    public static int GetStoredAmmoUnitCount(IEntityManager entMan, EntityUid entity)
    {
        if (entMan.TryGetComponent(entity, out BallisticAmmoProviderComponent? ballistic))
        {
            if (ballistic.InfiniteUnspawned) return Math.Max(1, ballistic.Capacity);
            return ballistic.Count;
        }
        return 1;
    }

    public static bool IsEmptyAmmoContainer(IEntityManager entMan, EntityUid entity)
    { return entMan.TryGetComponent(entity, out BallisticAmmoProviderComponent? ballistic) && !ballistic.InfiniteUnspawned && ballistic.Count <= 0; }
}
