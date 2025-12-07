using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Handles the Vigor trait effects on stamina.
/// </summary>
public sealed class VigorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VigorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VigorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StaminaComponent, ComponentInit>(OnStaminaInit);
    }

    private void OnStartup(Entity<VigorComponent> ent, ref ComponentStartup args)
    {
        ApplyVigorEffects(ent);
    }

    private void OnStaminaInit(EntityUid uid, StaminaComponent component, ComponentInit args)
    {
        if (HasComp<VigorComponent>(uid))
        {
            ApplyVigorEffects((uid, Comp<VigorComponent>(uid)));
        }
    }

    private void OnShutdown(Entity<VigorComponent> ent, ref ComponentShutdown args)
    {
        RemoveVigorEffects(ent);
    }

    private void ApplyVigorEffects(Entity<VigorComponent> ent)
    {
        if (!TryComp(ent, out StaminaComponent? stamina))
        {
            return;
        }

        stamina.CritThreshold += ent.Comp.StaminaBonus;
        stamina.Decay += ent.Comp.RegenerationBonus;
        stamina.Cooldown *= ent.Comp.CooldownReduction;

        stamina.NextUpdate = _timing.CurTime;
        Dirty(ent, stamina);
    }

    private void RemoveVigorEffects(Entity<VigorComponent> ent)
    {
        if (!TryComp(ent, out StaminaComponent? stamina))
            return;

        stamina.CritThreshold -= ent.Comp.StaminaBonus;
        stamina.Decay -= ent.Comp.RegenerationBonus;
        stamina.Cooldown /= ent.Comp.CooldownReduction;
        stamina.NextUpdate = _timing.CurTime;
        Dirty(ent, stamina);
    }
}

