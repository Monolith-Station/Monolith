using System.Linq;
using Content.Server._CE.ZCollapse;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Containers.ItemSlots;
using Robust.Server.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.MassFabricator;

public sealed partial class MassFabricatorSystem : Content.Shared._Mono.MassFabricator.MassFabricatorSystem
{
    [Dependency] private CEZCollapseSystem _collapse = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    private static readonly string[] ChipSlots = ["chip1", "chip2", "chip3"];

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(CEZCollapseSystem));
        SubscribeLocalEvent<PostGridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<MassFabricatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MassFabricatorComponent, EntInsertedIntoContainerMessage>(OnChipChanged);
        SubscribeLocalEvent<MassFabricatorComponent, EntRemovedFromContainerMessage>(OnChipChanged);
    }

    private void OnMapInit(Entity<MassFabricatorComponent> ent, ref MapInitEvent args) => RefreshChips(ent);

    private void OnChipChanged(Entity<MassFabricatorComponent> ent, ref EntInsertedIntoContainerMessage args) =>
        RefreshChips(ent);

    private void OnChipChanged(Entity<MassFabricatorComponent> ent, ref EntRemovedFromContainerMessage args) =>
        RefreshChips(ent);

    private void RefreshChips(Entity<MassFabricatorComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var added = new HashSet<Robust.Shared.Prototypes.ProtoId<MassFabricatorBlueprintPrototype>>();
        foreach (var slot in ChipSlots)
        {
            if (TryComp<MassFabricatorBlueprintChipComponent>(_slots.GetItemOrNull(ent, slot), out var chip))
                added.UnionWith(chip.Blueprints);
        }

        if (ent.Comp.ChipBlueprints.SetEquals(added))
            return;

        ent.Comp.ChipBlueprints = added;
        if (ent.Comp.SelectedBlueprint is { } selected && !ent.Comp.HasBlueprint(selected))
            TrySelectBlueprint(ent, null, null);
        Dirty(ent);
    }

    protected override bool? HasStructuralSupport(EntityUid grid, Vector2i cell) =>
        _collapse.CanSupportTile(grid, cell);

    public override void Update(float frameTime)
    {
        var byGrid = new Dictionary<EntityUid, List<Entity<MassFabricatorHologramComponent>>>();
        var plans = EntityQueryEnumerator<MassFabricatorHologramComponent, TransformComponent>();
        while (plans.MoveNext(out var uid, out var plan, out var xform))
        {
            if (plan.BuildGrid is not { } frame || plan.PrintState == MassFabricatorPrintState.Fading ||
                EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (!TryComp<MapGridComponent>(frame, out var grid) ||
                plan.SupportCell is { } support && (!_maps.TryGetTile(grid, support, out var tile) || tile.IsEmpty) ||
                OverlapsForeignGrid((frame, grid), plan.Cell))
            {
                ErasePlan(plan.Fabricator, (uid, plan));
                continue;
            }

            if (xform.ParentUid != frame)
                _transform.SetCoordinates(uid, _maps.GridTileToLocal(frame, grid, plan.Cell));

            if (!byGrid.TryGetValue(frame, out var onGrid))
                byGrid.Add(frame, onGrid = new());
            onGrid.Add((uid, plan));
        }

        foreach (var (frame, onGrid) in byGrid)
        {
            Entity<MapGridComponent> grid = (frame, Comp<MapGridComponent>(frame));
            if (!HasComp<MapComponent>(frame))
            {
                foreach (var group in onGrid.GroupBy(plan => plan.Comp.Fabricator))
                {
                    var connected = Reachable(group.Select(plan => plan.Comp.Cell), cell => HasPrintFooting(grid, cell));
                    foreach (var plan in group)
                        if (!connected.Contains(plan.Comp.Cell))
                            ErasePlan(plan.Comp.Fabricator, plan);
                }
                onGrid.RemoveAll(plan => EntityManager.IsQueuedForDeletion(plan.Owner));
            }

            if (onGrid.Count == 0)
                continue;

            if (HasComp<CEGridStabilityComponent>(frame) || onGrid.Any(plan => plan.Comp.Unsupported || plan.Comp.PrintBlocked))
            {
                var cells = onGrid.Select(plan => plan.Comp.Cell).ToHashSet();
                if (_collapse.GetConstructionSupport(frame, cells) is not { } support)
                    continue;

                foreach (var plan in onGrid)
                    UpdateStructuralSupport(plan, support.Printable.Contains(plan.Comp.Cell), support.Projected.Contains(plan.Comp.Cell));
            }

            foreach (var group in onGrid.GroupBy(plan => (plan.Comp.Fabricator, plan.Comp.Blueprint, plan.Comp.Target)))
            {
                var roots = group.Where(plan => !plan.Comp.Unsupported && !plan.Comp.PrintBlocked &&
                    HasPrintFooting(grid, plan.Comp.Cell)).Select(plan => plan.Comp.Cell).ToHashSet();
                var reachable = Reachable(group.Where(plan => !plan.Comp.Unsupported).Select(plan => plan.Comp.Cell),
                    roots.Contains);
                foreach (var plan in group)
                {
                    var blocked = !plan.Comp.Unsupported && !reachable.Contains(plan.Comp.Cell);
                    if (plan.Comp.NoWaveStart == blocked)
                        continue;

                    plan.Comp.NoWaveStart = blocked;
                    Dirty(plan);
                }
            }
        }

        base.Update(frameTime);
    }

    private static HashSet<Vector2i> Reachable(IEnumerable<Vector2i> cells, Func<Vector2i, bool> isRoot)
    {
        var pending = cells.ToHashSet();
        var reached = pending.Where(isRoot).ToHashSet();
        pending.ExceptWith(reached);
        var queue = new Queue<Vector2i>(reached);
        while (queue.TryDequeue(out var cell))
        {
            foreach (var offset in BuildNeighbors)
            {
                var next = cell + offset;
                if (pending.Remove(next))
                {
                    reached.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }

    private void OnGridSplit(ref PostGridSplitEvent args)
    {
        var grid = Comp<MapGridComponent>(args.Grid);
        var plans = EntityQueryEnumerator<MassFabricatorHologramComponent>();
        while (plans.MoveNext(out var uid, out var plan))
        {
            if (plan.BuildGrid != args.OldGrid ||
                !_maps.TryGetTile(grid, plan.SupportCell ?? plan.Cell, out var tile) || tile.IsEmpty)
                continue;

            plan.BuildGrid = args.Grid;
            _transform.SetCoordinates(uid, _maps.GridTileToLocal(args.Grid, grid, plan.Cell));
            Dirty(uid, plan);
        }
    }
}
