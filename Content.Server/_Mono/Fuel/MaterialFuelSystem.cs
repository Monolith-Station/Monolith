using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Fuel;

public sealed partial class MaterialFuelSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;

    private float _updateInterval = 1f;
    private float _updateAccumulator = 0f;

    public override void Update(float frameTime)
    {
        _updateAccumulator += frameTime;
        if (_updateAccumulator < _updateInterval)
            return;
        _updateAccumulator -= _updateInterval;


        var eqe = EntityQueryEnumerator<MaterialFuelComponent, FlammableComponent, PhysicalCompositionComponent>();
        while (eqe.MoveNext(out var uid, out var fuel, out var flame, out var composition))
        {
            if (!flame.OnFire)
                return;

            Burn(uid, fuel, composition);
        }
    }

    private void Burn(EntityUid uid, MaterialFuelComponent fuel, PhysicalCompositionComponent composition)
    {
        foreach (var pair in composition.MaterialComposition)
        {
            if (fuel.BaseMaterialComposition == 0)
                fuel.BaseMaterialComposition = pair.Value;

            composition.MaterialComposition[pair.Key] -= fuel.ConsumptionRate;

            if (composition.MaterialComposition[pair.Key] > 0)
                continue;

            if (!TryComp<StackComponent>(uid, out var stack))
            {
                QueueDel(uid);
                break;
            }

            _stackSystem.SetCount(uid, stack.Count-1);
            composition.MaterialComposition[pair.Key] += fuel.BaseMaterialComposition;
        }
    }
}
