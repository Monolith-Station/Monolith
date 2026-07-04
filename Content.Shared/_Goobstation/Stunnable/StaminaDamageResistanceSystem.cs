using Content.Shared.Armor;
using Content.Shared.Damage.Events;
using Content.Shared.Inventory;

namespace Content.Shared.Stunnable;

public sealed partial class StaminaDamageResistanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaminaDamageResistanceComponent, InventoryRelayedEvent<TakeStaminaDamageEvent>>(OnStaminaMeleeHit);
        SubscribeLocalEvent<StaminaDamageResistanceComponent, ArmorExamineEvent>(OnExamine);
    }

    private void OnStaminaMeleeHit(Entity<StaminaDamageResistanceComponent> ent, ref InventoryRelayedEvent<TakeStaminaDamageEvent> args)
    {
        args.Args.Multiplier *= ent.Comp.Coefficient;
    }
    private void OnExamine(Entity<StaminaDamageResistanceComponent> ent, ref ArmorExamineEvent args)
    {

        args.Msg.PushNewline();
        // Mono - fix floating point error stuff guh
        args.Msg.AddMarkupOrThrow(Loc.GetString("armor-examine-stamina",
            ("num", MathF.Round((1f - ent.Comp.Coefficient) * 100, 1)))); // behold my line of 4 )
    }
}
