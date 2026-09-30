/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Actions;
using Content.Shared._CE.ZLevels.Flight;
using Content.Shared._CE.ZLevels.Flight.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Flight;

public sealed partial class CEZFlightSystem : CESharedZFlightSystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan DrainInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEControllableFlightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEControllableFlightComponent, ComponentRemove>(OnRemove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CEZFlyerComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var flyer, out var stamina))
        {
            if (!flyer.Active || flyer.HoverStaminaDrain <= 0f)
                continue;

            if (now < flyer.NextStaminaDrain)
                continue;

            flyer.NextStaminaDrain = now + DrainInterval;
            var drain = flyer.HoverStaminaDrain * GetMassFactor((uid, flyer));
            _stamina.TakeStaminaDamage(uid, drain, stamina, visual: false);
        }
    }

    private void OnRemove(Entity<CEControllableFlightComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelToggleActionEntity);
    }

    private void OnMapInit(Entity<CEControllableFlightComponent> ent, ref MapInitEvent args)
    {
        if (!ZPhyzQuery.TryComp(ent, out var zPhys))
            return;

        if (!TryComp<CEZFlyerComponent>(ent.Owner, out var flyerComp))
            return;

        SetTargetHeight(ent.Owner, zPhys.CurrentZLevel);

        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelToggleActionEntity, ent.Comp.ToggleActionProto);
    }
}
