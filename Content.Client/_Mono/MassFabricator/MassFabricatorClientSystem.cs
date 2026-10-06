using System.Linq;
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

    private MassFabricatorViewComponent? View =>
        _players.LocalEntity is { } user && TryComp<MassFabricatorViewComponent>(user, out var view) ? view : null;

    public bool Returning => View?.Returning ?? false;
    private EntityUid? _fabricator;
    public MassFabricatorEraseMode EraseMode { get; private set; }

    private bool _placedThisPress;
    private readonly HashSet<NetCoordinates> _placedCoordinates = new();
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

        _queuedRequests.Enqueue(new MassFabricatorPlaceEvent(GetNetEntity(uid), netCoords, rotation, id, target));
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

            netPlans.Add(GetNetEntity(plan));
        }

        if (netPlans.Count > 0)
            _queuedRequests.Enqueue(new MassFabricatorEraseEvent(GetNetEntity(fabricator), netPlans.ToArray()));
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
