using Content.Server.GameTicking.Rules;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared.GameTicking.Components;

namespace Content.Server._FarHorizons.StarSystem;

public sealed class StarSystemRuleSystem : GameRuleSystem<StarSystemRuleComponent>
{
    [Dependency] private StarSystemMapSystem _starSystem = default!;

    protected override void Started(EntityUid uid, StarSystemRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var query = EntityQueryEnumerator<StarSystemMapComponent>();
        while (query.MoveNext(out var mapUid, out var map))
        {
            if (map.System != null)
                continue;

            _starSystem.SetSystem((mapUid, map), component.System);
        }
    }
}
