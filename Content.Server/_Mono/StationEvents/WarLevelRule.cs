using Content.Server._Mono.AlertLevel;
using Content.Server.StationEvents.Events;
﻿using Content.Shared.GameTicking.Components;

namespace Content.Server._Mono.StationEvents;

public sealed partial class WarLevelRule : StationEventSystem<WarLevelRuleComponent>
{
    [Dependency] private WarLevelSystem _warLevelSystem = default!;

    protected override void Started(EntityUid uid, WarLevelRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _warLevelSystem.SetLevel(component.WarLevel);
    }
}
