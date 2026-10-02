using System.Linq;
using Content.Client.Gameplay;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands.Components;
using Content.Shared.Maps;
using Robust.Client.Placement;
using Robust.Client.Input;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Content.Shared.Movement.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
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

    public bool Returning { get; private set; }
    private EntityUid? _viewUser;
    private EntityUid? _fabricator;
    public MassFabricatorEraseMode EraseMode { get; private set; }
    private EntityUid? _hiddenUser;
    private uint _returnStartTick;
    private uint _returnFinishTick;
    private bool _completedLocally;
    private bool _placedThisPress;
    private readonly HashSet<NetCoordinates> _placedCoordinates = new();
    [Dependency] private MassFabricatorRejectionSystem _rejections = default!;
    private readonly List<MassFabricatorEraseEvent> _queuedErasures = new();
    private readonly List<EntityUid> _queuedLocalErasures = new();
    private readonly List<PendingPlan> _pendingPlans = new();
    private readonly Dictionary<EntityUid, TimeSpan> _hiddenPlans = new();
    private readonly HashSet<EntityUid> _confirmedPlans = new();

    private sealed class PendingPlan(EntityUid uid, ProtoId<MassFabricatorBlueprintPrototype> blueprint,
        string target, MapCoordinates position, TimeSpan created)
    {
        public EntityUid Uid = uid;
        public readonly ProtoId<MassFabricatorBlueprintPrototype> Blueprint = blueprint;
        public readonly string Target = target;
        public readonly MapCoordinates Position = position;
        public readonly TimeSpan Created = created;
        public bool EraseRequested;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MassFabricatorViewEvent>(OnView);
        SubscribeLocalEvent<MassFabricatorComponent, ComponentShutdown>(OnFabricatorShutdown);
        _overlays.AddOverlay(new MassFabricatorEyeOverlay());
    }

    private void OnFabricatorShutdown(Entity<MassFabricatorComponent> ent, ref ComponentShutdown args)
    {
        if (_fabricator == ent.Owner)
            ClearPredictions();
    }

    public override void Shutdown()
    {
        ClearPredictions();
        _overlays.RemoveOverlay<MassFabricatorEyeOverlay>();
        base.Shutdown();
    }

    protected override void OnUse(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (Returning || StartView(ent, ref args) == null || args.User != _players.LocalEntity || !_timing.IsFirstTimePredicted)
            return;

        Returning = false;
        _completedLocally = false;
        _returnStartTick = 0;
        _viewUser = args.User;
        _fabricator = ent.Owner;
        EraseMode = MassFabricatorEraseMode.None;
        _hiddenUser = null;
        _states.RequestStateChange<MassFabricatorState>();
    }

    private void OnView(MassFabricatorViewEvent ev)
    {
        if (ev.Enabled && _states.CurrentState is not MassFabricatorState)
        {
            _hiddenUser = null;
            _viewUser = _players.LocalEntity;
            if (_viewUser is { } user && TryComp<HandsComponent>(user, out var hands))
                _fabricator = hands.ActiveHandEntity;
            _states.RequestStateChange<MassFabricatorState>();
        }
        else if (!ev.Enabled)
        {
            Returning = false;
            _completedLocally = false;
            _returnStartTick = 0;
            if (_viewUser is { } user && !Deleted(user))
                StopPredictedView(user);
            _viewUser = null;
            _fabricator = null;
            EraseMode = MassFabricatorEraseMode.None;
            ClearPredictions();
            _rejections.Clear();
            if (_states.CurrentState is MassFabricatorState)
                _states.RequestStateChange<GameplayState>();
        }
    }

    protected override void OnPrintStarted(Entity<MassFabricatorHologramComponent> plan,
        MassFabricatorBlueprintPrototype blueprint, int remaining) =>
        EntityManager.System<MassFabricatorHologramSystem>().TrackManualPrint(plan, blueprint, remaining);

    protected override void OnManualPrintResult(Entity<MassFabricatorHologramComponent> plan, bool cancelled) =>
        EntityManager.System<MassFabricatorHologramSystem>().OnManualPrintResult(plan, cancelled);

    protected override void OnPlanVisualShutdown(Entity<MassFabricatorHologramComponent> plan) =>
        EntityManager.System<MassFabricatorHologramSystem>().OnPlanShutdown(plan);

    protected override bool CanModifyPlans(EntityUid user, EntityUid gun) =>
        _viewUser == user && _fabricator == gun && !Returning &&
        TryComp<HandsComponent>(user, out var hands) && hands.ActiveHandEntity == gun;

    protected override void OnPlanCreated(Entity<MassFabricatorHologramComponent> plan)
    {
        var position = EntityManager.System<SharedTransformSystem>().ToMapCoordinates(Transform(plan.Owner).Coordinates);
        foreach (var pending in _pendingPlans)
        {
            if (pending.Uid.IsValid() && !Deleted(pending.Uid) ||
                pending.Blueprint != plan.Comp.Blueprint || pending.Target != plan.Comp.Target ||
                pending.Position.MapId != position.MapId ||
                (pending.Position.Position - position.Position).LengthSquared() >= 0.01f)
                continue;

            pending.Uid = plan.Owner;
            if (pending.EraseRequested)
            {
                ErasePlan(plan.Comp.Fabricator, plan);
                return;
            }
            break;
        }
        EntityManager.System<MassFabricatorHologramSystem>().ShowPlan(plan.Owner);
    }

    protected override bool UpdateAutomaticPrints => _timing.InPrediction;

    protected override void OnAutomaticPrintStarted(Entity<MassFabricatorHologramComponent> parent,
        Entity<MassFabricatorHologramComponent> neighbor, MassFabricatorBlueprintPrototype blueprint) =>
        EntityManager.System<MassFabricatorHologramSystem>().TrackAutomaticPrint(parent, neighbor, blueprint);

    public EntityUid? ActiveFabricatorUid => _fabricator;

    public float PredictedBlueprintLoad => ActiveFabricator?.BlueprintLoad ?? 0f;

    public void Select(ProtoId<MassFabricatorBlueprintPrototype>? blueprint, string? target, MassFabricatorEraseMode eraseMode)
    {
        if (_fabricator is not { } uid || !TryComp<MassFabricatorComponent>(uid, out var component) ||
            !TrySelectBlueprint((uid, component), blueprint, target))
            return;
        EraseMode = eraseMode;
        RaiseNetworkEvent(new MassFabricatorSelectEvent(GetNetEntity(uid), blueprint, target));
        BeginPlacement();
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

        var grid = _placement is PlacementManager { PlacementType: PlacementManager.PlacementTypes.Grid };
        if (_placedThisPress || _placedCoordinates.Count >= 256 || _players.LocalEntity is not { } user)
            return;

        if (!TryGetPlacementCoordinates(user, blueprint.Kind, coordinates, out coordinates))
            return;

        var result = CanPlacePlan(user, blueprint, target, coordinates, PredictedBlueprintLoad,
            component.MaxBlueprintLoad);
        if (result == MassFabricatorPlacementResult.Invalid)
            return;

        var netCoords = GetNetCoordinates(coordinates);
        if (!_placedCoordinates.Add(netCoords))
            return;

        if (!grid)
            _placedThisPress = true;

        if (result == MassFabricatorPlacementResult.OutOfLoad)
        {
            _rejections.Add(coordinates, rotation);
            return;
        }

        RaisePredictiveEvent(new MassFabricatorPlaceEvent(GetNetEntity(uid), netCoords, rotation, id, target));
        _pendingPlans.Add(new PendingPlan(EntityUid.Invalid, id, target,
            EntityManager.System<SharedTransformSystem>().ToMapCoordinates(coordinates), _timing.CurTime));
    }

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

            if (EntityManager.IsClientSide(plan))
            {
                foreach (var pending in _pendingPlans)
                {
                    if (pending.Uid != plan || pending.EraseRequested)
                        continue;

                    pending.EraseRequested = true;
                    HidePlan(plan);
                    _queuedLocalErasures.Add(plan);
                }
            }
            else if (!_hiddenPlans.ContainsKey(plan))
            {
                netPlans.Add(GetNetEntity(plan));
                HidePlan(plan);
            }
        }

        if (netPlans.Count > 0)
            _queuedErasures.Add(new MassFabricatorEraseEvent(GetNetEntity(fabricator), netPlans.ToArray()));
    }

    private void HidePlan(EntityUid plan)
    {
        if (!TryComp<SpriteComponent>(plan, out var sprite))
            return;

        EntityManager.System<SpriteSystem>().SetVisible((plan, sprite), false);
        _hiddenPlans[plan] = _timing.CurTime;
    }

    public void EndPlacementPress()
    {
        _placedThisPress = false;
        _placedCoordinates.Clear();
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
        _fabricator is { } uid && TryComp<MassFabricatorComponent>(uid, out var fabricator) ? fabricator : null;

    public bool IsEyeHidden(EntityUid user) => _hiddenUser == user;

    protected override bool CanBeginReturn(EntityUid user, EntityUid eye) => _viewUser == user;

    protected override void OnReturnStarted(EntityUid user, EntityUid eye)
    {
        if (!Returning)
            _returnStartTick = _timing.CurTick.Value;
        Returning = true;
    }

    public override void Update(float frameTime)
    {
        if (!_gameStates.IsPredictionEnabled || _timing.InPrediction && _timing.IsFirstTimePredicted)
        {
            foreach (var plan in _queuedLocalErasures)
                if (TryComp<MassFabricatorHologramComponent>(plan, out var hologram))
                    ErasePlan(hologram.Fabricator, (plan, hologram));
            _queuedLocalErasures.Clear();
            foreach (var request in _queuedErasures)
                RaisePredictiveEvent(request);
            _queuedErasures.Clear();
        }

        base.Update(frameTime);
        if (!Returning || !_timing.InPrediction || _timing.CurTick.Value < _returnStartTick ||
            _players.LocalEntity is not { } user)
            return;

        if (_completedLocally)
        {
            if (_timing.CurTick.Value >= _returnFinishTick)
                StopPredictedView(user);
            return;
        }

        if (!TryComp<EyeComponent>(user, out var eye) || eye.Target is not { } target)
            return;

        if (HasComp<MobMoverComponent>(target))
            BeginReturn(user, target);
        if (UpdateReturn(user, target, frameTime, _returnStartTick))
        {
            _completedLocally = true;
            _returnFinishTick = _timing.CurTick.Value;
            _hiddenUser = user;
            StopPredictedView(user);
            if (_timing.IsFirstTimePredicted && _states.CurrentState is MassFabricatorState)
                _states.RequestStateChange<GameplayState>();
        }
    }

    private void ClearPredictions()
    {
        _pendingPlans.Clear();
        foreach (var uid in _hiddenPlans.Keys)
            if (TryComp<SpriteComponent>(uid, out var sprite))
                EntityManager.System<SpriteSystem>().SetVisible((uid, sprite), true);
        _hiddenPlans.Clear();
        _confirmedPlans.Clear();
    }

    private void ReconcilePlans()
    {
        var transform = EntityManager.System<SharedTransformSystem>();
        _confirmedPlans.RemoveWhere(uid => Deleted(uid));
        for (var i = _pendingPlans.Count - 1; i >= 0; i--)
        {
            var pending = _pendingPlans[i];
            EntityUid? confirmed = null;
            var query = EntityQueryEnumerator<MassFabricatorHologramComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var plan, out var xform))
            {
                if (EntityManager.IsClientSide(uid) || _confirmedPlans.Contains(uid) ||
                    plan.Fabricator != _fabricator || plan.Blueprint != pending.Blueprint ||
                    plan.Target != pending.Target)
                    continue;

                var position = transform.ToMapCoordinates(xform.Coordinates);
                if (position.MapId == pending.Position.MapId &&
                    (position.Position - pending.Position.Position).LengthSquared() < 0.01f)
                {
                    confirmed = uid;
                    break;
                }
            }

            if (confirmed is { } real)
            {
                _confirmedPlans.Add(real);
                if (pending.EraseRequested && _fabricator is { } fabricator)
                {
                    HidePlan(real);
                    _queuedErasures.Add(new MassFabricatorEraseEvent(GetNetEntity(fabricator), [GetNetEntity(real)]));
                }
            }

            if (confirmed == null && (_timing.CurTime - pending.Created).TotalSeconds < 2)
                continue;

            _pendingPlans.RemoveAt(i);
        }

        foreach (var (uid, hiddenAt) in _hiddenPlans.ToArray())
        {
            if (!Deleted(uid) && (_timing.CurTime - hiddenAt).TotalSeconds < 2)
                continue;

            if (TryComp<SpriteComponent>(uid, out var sprite))
                EntityManager.System<SpriteSystem>().SetVisible((uid, sprite), true);
            _hiddenPlans.Remove(uid);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        ReconcilePlans();
        if (_completedLocally && Returning && _timing.CurTick.Value >= _returnFinishTick &&
            _viewUser is { } user && !Deleted(user) &&
            (HasComp<RelayInputMoverComponent>(user) ||
             TryComp<EyeComponent>(user, out var eye) && eye.Target != null))
            StopPredictedView(user);

        if (_completedLocally && _timing.CurTick.Value >= _returnFinishTick &&
            _states.CurrentState is MassFabricatorState)
            _states.RequestStateChange<GameplayState>();
    }

}
