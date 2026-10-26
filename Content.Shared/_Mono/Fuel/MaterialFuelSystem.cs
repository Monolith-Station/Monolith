using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Shared.Materials;
using Content.Shared.Stacks;

namespace Content.Shared._Mono.Fuel;

/// <summary>
/// Not compatible with multiple material compositions.
/// Prefer to use without stacks when possible.
/// </summary>
public sealed partial class MaterialFuelSystem : EntitySystem
{
    [Dependency] private  SharedStackSystem _stackSystem = default!;

    private float _updateInterval = 1f;
    private float _updateAccumulator;

    public override void Initialize()
    {
        SubscribeLocalEvent<MaterialFuelComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(EntityUid uid, MaterialFuelComponent component, MapInitEvent args)
    {
        if (!TryComp<PhysicalCompositionComponent>(uid, out var composition))
            return;

        component.Material ??= composition.MaterialComposition.First().Key;
        component.BaseMaterialComposition = composition.MaterialComposition[component.Material];
    }

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
                continue;

            Burn(uid, fuel, composition);
        }
    }

    private void Burn(EntityUid uid, MaterialFuelComponent fuel, PhysicalCompositionComponent composition)
    {
        string key = fuel.Material ?? composition.MaterialComposition.First().Key;

        composition.MaterialComposition[key] -= fuel.ConsumptionRate;

        if (composition.MaterialComposition[key] > 0)
            return;

        if (!TryComp<StackComponent>(uid, out var stack))
        {
            QueueDel(uid);
            return;
        }

        _stackSystem.SetCount(uid, stack.Count-1);
        composition.MaterialComposition[key] += fuel.BaseMaterialComposition;
    }

    public int CalculateComposition(EntityUid uid,
        MaterialFuelComponent? fuel,
        string mat,
        int multiplier,
        int volume)
    {
        if (!Resolve(uid, ref fuel) ||
            fuel.Material != mat)
            return multiplier * volume;

        return volume + (multiplier - 1) * fuel.BaseMaterialComposition;
    }
}
