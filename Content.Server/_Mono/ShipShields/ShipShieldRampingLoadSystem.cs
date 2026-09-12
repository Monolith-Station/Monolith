using Content.Server._Mono.ShipShields.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Crescent.ShipShields;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.ShipShields;

public sealed partial class ShipShieldRampingLoadSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipShieldRampingLoadComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ShipShieldRampingLoadComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.MultiplicationInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ShipShieldRampingLoadComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp<ShipShieldEmitterComponent>(uid, out var shieldComp))
                continue;
            if (comp.NextUpdate > curTime)
                continue;

            shieldComp.BaseDraw *= comp.Multiplier;
            shieldComp.MaxDraw *= comp.Multiplier;

            var channel = _prototypeManager.Index(comp.RadioChannel);
            _radio.SendRadioMessage(uid, Loc.GetString(comp.Message), channel, uid);

            comp.NextUpdate += comp.MultiplicationInterval;
        }
    }
}