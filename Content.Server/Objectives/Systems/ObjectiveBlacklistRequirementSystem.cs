using Content.Server.Objectives.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Objectives.Components;
using Content.Shared.Whitelist;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles applying the objective component blacklist to the objective entity.
/// </summary>
public sealed partial class ObjectiveBlacklistRequirementSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveBlacklistRequirementComponent, RequirementCheckEvent>(OnCheck);
    }

    private void OnCheck(EntityUid uid, ObjectiveBlacklistRequirementComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var objective in args.Mind.Objectives)
        {
            if (_whitelistSystem.IsBlacklistPass(comp.Blacklist, objective))
            {
                args.Cancelled = true;
                return;
            }

            // Mono
            if (TryComp<CompanyComponent>(objective, out var userCompany) && comp.BlacklistedCompanies.Contains(userCompany.CompanyName))
            {
                args.Cancelled = true;
                return;
            }
        }
    }
}
