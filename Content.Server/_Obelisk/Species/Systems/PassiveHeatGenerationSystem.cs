using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Server._Obelisk.Species.Systems;

public sealed class PassiveHeatGenerationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<Species.Components.PassiveHeatGenerationComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<Species.Components.PassiveHeatGenerationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<Species.Components.PassiveHeatGenerationComponent, TemperatureComponent>();

        while (query.MoveNext(out var uid, out var passiveHeatComp, out var tempComp))
        {
            if (passiveHeatComp.NextUpdate > curTime)
                continue;

            var currentTemp = tempComp.CurrentTemperature;
            if (currentTemp > passiveHeatComp.MaximumTemperature || currentTemp < passiveHeatComp.MinimumTemperature)
                continue;

            var joules = passiveHeatComp.Joules;
            if (passiveHeatComp.MobStateModifier != null && TryComp<MobStateComponent>(uid, out var mobStateComp) )
            {
                var currentState = mobStateComp.CurrentState;
                if (passiveHeatComp.MobStateModifier.TryGetValue(currentState, out var modifier))
                    joules *= modifier;
            }

            _temperature.ChangeHeat(uid, joules, passiveHeatComp.IgnoreHeatResistance, tempComp);

            passiveHeatComp.NextUpdate += passiveHeatComp.UpdateInterval;
        }
    }
}
