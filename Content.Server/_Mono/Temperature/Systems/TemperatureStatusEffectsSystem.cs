using System.Linq;
using Content.Server._Mono.Temperature.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;

namespace Content.Server._Mono.Temperature.Systems;

public sealed class TemperatureStatusEffectsSystem : EntitySystem
{
    private float _updateCooldown = 1f;
    private TimeSpan _updateTimer = TimeSpan.Zero;

    [Dependency] private readonly StatusEffectsSystem _effects = default!;
    [Dependency] private readonly MobStateSystem _state = default!;

    public override void Update(float frameTime)
    {
        if (_updateTimer < TimeSpan.FromSeconds(_updateCooldown))
        {
            _updateTimer += TimeSpan.FromSeconds(frameTime);
            return;
        }

        var ents = EntityQueryEnumerator<TemperatureStatusEffectsComponent, TemperatureComponent>();

        while (ents.MoveNext(out var uid, out var comp, out var temperature))
        {
            if (!_state.IsAlive(uid))
                continue;

            var t = temperature.CurrentTemperature;
            var args = new EntityEffectBaseArgs(uid, EntityManager);

            foreach (var tEff in comp.TemperatureEffects)
            {
                if (tEff.MaximumTemperature < t ||
                    tEff.MinimumTemperature > t)
                    continue;

                foreach (var effect in tEff.Effects)
                {
                    effect.Effect(args);
                }
            }
        }

        _updateTimer = TimeSpan.Zero;
    }
}
