using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Handles the Lethargy trait effects on stamina.
/// </summary>
public sealed class LethargySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LethargyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LethargyComponent, ComponentShutdown>(OnShutdown);
        //SubscribeLocalEvent<StaminaComponent, ComponentInit>(OnStaminaInit);
    }

    private void OnStartup(Entity<LethargyComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnStaminaInit(EntityUid uid, StaminaComponent component, ComponentInit args)
    {
        if (HasComp<LethargyComponent>(uid))
            Apply((uid, Comp<LethargyComponent>(uid)));
    }

    private void OnShutdown(Entity<LethargyComponent> ent, ref ComponentShutdown args)
    {
        Remove(ent);
    }

    private void Apply(Entity<LethargyComponent> ent)
    {
        if (!TryComp(ent, out StaminaComponent? stamina))
            return;

        stamina.CritThreshold -= ent.Comp.StaminaPenalty;
        stamina.Decay -= ent.Comp.RegenerationPenalty;
        stamina.Cooldown *= ent.Comp.CooldownIncrease;
        stamina.NextUpdate = _timing.CurTime;
        Dirty(ent, stamina);
    }

    private void Remove(Entity<LethargyComponent> ent)
    {
        if (!TryComp(ent, out StaminaComponent? stamina))
            return;

        stamina.CritThreshold += ent.Comp.StaminaPenalty;
        stamina.Decay += ent.Comp.RegenerationPenalty;
        stamina.Cooldown /= ent.Comp.CooldownIncrease;
        stamina.NextUpdate = _timing.CurTime;
        Dirty(ent, stamina);
    }
}


