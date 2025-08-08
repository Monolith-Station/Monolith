using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Localizations;
using Robust.Server.GameObjects;

namespace Content.Server.GameTicking.Rules;

public sealed partial class DragonRuleSystem : GameRuleSystem<DragonRuleComponent>
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DragonRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagEntitySelected);
    }

    private void AfterAntagEntitySelected(Entity<DragonRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _antag.SendBriefing(args.EntityUid, MakeBriefing(args.EntityUid), null, null);
    }

    private string MakeBriefing(EntityUid dragon)
    {
        var direction = string.Empty;

        var dragonXform = Transform(dragon);

        EntityUid? stationGrid = null;
        if (_station.GetStationInMap(dragonXform.MapID) is { } station)
            stationGrid = _station.GetLargestGrid(station);

        if (stationGrid is not null)
        {
            var stationPosition = _transform.GetWorldPosition((EntityUid)stationGrid);
            var dragonPosition = _transform.GetWorldPosition(dragon);

            var vectorToStation = stationPosition - dragonPosition;
            direction = ContentLocalizationManager.FormatDirection(vectorToStation.GetDir());
        }

        var briefing = Loc.GetString("dragon-role-briefing", ("direction", direction));

        return briefing;
    }
}
