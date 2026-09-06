using Content.Server.GameTicking.Rules;
using Content.Server._Mono.Company;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Server._Mono.AlertLevel;
using Content.Shared._Mono.Company;
using Content.Server.GameTicking;
using Robust.Shared.Prototypes;
using Content.Server.Radio.EntitySystems;

namespace Content.Server._Mono.WarDeclarator;

public sealed partial class ManualPortstrikeRuleSystem : GameRuleSystem<ManualPortstrikeRuleComponent>
{
    [Dependency] private CompanyManager _companyManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private WarLevelSystem _warLevelSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionWarDeclaratorComponent, UseInHandEvent>(OnWarDeclaratorUsed);
    }

    private void OnWarDeclaratorUsed(Entity<FactionWarDeclaratorComponent> ent, ref UseInHandEvent args)
    {
        if (!TryComp<CompanyComponent>(args.User, out var userCompany) || ent.Comp.Faction != userCompany.CompanyName)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.WarDeclarationFailedMessage), ent, args.User);
            return;
        }

        var query = EntityQueryEnumerator<ManualPortstrikeRuleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.SectorStatus.TryGetValue(ent.Comp.Faction, out var warAlreadyDeclared) || warAlreadyDeclared)
                continue;

            comp.SectorStatus[ent.Comp.Faction] = true;

            var channel = _prototypeManager.Index(ent.Comp.Channel);
            _radio.SendRadioMessage(ent, Loc.GetString(ent.Comp.WarDeclarationMessage), channel, ent);

            var factionYetToDeclare = false;
            foreach (var factionDeclaredWar in comp.SectorStatus.Values)
            {
                if (!factionDeclaredWar)
                {
                    factionYetToDeclare = true;
                    break;
                }
            }
            if (!factionYetToDeclare)
                _warLevelSystem.SetLevel(comp.WarLevel);
        }
    }
}