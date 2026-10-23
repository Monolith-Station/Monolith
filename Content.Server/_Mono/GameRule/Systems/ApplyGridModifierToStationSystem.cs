using Content.Server._Mono.GameRule.Components;
using Content.Server._Mono.Grid;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;

namespace Content.Server_Mono.GameRule.Systems;

public sealed partial class ApplyGridModifierToStationSystem : GameRuleSystem<ApplyGridModifierToStationComponent>
{
    [Dependency] private GridModifierSystem _gridModifier = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGenerated);
    }

    private void OnStationGenerated(StationGridAddedEvent args)
    {
        if (!TryComp<BecomesStationComponent>(args.GridId, out var stationComp))
            return;
        var query = EntityQueryEnumerator<ApplyGridModifierToStationComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            foreach (var station in comp.Modifiers.Keys)
            {
                if (station != stationComp.Id)
                    continue;
                _gridModifier.ModifyGrid(args.GridId, comp.Modifiers[station]);
            }
        }
    }
}