using System.Numerics;
using System.Linq;
using Content.Shared.Materials;
using Content.Shared.Input;
using Content.Shared.Examine;
using Robust.Shared.Configuration;
using Robust.Shared;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Content.Shared.Maps;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.MassFabricator;

public enum MassFabricatorPlacementResult : byte
{
    Allowed,
    Invalid,
    OutOfLoad,
}

public abstract partial class MassFabricatorSystem : EntitySystem
{
    private const float ReturnStartSpeed = 2f;
    private const float ReturnAcceleration = 32f;
    private const float ReturnMaxSpeed = 20f;
    private const float ReturnRadius = 0.7f;

    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private SharedEyeSystem _eyes = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private MassFabricatorVisibilitySystem _visibility = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _printDoAfter = default!;
    [Dependency] private IConfigurationManager _eraseConfig = default!;
    [Dependency] private ExamineSystemShared _eraseExamine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MassFabricatorComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<MassFabricatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MassFabricatorHologramComponent, InteractUsingEvent>(OnPrint);
        SubscribeLocalEvent<MassFabricatorHologramComponent, RangedInteractEvent>(OnRangedPrint);
        SubscribeLocalEvent<MassFabricatorHologramComponent, MassFabricatorPrintDoAfterEvent>(OnPrintFinished);
        SubscribeLocalEvent<MassFabricatorHologramComponent, ComponentShutdown>(OnPlanShutdown);
        SubscribeAllEvent<MassFabricatorPlaceEvent>(OnPlaceRequest);
        SubscribeAllEvent<MassFabricatorEraseEvent>(OnEraseRequest);
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.EscapeContext, InputCmdHandler.FromDelegate(HandleReturn, handle: false, outsidePrediction: false))
            .Register<MassFabricatorSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<MassFabricatorSystem>();
        base.Shutdown();
    }

    private void OnExamined(Entity<MassFabricatorComponent> ent, ref ExaminedEvent args)
    {
        var currentLoad = ent.Comp.BlueprintLoad;
        var load = ent.Comp.MaxBlueprintLoad > 0
            ? Math.Clamp((float) currentLoad / ent.Comp.MaxBlueprintLoad, 0f, 1f) : 0f;
        var loadColor = Color.InterpolateBetween(Color.FromHex("#379DFF"), Color.FromHex("#FF3B3B"),
            Math.Clamp((load - 0.75f) * 4f, 0f, 1f));
        using var group = args.PushGroup(nameof(MassFabricatorComponent));
        args.PushMarkup(Loc.GetString("mass-fabricator-examine-load",
            ("current", currentLoad), ("maximum", ent.Comp.MaxBlueprintLoad),
            ("color", loadColor.ToHex()), ("percent", $"{load * 100f:0}")));
        if (!TryComp<MaterialStorageComponent>(ent.Owner, out var storage))
            return;

        var materials = _materials.GetStoredMaterials((ent.Owner, storage));
        args.PushMarkup(Loc.GetString("mass-fabricator-examine-buffer",
            ("stored", $"{materials.Values.Sum():N0}"),
            ("capacity", storage.StorageLimit is { } limit ? $"{limit:N0}" : Loc.GetString("mass-fabricator-buffer-unlimited"))));
        if (materials.Count == 0)
            args.PushMarkup(Loc.GetString("mass-fabricator-examine-empty"));
        foreach (var (id, amount) in materials.OrderBy(pair => Loc.GetString(_prototypes.Index(pair.Key).Name)))
        {
            var material = _prototypes.Index(id);
            var name = Loc.GetString("mass-fabricator-display-name", ("name", Loc.GetString(material.Name)));
            var color = Color.InterpolateBetween(material.Color, Color.White, 0.4f);
            var line = Loc.GetString("mass-fabricator-examine-material",
                ("name", FormattedMessage.EscapeText(name)), ("color", color.ToHex()), ("amount", $"{amount:N0}"));
            var reserved = ent.Comp.ReservedMaterials.GetValueOrDefault(id.Id);
            if (reserved > 0)
                line += Loc.GetString("mass-fabricator-examine-reserved", ("amount", $"{reserved:N0}"));
            args.PushMarkup(line);
        }
    }

    public bool TrySelectBlueprint(Entity<MassFabricatorComponent> ent,
        ProtoId<MassFabricatorBlueprintPrototype>? blueprint, string? target)
    {
        if (blueprint is { } id)
        {
            if (!ent.Comp.AvailableBlueprints.Contains(id) ||
                !_prototypes.TryIndex(id, out var selected) || !selected.HasTarget(target))
                return false;
        }
        else if (target != null)
            return false;

        ent.Comp.SelectedBlueprint = blueprint;
        ent.Comp.SelectedTarget = target;
        Dirty(ent.Owner, ent.Comp);
        return true;
    }

    public bool InPrintRange(EntityUid user, EntityUid plan) =>
        _transforms.InRange(Transform(user).Coordinates, Transform(plan).Coordinates,
            SharedInteractionSystem.InteractionRange);

    public bool TryStartPrintDoAfter(EntityUid user, EntityUid plan, EntityUid gun, float duration)
    {
        var args = new DoAfterArgs(EntityManager, user, duration,
            new MassFabricatorPrintDoAfterEvent(), plan, used: gun)
        {
            NeedHand = true,
            BreakOnMove = true,
            MultiplyDelay = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
        };
        return _printDoAfter.TryStartDoAfter(args);
    }

    private void OnPrint(Entity<MassFabricatorHologramComponent> ent, ref InteractUsingEvent args)
    {
        if (!args.Handled)
            args.Handled = TryPrint(ent, args.User, args.Used);
    }

    private void OnRangedPrint(Entity<MassFabricatorHologramComponent> ent, ref RangedInteractEvent args)
    {
        if (!args.Handled && ent.Comp.Kind == MassFabricatorBlueprintKind.Entity &&
            InPrintRange(args.UserUid, ent.Owner))
            args.Handled = TryPrint(ent, args.UserUid, args.UsedUid);
    }

    private bool TryPrint(Entity<MassFabricatorHologramComponent> ent, EntityUid user, EntityUid used)
    {
        if (ent.Comp.PrintState != MassFabricatorPrintState.Idle || used != ent.Comp.Fabricator ||
            !InPrintRange(user, ent.Owner) ||
            !TryComp<HandsComponent>(user, out var hands) || hands.ActiveHandEntity != used ||
            !_prototypes.TryIndex(ent.Comp.Blueprint, out var blueprint) ||
            blueprint.Kind != ent.Comp.Kind || !blueprint.HasTarget(ent.Comp.Target))
            return false;

        return StartPrint(ent, blueprint, user);
    }

    protected virtual void OnPrintStarted(Entity<MassFabricatorHologramComponent> plan,
        MassFabricatorBlueprintPrototype blueprint) { }

    protected virtual void OnAutomaticPrintStarted(Entity<MassFabricatorHologramComponent> parent,
        Entity<MassFabricatorHologramComponent> neighbor, MassFabricatorBlueprintPrototype blueprint) { }

    protected virtual bool UpdateAutomaticPrints => true;

    protected void StartNeighbors(Entity<MassFabricatorHologramComponent> completed, EntityUid grid,
        MapGridComponent mapGrid, Vector2i index)
    {
        var query = EntityQueryEnumerator<MassFabricatorHologramComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var plan, out var transform))
        {
            if (plan.Kind != MassFabricatorBlueprintKind.Tile ||
                plan.PrintState != MassFabricatorPrintState.Idle ||
                plan.Fabricator != completed.Comp.Fabricator || plan.Blueprint != completed.Comp.Blueprint ||
                plan.Target != completed.Comp.Target || transform.GridUid != grid)
                continue;

            var cell = _maps.CoordinatesToTile(grid, mapGrid, transform.Coordinates);
            if (Math.Abs(cell.X - index.X) + Math.Abs(cell.Y - index.Y) != 1 ||
                !_prototypes.TryIndex(plan.Blueprint, out var blueprint))
                continue;

            if (StartPrint((uid, plan), blueprint))
                OnAutomaticPrintStarted(completed, (uid, plan), blueprint);
        }
    }

    private void UpdateAutomaticPrintsNow()
    {
        var ready = new List<EntityUid>();
        var query = EntityQueryEnumerator<MassFabricatorHologramComponent>();
        while (query.MoveNext(out var uid, out var plan))
            if (plan.AutomaticPrint && plan.PrintState == MassFabricatorPrintState.Printing &&
                (_timing.CurTime - plan.PrintStarted).TotalSeconds >= plan.PrintDuration)
                ready.Add(uid);

        foreach (var uid in ready)
            if (TryComp<MassFabricatorHologramComponent>(uid, out var plan))
                FinishPrint((uid, plan));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (UpdateAutomaticPrints)
            UpdateAutomaticPrintsNow();
    }

    protected virtual void OnManualPrintResult(Entity<MassFabricatorHologramComponent> plan, bool cancelled) { }

    protected bool StartPrint(Entity<MassFabricatorHologramComponent> ent,
        MassFabricatorBlueprintPrototype blueprint, EntityUid? user = null)
    {
        if (ent.Comp.PrintState != MassFabricatorPrintState.Idle ||
            !TryComp<MassFabricatorComponent>(ent.Comp.Fabricator, out var fabricator))
            return false;

        var costs = blueprint.GetMaterials(ent.Comp.Target);
        var available = AvailableMaterials(ent.Comp.Fabricator);
        if (costs.Count == 0 || costs.Any(pair => pair.Value <= 0 ||
            available.GetValueOrDefault(pair.Key) < pair.Value))
            return false;

        var duration = blueprint.PrintDuration(fabricator.MaxBlueprintLoad);
        if (user is { } actor && !TryStartPrintDoAfter(actor, ent.Owner, ent.Comp.Fabricator, duration))
            return false;

        ent.Comp.ReservedMaterials = new(costs);
        foreach (var (material, amount) in costs)
            fabricator.ReservedMaterials[material] = fabricator.ReservedMaterials.GetValueOrDefault(material) + amount;
        Dirty(ent.Comp.Fabricator, fabricator);
        if (user != null)
            OnPrintStarted(ent, blueprint);
        ent.Comp.AutomaticPrint = user == null;
        ent.Comp.PrintState = MassFabricatorPrintState.Printing;
        ent.Comp.PrintStarted = _timing.CurTime;
        ent.Comp.PrintDuration = duration;
        Dirty(ent.Owner, ent.Comp);
        return true;
    }

    private void OnPrintFinished(Entity<MassFabricatorHologramComponent> ent, ref MassFabricatorPrintDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.PrintState != MassFabricatorPrintState.Printing ||
            ent.Comp.AutomaticPrint || args.Used != ent.Comp.Fabricator)
            return;

        args.Handled = true;
        OnManualPrintResult(ent, args.Cancelled);
        if (args.Cancelled)
            ResetPrint(ent);
        else
            FinishPrint(ent);
    }

    protected void ResetPrint(Entity<MassFabricatorHologramComponent> ent)
    {
        ReleaseMaterials(ent);
        ent.Comp.ReservedMaterials = new();
        ent.Comp.PrintState = MassFabricatorPrintState.Idle;
        Dirty(ent.Owner, ent.Comp);
    }

    protected void FinishPrint(Entity<MassFabricatorHologramComponent> ent)
    {
        if (ent.Comp.PrintState != MassFabricatorPrintState.Printing)
            return;

        if (!_prototypes.TryIndex(ent.Comp.Blueprint, out var blueprint) || !blueprint.HasTarget(ent.Comp.Target))
        {
            ResetPrint(ent);
            return;
        }

        var coords = Transform(ent.Owner).Coordinates;
        if (ent.Comp.Kind == MassFabricatorBlueprintKind.Tile &&
            _prototypes.TryIndex<ContentTileDefinition>(ent.Comp.Target, out var tile) &&
            _transforms.GetGrid(coords) is { } grid &&
            TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            if (!ConsumePlanMaterials(ent))
                return;

            var index = _maps.CoordinatesToTile(grid, mapGrid, coords);
            _maps.SetTile(grid, mapGrid, index, new Tile(tile.TileId));
            ent.Comp.PrintState = MassFabricatorPrintState.Fading;
            ent.Comp.PrintStarted = _timing.CurTime;
            Dirty(ent.Owner, ent.Comp);
            StartNeighbors(ent, grid, mapGrid, index);
        }
        else if (ent.Comp.Kind == MassFabricatorBlueprintKind.Entity &&
                 _prototypes.HasIndex<EntityPrototype>(ent.Comp.Target))
        {
            if (!ConsumePlanMaterials(ent))
                return;

            PredictedSpawnAttachedTo(ent.Comp.Target, coords, rotation: Transform(ent.Owner).LocalRotation);
            ent.Comp.PrintState = MassFabricatorPrintState.Fading;
            ent.Comp.PrintStarted = _timing.CurTime;
            Dirty(ent.Owner, ent.Comp);
        }
        else
            ResetPrint(ent);
    }

    private bool ConsumePlanMaterials(Entity<MassFabricatorHologramComponent> plan)
    {
        if (plan.Comp.ReservedMaterials.Count == 0 ||
            !_materials.TryChangeMaterialAmount((plan.Comp.Fabricator, null),
                plan.Comp.ReservedMaterials.ToDictionary(pair => pair.Key, pair => -pair.Value)))
        {
            ResetPrint(plan);
            return false;
        }

        ReleaseMaterials(plan);
        ReleaseBlueprintLoad(plan);
        plan.Comp.ReservedMaterials = new();
        return true;
    }

    public Dictionary<string, int> AvailableMaterials(EntityUid fabricator)
    {
        var available = _materials.GetStoredMaterials((fabricator, null))
            .ToDictionary(pair => pair.Key.Id, pair => pair.Value);
        if (!TryComp<MassFabricatorComponent>(fabricator, out var gun))
            return available;

        foreach (var (material, amount) in gun.ReservedMaterials)
            available[material] = available.GetValueOrDefault(material) - amount;

        return available;
    }

    private void HandleReturn(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } user ||
            !TryComp<EyeComponent>(user, out var component) || component.Target is not { } eye ||
            Prototype(eye)?.ID != "HZMassFabricatorEye" || !CanBeginReturn(user, eye))
            return;

        BeginReturn(user, eye);
        OnReturnStarted(user, eye);
    }

    public EntityUid? GetPlacementGrid(EntityUid user, MassFabricatorBlueprintKind kind,
        EntityCoordinates requested) => _transforms.GetGrid(requested);

    public bool TryGetPlacementCoordinates(EntityUid user, MassFabricatorBlueprintKind kind,
        EntityCoordinates requested, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (!requested.IsValid(EntityManager) ||
            GetPlacementGrid(user, kind, requested) is not { } grid ||
            !TryComp<MapGridComponent>(grid, out _))
            return false;

        coordinates = _transforms.WithEntityId(requested, grid);
        return true;
    }

    public MassFabricatorPlacementResult CanPlacePlan(EntityUid user, MassFabricatorBlueprintPrototype blueprint,
        string target, EntityCoordinates requested, double currentLoad, int maxLoad)
    {
        if (!TryGetPlacementCoordinates(user, blueprint.Kind, requested, out var coordinates) ||
            !blueprint.HasTarget(target) || blueprint.Load <= 0 || maxLoad <= 0 || blueprint.Load > maxLoad ||
            !(blueprint.Kind == MassFabricatorBlueprintKind.Tile && _prototypes.HasIndex<ContentTileDefinition>(target) ||
              blueprint.Kind == MassFabricatorBlueprintKind.Entity && _prototypes.HasIndex<EntityPrototype>(target)) ||
            !CanSeePlanPlacement(user, coordinates) ||
            blueprint.Kind == MassFabricatorBlueprintKind.Tile && !CanPlaceTile(coordinates) ||
            blueprint.Kind == MassFabricatorBlueprintKind.Entity && !CanPlacePlanOnTile(coordinates))
            return MassFabricatorPlacementResult.Invalid;

        return currentLoad + blueprint.Load > maxLoad
            ? MassFabricatorPlacementResult.OutOfLoad
            : MassFabricatorPlacementResult.Allowed;
    }

    protected virtual bool CanModifyPlans(EntityUid user, EntityUid gun) => false;
    protected virtual void OnPlanCreated(Entity<MassFabricatorHologramComponent> plan) { }

    private void OnPlaceRequest(MassFabricatorPlaceEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(message.Fabricator, out var uid) || uid is not { } gun ||
            !CanModifyPlans(user, gun) ||
            !TryComp<MassFabricatorComponent>(gun, out var component) ||
            !component.AvailableBlueprints.Contains(message.Blueprint) ||
            !_prototypes.TryIndex(message.Blueprint, out var blueprint) || !blueprint.HasTarget(message.Target))
            return;

        CreatePlan(user, (gun, component), blueprint, message.Target,
            GetCoordinates(message.Coordinates), message.Rotation);
    }

    private void OnEraseRequest(MassFabricatorEraseEvent message, EntitySessionEventArgs args)
    {
        if (message.Plans.Length > 256 || args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(message.Fabricator, out var uid) || uid is not { } gun ||
            !CanModifyPlans(user, gun) || !HasComp<MassFabricatorComponent>(gun))
            return;

        var range = _eraseConfig.GetCVar(CVars.NetMaxUpdateRange) * 0.5f;
        foreach (var netPlan in message.Plans)
            if (TryGetEntity(netPlan, out var planUid) && planUid is { } plan &&
                TryComp<MassFabricatorHologramComponent>(plan, out var hologram) &&
                hologram.Fabricator == gun &&
                _eraseExamine.InRangeUnOccluded(user, Transform(plan).Coordinates, range))
                ErasePlan(gun, (plan, hologram));
    }

    public EntityUid? CreatePlan(EntityUid user, Entity<MassFabricatorComponent> gun,
        MassFabricatorBlueprintPrototype blueprint, string target, EntityCoordinates requested, Angle rotation)
    {
        if (!TryGetPlacementCoordinates(user, blueprint.Kind, requested, out var coordinates) ||
            CanPlacePlan(user, blueprint, target, coordinates, gun.Comp.BlueprintLoad,
                gun.Comp.MaxBlueprintLoad) != MassFabricatorPlacementResult.Allowed)
            return null;

        var uid = PredictedSpawnAttachedTo("HZMassFabricatorHologram", coordinates,
            rotation: blueprint.Kind == MassFabricatorBlueprintKind.Tile ? Angle.Zero : rotation);
        var plan = Comp<MassFabricatorHologramComponent>(uid);
        plan.Blueprint = blueprint.ID;
        plan.Fabricator = gun.Owner;
        plan.Kind = blueprint.Kind;
        plan.Target = target;
        Dirty(uid, plan);

        gun.Comp.Plans.Add(uid);
        gun.Comp.BlueprintLoad += blueprint.Load;
        Dirty(gun.Owner, gun.Comp);
        OnPlanCreated((uid, plan));
        return uid;
    }

    public bool ErasePlan(EntityUid gun, Entity<MassFabricatorHologramComponent> plan)
    {
        if (plan.Comp.Fabricator != gun)
            return false;

        if (plan.Comp.PrintState == MassFabricatorPrintState.Printing)
            ResetPrint(plan);
        ReleasePlan(plan);
        PredictedQueueDel(plan.Owner);
        return true;
    }

    protected virtual void OnPlanVisualShutdown(Entity<MassFabricatorHologramComponent> plan) { }

    private void OnPlanShutdown(Entity<MassFabricatorHologramComponent> ent, ref ComponentShutdown args)
    {
        OnPlanVisualShutdown(ent);
        if (!_timing.ApplyingState)
            ReleasePlan(ent);
    }

    private void ReleasePlan(Entity<MassFabricatorHologramComponent> ent)
    {
        if (Deleted(ent.Comp.Fabricator) ||
            !TryComp<MassFabricatorComponent>(ent.Comp.Fabricator, out var gun))
            return;

        var removed = gun.Plans.Remove(ent.Owner);
        if (removed)
        {
            ReleaseBlueprintLoad(ent);
            ReleaseMaterials(ent);
            Dirty(ent.Comp.Fabricator, gun);
        }
    }

    private void ReleaseBlueprintLoad(Entity<MassFabricatorHologramComponent> plan)
    {
        if (plan.Comp.LoadReleased ||
            !TryComp<MassFabricatorComponent>(plan.Comp.Fabricator, out var gun) ||
            !_prototypes.TryIndex(plan.Comp.Blueprint, out var blueprint))
            return;

        gun.BlueprintLoad = Math.Max(0, gun.BlueprintLoad - blueprint.Load);
        plan.Comp.LoadReleased = true;
        Dirty(plan.Comp.Fabricator, gun);
        Dirty(plan.Owner, plan.Comp);
    }

    private void ReleaseMaterials(Entity<MassFabricatorHologramComponent> plan)
    {
        if (!TryComp<MassFabricatorComponent>(plan.Comp.Fabricator, out var gun))
            return;

        foreach (var (material, amount) in plan.Comp.ReservedMaterials)
        {
            var remaining = gun.ReservedMaterials.GetValueOrDefault(material) - amount;
            if (remaining > 0)
                gun.ReservedMaterials[material] = remaining;
            else
                gun.ReservedMaterials.Remove(material);
        }
        Dirty(plan.Comp.Fabricator, gun);
    }

    public bool CanPlaceTile(EntityCoordinates coordinates)
    {
        if (!coordinates.IsValid(EntityManager) ||
            _transforms.GetGrid(coordinates) is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var mapGrid))
            return false;

        var cell = _maps.CoordinatesToTile(grid, mapGrid, coordinates);
        var plans = EntityQueryEnumerator<MassFabricatorHologramComponent, TransformComponent>();
        while (plans.MoveNext(out _, out var plan, out var xform))
        {
            if (plan.Kind == MassFabricatorBlueprintKind.Tile && xform.GridUid == grid &&
                _maps.CoordinatesToTile(grid, mapGrid, xform.Coordinates) == cell)
                return false;
        }

        return true;
    }

    public bool CanPlacePlanOnTile(EntityCoordinates coordinates)
    {
        return _transforms.GetGrid(coordinates) is { } grid &&
               TryComp<MapGridComponent>(grid, out var mapGrid) &&
               !_maps.GetTileRef(grid, mapGrid, coordinates).Tile.IsEmpty;
    }

    public bool CanSeePlanPlacement(EntityUid user, EntityCoordinates coordinates)
    {
        if (_transforms.GetGrid(coordinates) is { } grid &&
            TryComp<BroadphaseComponent>(grid, out var broadphase) &&
            TryComp<MapGridComponent>(grid, out var mapGrid))
            return _visibility.CanSee(user, (grid, broadphase, mapGrid), coordinates);

        var userMap = Transform(user).MapID;
        var target = _transforms.ToMapCoordinates(coordinates);
        var range = _eraseConfig.GetCVar(CVars.NetMaxUpdateRange) * 0.5f;
        return userMap == target.MapId &&
               Vector2.DistanceSquared(_transforms.GetWorldPosition(user), target.Position) <= range * range;
    }

    protected virtual bool CanBeginReturn(EntityUid user, EntityUid eye) => true;
    protected virtual void OnReturnStarted(EntityUid user, EntityUid eye) { }

    protected virtual void OnUse(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        StartView(ent, ref args);
    }

    protected EntityUid? StartView(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || HasComp<RelayInputMoverComponent>(args.User))
            return null;

        var eye = PredictedSpawnAtPosition("HZMassFabricatorEye", Transform(args.User).Coordinates);
        var visual = Comp<MassFabricatorEyeComponent>(eye);
        visual.User = args.User;
        visual.Color = ent.Comp.EyeColor;
        visual.BodyPosition = _transforms.GetWorldPosition(args.User);
        visual.BodyMap = Transform(args.User).MapUid;
        Dirty(eye, visual);

        var seed = SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value,
            GetNetEntity(args.User).Id, GetNetEntity(ent.Owner).Id);
        var angle = (float) (new System.Random(seed).NextDouble() * Math.Tau);
        _physics.SetLinearVelocity(eye, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 50f);

        _eyes.SetDrawFov(args.User, false);
        _eyes.SetTarget(args.User, eye);
        _mover.SetRelay(args.User, eye);
        args.Handled = true;
        return eye;
    }

    protected void BeginReturn(EntityUid user, EntityUid eye)
    {
        RemComp<MobMoverComponent>(eye);
        var delta = _transforms.GetWorldPosition(user) - _transforms.GetWorldPosition(eye);
        if (delta.LengthSquared() > 0f)
            _physics.SetLinearVelocity(eye, Vector2.Normalize(delta) * ReturnStartSpeed);
    }

    protected bool UpdateReturn(EntityUid user, EntityUid eye, float frameTime, uint startTick)
    {
        if (Deleted(eye) || Transform(user).MapID != Transform(eye).MapID)
            return true;

        var destination = _transforms.GetWorldPosition(user);
        var delta = destination - _transforms.GetWorldPosition(eye);
        var distance = delta.Length();
        var elapsed = (_timing.CurTick.Value - startTick) * _timing.TickPeriod.TotalSeconds;
        var speed = MathF.Min(ReturnMaxSpeed, ReturnStartSpeed + ReturnAcceleration * (float) elapsed);
        if (distance <= ReturnRadius + speed * frameTime)
        {
            _transforms.SetWorldPosition(eye, destination);
            return true;
        }

        _physics.SetLinearVelocity(eye, delta / distance * speed);
        return false;
    }

    protected void StopPredictedView(EntityUid user)
    {
        var target = TryComp<RelayInputMoverComponent>(user, out var relay) ? relay.RelayEntity : EntityUid.Invalid;
        if (TryComp<EyeComponent>(user, out var eye) && eye.Target is { } eyeTarget)
            target = eyeTarget;

        _eyes.SetTarget(user, null);
        RemComp<RelayInputMoverComponent>(user);
        _eyes.SetDrawFov(user, true);
        if (target.IsValid() && !Deleted(target) && EntityManager.IsClientSide(target))
            QueueDel(target);
    }
}
