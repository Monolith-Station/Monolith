using System.Linq;
using Content.Client.Clickable;
using Content.Client.Gameplay;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Maps;
using Robust.Client.Placement;
using Robust.Client.Input;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.GameStates;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorClientSystem : MassFabricatorSystem
{
    [Dependency] private IStateManager _states = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private IEyeManager _eyes = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IMapManager _maps = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private ClickableSystem _clickable = default!;

    private MassFabricatorViewComponent? View =>
        _players.LocalEntity is { } user && TryComp<MassFabricatorViewComponent>(user, out var view) ? view : null;

    public IReadOnlyList<EntityCoordinates>? WaveRoute { get; private set; }

    public bool Returning => View?.Returning ?? false;
    private EntityUid? _fabricator;
    public MassFabricatorEraseMode EraseMode { get; private set; }

    private bool _placedThisPress;
    private readonly HashSet<NetCoordinates> _placedCoordinates = new();
    private readonly HashSet<EntityCoordinates> _stagedTiles = new();
    [Dependency] private MassFabricatorRejectionSystem _rejections = default!;
    private readonly Queue<EntityEventArgs> _queuedRequests = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new MassFabricatorEyeOverlay());
    }

    public override void Shutdown()
    {
        _queuedRequests.Clear();
        _overlays.RemoveOverlay<MassFabricatorEyeOverlay>();
        base.Shutdown();
    }

    protected override void OnPlanCreated(Entity<MassFabricatorHologramComponent> plan) =>
        EntityManager.System<MassFabricatorHologramSystem>().ShowPlan(plan.Owner);

    protected override void OnPlacementRejected(EntityCoordinates coordinates, Angle rotation)
    {
        if (_timing.IsFirstTimePredicted)
            _rejections.Add(coordinates, rotation);
    }

    public EntityUid? ActiveFabricatorUid => View?.Fabricator;

    public EntityCoordinates GetBuildCursor(ScreenCoordinates mouse, bool lookUp = true)
    {
        if (!mouse.IsValid)
            return default;

        if (lookUp && _states.CurrentState is MassFabricatorState editor &&
            _ui.CurrentlyHovered == editor.Viewport.Viewport &&
            _players.LocalEntity is { } user &&
            editor.Viewport.Viewport.TryGetLookUpAim(mouse.Position, Transform(user).MapID, out var upper) &&
            _mapSystem.TryGetMap(upper.MapId, out var upperMap) && CanTargetBuildLayer(user, upperMap))
            return _transform.ToCoordinates(upperMap.Value, upper);

        var point = _eyes.PixelToMap(mouse);
        if (!_mapSystem.TryGetMap(point.MapId, out var currentMap) ||
            !float.IsFinite(point.Position.X) || !float.IsFinite(point.Position.Y))
            return default;

        var anchor = _maps.TryFindGridAt(point, out var grid, out _) ? grid : currentMap.Value;
        return _transform.ToCoordinates(anchor, point);
    }

    public (string Key, bool Blocked)? GetPlanDenialTooltipAt(ScreenCoordinates mouse)
    {
        WaveRoute = null;
        if (ActiveFabricator is not { } fabricator)
            return null;

        var coordinates = GetBuildCursor(mouse);
        if (!coordinates.IsValid(EntityManager))
            return null;

        var point = _transform.ToMapCoordinates(coordinates);
        MassFabricatorHologramComponent? wavePlan = null;
        var unsupported = false;
        var noWaveStart = false;
        foreach (var uid in fabricator.Plans)
        {
            if (!TryComp<MassFabricatorHologramComponent>(uid, out var plan) ||
                plan.PrintState == MassFabricatorPrintState.Fading)
                continue;

            var removed = !fabricator.HasBlueprint(plan.Blueprint);
            var waveOnly = plan.PrintState == MassFabricatorPrintState.Idle && NeedsWave(plan);
            if ((!removed && !plan.Unsupported && !plan.NoWaveStart && !waveOnly) ||
                !TryComp<SpriteComponent>(uid, out var sprite) || !sprite.Visible ||
                !TryComp<TransformComponent>(uid, out var transform) || transform.MapID != point.MapId)
                continue;

            var hovered = plan.BuildGrid is { } frame &&
                TryComp<MapGridComponent>(frame, out var grid)
                ? Transform(frame).MapID == point.MapId && _mapSystem.TileIndicesFor(frame, grid, point) == plan.Cell
                : _clickable.CheckClick((uid, null, sprite, transform), point.Position, _eyes.CurrentEye, out _, out _, out _);
            if (!hovered)
                continue;

            if (removed)
                return ("mass-fabricator-blueprint-removed", true);

            if (plan.Unsupported)
                unsupported = true;
            else if (plan.NoWaveStart)
                noWaveStart = true;
            else if (waveOnly)
                wavePlan = plan;
        }

        if (unsupported)
            return ("mass-fabricator-unsupported-tile", true);
        if (noWaveStart)
            return ("mass-fabricator-no-wave-start", true);
        if (wavePlan == null)
            return null;

        WaveRoute = FindWaveRoute(fabricator, wavePlan);
        return (WaveRoute != null ? "mass-fabricator-wave-arrow" : string.Empty, false);
    }

    public void ClearWaveRoute() => WaveRoute = null;

    public bool NeedsWave(MassFabricatorHologramComponent plan) =>
        plan.PrintBlocked || plan.BuildGrid is { } frame &&
        TryComp<MapGridComponent>(frame, out var grid) && !HasPrintFooting((frame, grid), plan.Cell);

    private List<EntityCoordinates>? FindWaveRoute(MassFabricatorComponent fabricator, MassFabricatorHologramComponent start)
    {
        if (start.BuildGrid is not { } frame || !TryComp<MapGridComponent>(frame, out var grid))
            return null;

        var plans = new Dictionary<Vector2i, MassFabricatorHologramComponent>();
        foreach (var uid in fabricator.Plans)
        {
            if (TryComp<MassFabricatorHologramComponent>(uid, out var plan) &&
                plan.BuildGrid == frame && plan.Blueprint == start.Blueprint && plan.Target == start.Target &&
                plan.PrintState != MassFabricatorPrintState.Fading && !plan.Unsupported)
                plans.TryAdd(plan.Cell, plan);
        }

        var queue = new Queue<Vector2i>();
        var previous = new Dictionary<Vector2i, Vector2i>();
        plans.Remove(start.Cell);
        queue.Enqueue(start.Cell);
        while (queue.TryDequeue(out var cell))
        {
            foreach (var offset in BuildNeighbors)
            {
                var next = cell + offset;
                if (!plans.Remove(next, out var plan))
                    continue;

                previous[next] = cell;
                if (!NeedsWave(plan))
                {
                    var path = new List<EntityCoordinates>();
                    for (var step = next; step != start.Cell; step = previous[step])
                        path.Add(_mapSystem.GridTileToLocal(frame, grid, step));
                    path.Add(_mapSystem.GridTileToLocal(frame, grid, start.Cell));
                    path.Reverse();
                    return path;
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    public float PredictedBlueprintLoad => ActiveFabricator?.BlueprintLoad ?? 0f;

    public void Select(ProtoId<MassFabricatorBlueprintPrototype>? blueprint, string? target, MassFabricatorEraseMode eraseMode)
    {
        if (Returning || ActiveFabricatorUid is not { } uid || !HasComp<MassFabricatorComponent>(uid))
            return;

        EraseMode = eraseMode;
        ClearPlacement();
        _queuedRequests.Enqueue(new MassFabricatorSelectEvent(GetNetEntity(uid), blueprint, target));
        if (_states.CurrentState is MassFabricatorState)
            _input.Contexts.SetActiveContext("fabricator");
    }

    public void Place(EntityCoordinates coordinates, Angle rotation)
    {
        if (_fabricator is not { } uid ||
            EraseMode != MassFabricatorEraseMode.None || ActiveFabricator is not { SelectedBlueprint: { } id, SelectedTarget: { } target } component ||
            !_prototypes.TryIndex(id, out var blueprint))
            return;

        if (blueprint.Kind == MassFabricatorBlueprintKind.Tile)
            rotation = Angle.Zero;

        var bulk = _placement is PlacementManager manager && manager.PlacementType != PlacementManager.PlacementTypes.None;
        if (_placedThisPress || _placedCoordinates.Count >= 256 || _players.LocalEntity is not { } user)
            return;

        if (!TryGetPlacementCoordinates(user, blueprint.Kind, coordinates, out coordinates))
            return;

        var staged = bulk && blueprint.Kind == MassFabricatorBlueprintKind.Tile ? _stagedTiles : null;
        var result = CanPlacePlan(user, blueprint, target, coordinates, PredictedBlueprintLoad + (staged?.Count ?? 0) * blueprint.Load,
            component.MaxBlueprintLoad, staged);
        if (result == MassFabricatorPlacementResult.Invalid)
            return;

        var netCoords = GetNetCoordinates(coordinates);
        if (!_placedCoordinates.Add(netCoords))
            return;

        if (!bulk)
            _placedThisPress = true;

        if (result == MassFabricatorPlacementResult.OutOfLoad)
        {
            _rejections.Add(coordinates, rotation);
            return;
        }

        staged?.Add(coordinates);
        _queuedRequests.Enqueue(new MassFabricatorPlaceEvent(GetNetEntity(uid), netCoords, rotation, id, target));
    }

    public bool CanPreviewTile(EntityUid user, EntityCoordinates coordinates) =>
        CanPlaceTile(user, coordinates, _stagedTiles);

    public void Erase(EntityUid[] plans)
    {
        if (EraseMode != MassFabricatorEraseMode.Single || _fabricator is not { } fabricator)
            return;

        var netPlans = new List<NetEntity>();
        foreach (var plan in plans)
        {
            if (!TryComp<MassFabricatorHologramComponent>(plan, out var hologram) ||
                hologram.Fabricator != fabricator)
                continue;

            netPlans.Add(GetNetEntity(plan));
        }

        if (netPlans.Count > 0)
            _queuedRequests.Enqueue(new MassFabricatorEraseEvent(GetNetEntity(fabricator), netPlans.ToArray()));
    }

    public void EndPlacementPress()
    {
        _placedThisPress = false;
        _placedCoordinates.Clear();
        _stagedTiles.Clear();
    }

    public void ClearPlacement()
    {
        EndPlacementPress();
        if (_fabricator is { } uid && _placement.CurrentPermission?.MobUid == uid)
            _placement.Clear();
    }

    public void BeginPlacement()
    {
        ClearPlacement();
        if (_fabricator is not { } uid || EraseMode != MassFabricatorEraseMode.None || ActiveFabricator is not { } component ||
            component.SelectedBlueprint is not { } id || component.SelectedTarget is not { } target ||
            !_prototypes.TryIndex(id, out var blueprint) || !blueprint.HasTarget(target))
            return;

        var tile = blueprint.Kind == MassFabricatorBlueprintKind.Tile;
        var info = new PlacementInformation
        {
            MobUid = uid,
            PlacementOption = nameof(AlignMassFabricator),
            IsTile = tile,
            EntityType = tile ? null : target,
            TileType = tile && _prototypes.TryIndex<ContentTileDefinition>(target, out var definition) ? definition.TileId : 0,
            UseEditorContext = false,
        };
        _placement.BeginPlacing(info, new MassFabricatorPlacementHijack(this, blueprint, target, _prototypes, _resources, EntityManager.System<SpriteSystem>()));
    }

    public MassFabricatorComponent? ActiveFabricator =>
        ActiveFabricatorUid is { } uid && TryComp<MassFabricatorComponent>(uid, out var fabricator) ? fabricator : null;

    public bool IsEyeHidden(EntityUid user) => !HasComp<MassFabricatorViewComponent>(user);

    public override void Update(float frameTime)
    {
        if (!_gameStates.IsPredictionEnabled || _timing.InPrediction && _timing.IsFirstTimePredicted)
        {
            while (_queuedRequests.TryDequeue(out var request))
            {
                switch (request)
                {
                    case MassFabricatorSelectEvent selection:
                        RaisePredictiveEvent(selection);
                        break;
                    case MassFabricatorPlaceEvent placement:
                        RaisePredictiveEvent(placement);
                        break;
                    case MassFabricatorEraseEvent erasure:
                        RaisePredictiveEvent(erasure);
                        break;
                }
            }
        }

        base.Update(frameTime);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var fabricator = ActiveFabricatorUid;
        if (_fabricator != fabricator)
        {
            ClearPlacement();
            EntityManager.System<MassFabricatorEraseSystem>().Clear();
            _queuedRequests.Clear();
            _rejections.Clear();
            EraseMode = MassFabricatorEraseMode.None;
            _fabricator = fabricator;
        }

        if (fabricator != null && _states.CurrentState is not MassFabricatorState)
            _states.RequestStateChange<MassFabricatorState>();
        else if (fabricator == null && _states.CurrentState is MassFabricatorState)
            _states.RequestStateChange<GameplayState>();

        if (_queuedRequests.Count == 0 && _placement is PlacementManager manager)
        {
            if (fabricator == null || EraseMode != MassFabricatorEraseMode.None ||
                ActiveFabricator is not { SelectedBlueprint: { } blueprint, SelectedTarget: { } target })
            {
                if (manager.Hijack is MassFabricatorPlacementHijack)
                    ClearPlacement();
            }
            else if (manager.CurrentPermission?.MobUid != fabricator ||
                     manager.Hijack is not MassFabricatorPlacementHijack hijack || !hijack.Matches(blueprint, target))
                BeginPlacement();
        }
    }

}
