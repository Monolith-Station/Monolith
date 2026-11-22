using Content.Shared._Mono.Caelestinus.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.Caelestinus.Systems;

public sealed partial class ReverseEngineeringSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReverseEngineerableComponent, GetVerbsEvent<ExamineVerb>>(OnEngineerableDetailedExamine);
        SubscribeLocalEvent<ReverseEngineeringStationComponent, GetVerbsEvent<ExamineVerb>>(OnStationDetailedExamine);
        SubscribeLocalEvent<ReverseEngineeringStationComponent, InteractUsingEvent>(OnItemInteract);
    }

    private void OnEngineerableDetailedExamine(Entity<ReverseEngineerableComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!_proto.TryIndex(ent.Comp.Category, out var categoryProto))
            return;

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString(categoryProto.ExamineText, ("category", categoryProto.Name), ("points", ent.Comp.ProgressValue)));

        _examine.AddDetailedExamineVerb(args, ent.Comp, message, Loc.GetString("reverse-engineerable-verb-examine"), "/Textures/Interface/VerbIcons/screwdriver.png", Loc.GetString("reverse-engineerable-verb-examine"));
    }

    private void OnStationDetailedExamine(Entity<ReverseEngineeringStationComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        var message = new FormattedMessage();

        foreach (var kvp in ent.Comp.ProgressTree)
        {
            if (!_proto.TryIndex(kvp.Key, out var categoryProto))
                continue;
            if (!ent.Comp.Progress.ContainsKey(kvp.Key))
                ent.Comp.Progress[kvp.Key] = 0;
            var (nextOutputKey, nextOutputValue) = GetNextOutput(kvp.Value, ent.Comp.Progress[kvp.Key]);
            if (nextOutputValue is null
                || !_proto.TryIndex(nextOutputValue, out var outputEntProto))
                continue;

            var needed = nextOutputKey - ent.Comp.Progress[kvp.Key];
            message.AddMarkupOrThrow(Loc.GetString("reverse-engineering-station-status-line", ("number", needed), ("name", categoryProto.Name), ("output", outputEntProto.Name)));
            message.PushNewline();
        }

        var (nextOutputKeyTotal, nextOutputValueTotal) = GetNextOutput(ent.Comp.TotalProgressTree, ent.Comp.TotalProgress);
        if (nextOutputValueTotal != null
            && _proto.TryIndex(nextOutputValueTotal, out var outputTotalEntProto))
        {
            var neededTotal = nextOutputKeyTotal - ent.Comp.TotalProgress;
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("reverse-engineering-station-status-line-total", ("number", neededTotal), ("output", outputTotalEntProto.Name)));
        }

        _examine.AddDetailedExamineVerb(args, ent.Comp, message, Loc.GetString("reverse-engineerable-verb-examine"), "/Textures/Interface/VerbIcons/screwdriver.png", Loc.GetString("reverse-engineerable-verb-examine"));
    }

    private (int, EntProtoId?) GetNextOutput(Dictionary<int, EntProtoId> tree, int progress)
    {
        foreach (var output in tree)
        {
            if (output.Key > progress)
                return (output.Key, output.Value);
        }

        return (0, null);
    }


    public void HandleReverseEngineerableByCompany(EntityUid ent, EntityUid? actor)
    {
        if (!TryComp<ReverseEngineerableComponent>(ent, out var reverseEngineerableComp)
            || reverseEngineerableComp.OriginCompany is null)
            return;

        if (!TryComp<CompanyComponent>(actor, out var company)
            || reverseEngineerableComp.OriginCompany != company.CompanyName)
            RemComp<ReverseEngineerableComponent>(ent);
    }

    private void OnItemInteract(Entity<ReverseEngineeringStationComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<ReverseEngineerableComponent>(args.Used, out var reverseEngineerableComp)
            || !ent.Comp.ProgressTree.ContainsKey(reverseEngineerableComp.Category))
            return;

        if (!ent.Comp.Progress.ContainsKey(reverseEngineerableComp.Category))
            ent.Comp.Progress[reverseEngineerableComp.Category] = 0;

        var (nextUnlockReq, nextUnlock) = GetNextOutput(ent.Comp.ProgressTree[reverseEngineerableComp.Category], ent.Comp.Progress[reverseEngineerableComp.Category]);
        var (nextTotalUnlockReq, nextTotalUnlock) = GetNextOutput(ent.Comp.TotalProgressTree, ent.Comp.TotalProgress);

        ent.Comp.Progress[reverseEngineerableComp.Category] += reverseEngineerableComp.Value;
        ent.Comp.TotalProgress += reverseEngineerableComp.ProgressValue;

        var skipSpawningProgress = nextUnlock is null;
        var skipTotalProgress = nextTotalUnlock is null;

        var spawnedEntity = false; // prevent showing two popups at once ALL the time.
        if (nextUnlockReq <= ent.Comp.Progress[reverseEngineerableComp.Category] && !skipSpawningProgress)
        {
            var item = PredictedSpawnAtPosition(nextUnlock, Transform(ent).Coordinates);
            _popup.PopupPredicted(Loc.GetString("reverse-engineering-station-output", ("machine", Name(ent)), ("item", Name(item))), ent, args.User, PopupType.Large);
            spawnedEntity = true;
        }

        if (nextTotalUnlockReq <= ent.Comp.TotalProgress && !skipTotalProgress)
        {
            var item = PredictedSpawnAtPosition(nextTotalUnlock, Transform(ent).Coordinates);
            _popup.PopupPredicted(Loc.GetString("reverse-engineering-station-output", ("machine", Name(ent)), ("item", Name(item))), ent, args.User, PopupType.Large);
            spawnedEntity = true;
        }

        if (!spawnedEntity)
            _popup.PopupPredicted(Loc.GetString("entity-storage-popup-insert", ("user", Identity.Entity(args.User, EntityManager)), ("blueprint", Name(args.Used)), ("receiver", Name(ent))), ent, args.User);
        PredictedQueueDel(args.Used);

        Dirty(ent);
        args.Handled = true;
    }
}