using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Server.Station.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Random.Helpers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DV.Antag;

public sealed class NukieOperationSystem : GameRuleSystem<NukieOperationComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerCompanyCompSpawned);
    }

    private void OnPlayerCompanyCompSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!_mind.TryGetMind(args.Player, out var mindId, out var mind))
            return;

        if (!TryComp<CompanyComponent>(args.Mob, out var userCompany))
            return;

        var query = QueryActiveRules();
        var rules = new List<(EntityUid, NukieOperationComponent)>();
        while (query.MoveNext(out var uid, out _, out var operation, out _))
        {
            rules.Add((uid, operation));
        }
        foreach (var (uid, operation) in rules)
        {
            if (operation.ChosenOperation == null)
            {
                if (!_proto.TryIndex(operation.Operations, out var opProto))
                    return;

                operation.ChosenOperation = _random.Pick(opProto.Weights);
            }

            if (!_proto.TryIndex(operation.ChosenOperation, out var chosenOp))
                return;

            foreach (var objectiveProto in chosenOp.OperationObjectives)
            {
                if (operation.ParticipatingCompany != userCompany.CompanyName)
                    return;
                if (!_objectives.TryCreateObjective((mindId, mind), objectiveProto, out var objective))
                {
                    Log.Error("Couldn't create objective for company member: " + mindId); // This should never happen.
                    continue;
                }

                _mind.AddObjective(mindId, mind, objective.Value);
                Log.Info("Adding objective " + objective +  " to mindId " + mindId);
            }
        }
    }
}
