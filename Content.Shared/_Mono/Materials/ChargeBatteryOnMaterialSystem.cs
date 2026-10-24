using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Materials;

public sealed partial class ChargeBatteryOnMaterialSystem : EntitySystem
{
    [Dependency] private SharedStackSystem _sharedStackSystem = default!;
    [Dependency] private SharedBatterySystem _sharedBatterySystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeBatteryOnMaterialComponent, InteractUsingEvent>(OnInteract);
    }

    private void OnInteract(EntityUid uid, ChangeBatteryOnMaterialComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PhysicalCompositionComponent>(args.Used, out var composition) ||
            !TryComp<BatteryComponent>(uid, out var battery))
            return;

        foreach (var mat in component.ChargingMaterials)
        {
            TryCharge((uid, battery), (args.Used, composition), mat.Key, mat.Value);
        }

        args.Handled = true;
    }

    private bool TryCharge(Entity<BatteryComponent> toCharge,
        Entity<PhysicalCompositionComponent> chargingWith,
        ProtoId<MaterialPrototype> mat,
        float change)
    {
        var availableVolume = toCharge.Comp.MaxCharge - toCharge.Comp.CurrentCharge;
        float toAdd;

        if (availableVolume == 0)
            return false;

        if (TryComp<StackComponent>(chargingWith, out var stack))
        {
            var multiplier = (int) Math.Clamp(stack.Count, 0, MathF.Ceiling(availableVolume / (change * chargingWith.Comp.MaterialComposition[mat])));
            toAdd = multiplier * change * chargingWith.Comp.MaterialComposition[mat];

            _sharedStackSystem.SetCount(chargingWith, stack.Count-multiplier);
        }
        else
        {
            toAdd = Math.Clamp(chargingWith.Comp.MaterialComposition[mat]*change, 0, availableVolume);
            chargingWith.Comp.MaterialComposition[mat] -= (int)MathF.Ceiling(availableVolume / change);

            if (chargingWith.Comp.MaterialComposition[mat] <= 0)
                QueueDel(chargingWith);
        }

        _sharedBatterySystem.ChangeCharge(toCharge, toAdd);

        return true;
    }
}
