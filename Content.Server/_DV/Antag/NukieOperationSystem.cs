using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DV.Antag;

public sealed partial class NukieOperationSystem : GameRuleSystem<NukieOperationComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerCompanyCompSpawned);
    }

    protected override void Started(EntityUid uid, NukieOperationComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        if (component.ChosenOperation == null)
        {
            if (!_proto.TryIndex(component.Operations, out var opProto))
                return;

            component.ChosenOperation = _random.Pick(opProto.Weights);
        }

        if (!_proto.TryIndex(component.ChosenOperation, out var chosenOp))
            return;

        foreach (var objectiveProto in chosenOp.OperationObjectives)
        {
            var objectiveData = TryCreateObjective(uid, component, objectiveProto);
            if (objectiveData == null)
            {
                Log.Error("Failed to create objective for operation " + uid.Id);
                continue;
            }

            var ev = new ObjectiveAssignedEvent(uid);
            RaiseLocalEvent(objectiveData.Value.Objective, ref ev);
            if (ev.Cancelled)
            {
                Del(uid);
                Log.Warning($"Could not assign objective {uid}, deleted it");
                break;
            }
            component.Objectives.Add(objectiveData.Value.Objective);
            var afterEv = new ObjectiveAfterAssignEvent(uid, objectiveData.Value.Component, MetaData(objectiveData.Value.Objective));
            RaiseLocalEvent(objectiveData.Value.Objective, ref afterEv);
        }
    }
    public OperationObjectiveData? TryCreateObjective(EntityUid operationUid, NukieOperationComponent component, string proto)
    {
        if (!_proto.HasIndex<EntityPrototype>(proto))
            return null;

        var uid = Spawn(proto);
        if (!TryComp<ObjectiveComponent>(uid, out var comp))
        {
            Del(uid);
            Log.Error($"Invalid objective prototype {proto}, missing ObjectiveComponent");
            return null;
        }
        // Assigning objectives is done on player spawn, not here!
        Log.Debug($"Created objective {ToPrettyString(uid):objective}");
        return new OperationObjectiveData(uid, comp);
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
            foreach (var objective in operation.Objectives)
            {
                if (operation.ParticipatingCompany != userCompany.CompanyName || !TryComp<ObjectiveComponent>(objective, out var objectiveComp))
                    break;

                if (!_objectives.CanBeAssigned(uid, mindId, mind, objectiveComp))
                {
                    Log.Warning($"Objective {uid} did not match the requirements for {_mind.MindOwnerLoggingString(mind)}, deleted it");
                    break;
                }

                _mind.AddObjective(mindId, mind, objective);
                Log.Info("Adding objective " + objective +  " to mindId " + mindId);
            }
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        NukieOperationComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        if (_proto.TryIndex(component.ChosenOperation, out var opProto) &&
            _proto.TryIndex(component.ParticipatingCompany, out var company))
        {
            var compMemberQuery = EntityQueryEnumerator<MindContainerComponent, CompanyComponent>();
            var compMembers = new List<Entity<MindContainerComponent, CompanyComponent>>();
            while (compMemberQuery.MoveNext(out var eligibleUid, out var mindComp, out var compComp))
            {
                if (compComp.CompanyName == company)
                    compMembers.Add((eligibleUid, mindComp, compComp));

            }

            var startText = Loc.GetString("fac-operation-start", ("company", company.Name), ("color", company.Color));
            args.AddLine(startText);
            args.AddLine(Loc.GetString("fac-operation-members-list-start"));

            foreach (var member in compMembers)
            {
                args.AddLine(Loc.GetString("fac-operation-members-list-name",("name", Name(member))));
            }

            args.AddLine(Loc.GetString("fac-operation-objectives-list-start"));
            foreach(var objective in component.Objectives)
            {
                    var info = _objectives.GetInfo(objective, uid);
                    if (info != null)
                    {
                        args.AddLine(Loc.GetString("fac-operation-objectives-list-entry",
                            ("name", info.Value.Title),
                            ("progress", (int)(info.Value.Progress * 100))
                        ));
                    }
            }
        }
    }

    [Serializable]
    public record struct OperationObjectiveData(EntityUid Objective, ObjectiveComponent Component);
}
