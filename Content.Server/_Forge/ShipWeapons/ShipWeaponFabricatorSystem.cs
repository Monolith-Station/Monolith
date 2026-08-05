using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Audio;
using Content.Server.Construction;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared._Forge.ShipWeapons;
using Content.Shared._Forge.ShipWeapons.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Forge.ShipWeapons;

public sealed class ShipWeaponFabricatorSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> MachineBoard2x1Tag = "MachineBoard2x1";
    private static readonly ProtoId<TagPrototype> MachineBoard2x2Tag = "MachineBoard2x2";
    private static readonly EntProtoId Flatpack2x3Prototype = "ForgeShipWeaponFlatpack2x3";
    private static readonly EntProtoId Flatpack3x3Prototype = "ForgeShipWeaponFlatpack3x3";

    /// <summary>
    /// Frontier stock parts use internal <see cref="MachinePartComponent.Rating"/> values that do not match
    /// player-facing tiers (see ru-RU machine_parts.ftl suffixes). Ship weapon boards use display tiers 1–4.
    /// </summary>
    private static readonly HashSet<string> BluespaceStockPartPrototypes = new(StringComparer.Ordinal)
    {
        "QuadraticCapacitorStockPart",
        "FemtoManipulatorStockPart",
        "BluespaceMatterBinStockPart",
    };

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedConstructionSystem _construction = default!;
    [Dependency] private readonly FlatpackSystem _flatpack = default!;
    [Dependency] private readonly MachinePartSystem _machinePart = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipWeaponFabricatorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, EntRemovedFromContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, GetMaterialWhitelistEvent>(OnGetMaterialWhitelist);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpen);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, ShipWeaponFabricatorStartMessage>(OnStartPressed);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, ShipWeaponFabricatorEjectMessage>(OnEjectPressed);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, ShipWeaponFabricatorEjectPartMessage>(OnEjectPartPressed);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, MachineDeconstructedEvent>(OnDeconstructed);
        SubscribeLocalEvent<ShipWeaponFabricatorComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnInit(EntityUid uid, ShipWeaponFabricatorComponent component, ComponentInit args)
    {
        component.BoardContainer = _container.EnsureContainer<Container>(uid, ShipWeaponFabricatorComponent.BoardContainerName);
        component.PartContainer = _container.EnsureContainer<Container>(uid, ShipWeaponFabricatorComponent.PartContainerName);
    }

    private void OnMapInit(EntityUid uid, ShipWeaponFabricatorComponent component, MapInitEvent args)
    {
        _materialStorage.UpdateMaterialWhitelist(uid);
    }

    private void OnStartup(EntityUid uid, ShipWeaponFabricatorComponent component, ComponentStartup args)
    {
        RegenerateProgress(uid, component);
        UpdateUi(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, ShipWeaponFabricatorComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered && component.Fabricating)
            SetFabricatingState(uid, component, false);

        UpdateUi(uid, component);
    }

    private void OnAfterUiOpen(EntityUid uid, ShipWeaponFabricatorComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnContainerModified(EntityUid uid, ShipWeaponFabricatorComponent component, ContainerModifiedMessage args)
    {
        if (!ContainersReady(component))
            return;

        if (args.Container.ID != ShipWeaponFabricatorComponent.BoardContainerName &&
            args.Container.ID != ShipWeaponFabricatorComponent.PartContainerName)
            return;

        _materialStorage.UpdateMaterialWhitelist(uid);
        RegenerateProgress(uid, component);
        UpdateUi(uid, component);
    }

    private void OnMaterialAmountChanged(EntityUid uid, ShipWeaponFabricatorComponent component, ref MaterialAmountChangedEvent args)
    {
        if (!ContainersReady(component))
            return;

        RegenerateProgress(uid, component);
        UpdateUi(uid, component);
    }

    private void OnDeconstructed(EntityUid uid, ShipWeaponFabricatorComponent component, ref MachineDeconstructedEvent args)
    {
        EjectContainedEntities(uid, component);
    }

    private void OnDestroyed(EntityUid uid, ShipWeaponFabricatorComponent component, ref DestructionEventArgs args)
    {
        EjectContainedEntities(uid, component);
    }

    private void OnGetMaterialWhitelist(EntityUid uid, ShipWeaponFabricatorComponent component, ref GetMaterialWhitelistEvent args)
    {
        foreach (var material in _prototype.EnumeratePrototypes<MaterialPrototype>())
        {
            args.Whitelist.Add(material);
        }
    }

    private void OnExamined(EntityUid uid, ShipWeaponFabricatorComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!component.HasBoard)
        {
            args.PushMarkup(Loc.GetString("ship-weapon-fabricator-examine-empty"));
            return;
        }

        var board = GetActiveBoard(component)!.Value;
        if (component.BoardContainer.Count > 1)
        {
            args.PushMarkup(Loc.GetString("ship-weapon-fabricator-examine-board-queued",
                ("board", Name(board)),
                ("queued", component.BoardContainer.Count - 1)));
            return;
        }

        args.PushMarkup(Loc.GetString("ship-weapon-fabricator-examine-board", ("board", Name(board))));
    }

    private void OnInteractUsing(EntityUid uid, ShipWeaponFabricatorComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<MachineBoardComponent>(args.Used, out _) &&
            TryComp<ShipWeaponBoardComponent>(args.Used, out _))
        {
            if (TryInsertBoard(uid, args.Used, component, args.User))
            {
                args.Handled = true;
                UpdateUi(uid, component);
            }

            return;
        }

        if (component.Fabricating)
            return;

        if (TryComp<MachinePartComponent>(args.Used, out _))
        {
            if (TryInsertPart(args.Used, component))
            {
                args.Handled = true;
                UpdateUi(uid, component);
            }

            return;
        }

        if (TryComp<StackComponent>(args.Used, out _))
        {
            if (TryInsertAnyMaterial(uid, args.User, args.Used))
            {
                args.Handled = true;
                UpdateUi(uid, component);
            }

            return;
        }

        foreach (var (compName, info) in component.ComponentRequirements)
        {
            if (component.ComponentProgress[compName] >= info.Amount)
                continue;

            var registration = Factory.GetRegistration(compName);
            if (!HasComp(args.Used, registration.Type))
                continue;

            if (!_container.TryRemoveFromContainer(args.Used, false, out var wasInContainer) && wasInContainer)
                return;

            if (!_container.Insert(args.Used, component.PartContainer))
                return;

            component.ComponentProgress[compName]++;
            args.Handled = true;
            UpdateUi(uid, component);
            return;
        }

        if (!TryComp<TagComponent>(args.Used, out var tagComp))
            return;

        foreach (var (tagName, info) in component.TagRequirements)
        {
            if (component.TagProgress[tagName] >= info.Amount)
                continue;

            if (!_tag.HasTag(tagComp, tagName))
                continue;

            if (!_container.TryRemoveFromContainer(args.Used, false, out var wasInContainer) && wasInContainer)
                return;

            if (!_container.Insert(args.Used, component.PartContainer))
                return;

            component.TagProgress[tagName]++;
            args.Handled = true;
            UpdateUi(uid, component);
            return;
        }
    }

    private void OnStartPressed(EntityUid uid, ShipWeaponFabricatorComponent component, ShipWeaponFabricatorStartMessage args)
    {
        if (!TryStartFabrication(uid, component, args.Actor))
            return;

        UpdateUi(uid, component);
    }

    private void OnEjectPressed(EntityUid uid, ShipWeaponFabricatorComponent component, ShipWeaponFabricatorEjectMessage args)
    {
        if (component.Fabricating || GetActiveBoard(component) is not { } board)
            return;

        _hands.PickupOrDrop(args.Actor, board, checkActionBlocker: false, dropNear: true);
        UpdateUi(uid, component);
    }

    private void OnEjectPartPressed(EntityUid uid, ShipWeaponFabricatorComponent component, ShipWeaponFabricatorEjectPartMessage args)
    {
        if (component.Fabricating || !ContainersReady(component))
            return;

        var remaining = Math.Max(args.Amount, 0);
        var coordinates = Transform(uid).Coordinates;

        for (var i = component.PartContainer.ContainedEntities.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var entity = component.PartContainer.ContainedEntities[i];
            if (MetaData(entity).EntityPrototype?.ID != args.PartPrototype)
                continue;

            if (TryComp<StackComponent>(entity, out var stack) && stack.Count > remaining)
            {
                _stack.Split(entity, remaining, coordinates, stack);
                remaining = 0;
                break;
            }

            var count = 1;
            if (TryComp<StackComponent>(entity, out stack))
                count = stack.Count;

            if (!_container.Remove(entity, component.PartContainer, force: true, destination: coordinates))
                continue;

            remaining -= count;
        }

        RegenerateProgress(uid, component);
        UpdateUi(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShipWeaponFabricatorComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Fabricating || _timing.CurTime < component.FabricationEndTime)
                continue;

            FinishFabrication(uid, component);
        }
    }

    private bool TryInsertBoard(EntityUid uid, EntityUid used, ShipWeaponFabricatorComponent component, EntityUid? user = null)
    {
        if (!TryComp<MachineBoardComponent>(used, out var machineBoard) ||
            !TryComp<ShipWeaponBoardComponent>(used, out _))
            return false;

        if (_whitelist.IsWhitelistFail(component.BoardWhitelist, used) ||
            _whitelist.IsBlacklistPass(component.BoardBlacklist, used))
            return false;

        if (component.BoardContainer.Count >= component.MaxBoardQueue)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ship-weapon-fabricator-popup-queue-full"), uid, user.Value);

            return false;
        }

        if (!_container.TryRemoveFromContainer(used, false, out var wasInContainer) && wasInContainer)
            return false;

        if (!_container.Insert(used, component.BoardContainer))
            return false;

        if (component.BoardContainer.Count == 1)
        {
            ResetProgressAndRequirements(component, machineBoard);
            _materialStorage.UpdateMaterialWhitelist(uid);
        }

        RegenerateProgress(uid, component);
        return true;
    }

    private bool TryInsertPart(EntityUid used, ShipWeaponFabricatorComponent component)
    {
        if (!_container.TryRemoveFromContainer(used, false, out var wasInContainer) && wasInContainer)
            return false;

        if (!_container.Insert(used, component.PartContainer))
            return false;

        return true;
    }

    private bool TryInsertAnyMaterial(EntityUid uid, EntityUid user, EntityUid used)
    {
        return _materialStorage.TryInsertMaxPossibleMaterialEntity(user, used, uid, out _);
    }

    public bool IsComplete(ShipWeaponFabricatorComponent component)
    {
        if (GetActiveBoard(component) == null)
            return false;

        foreach (var (type, amount) in component.Requirements)
        {
            if (component.Progress[type] < amount)
                return false;
        }

        foreach (var (type, amount) in component.MaterialRequirements)
        {
            if (component.MaterialProgress[type] < amount)
                return false;
        }

        foreach (var (compName, info) in component.ComponentRequirements)
        {
            if (component.ComponentProgress[compName] < info.Amount)
                return false;
        }

        foreach (var (tagName, info) in component.TagRequirements)
        {
            if (component.TagProgress[tagName] < info.Amount)
                return false;
        }

        return true;
    }

    public void ResetProgressAndRequirements(ShipWeaponFabricatorComponent component, MachineBoardComponent machineBoard)
    {
        component.Requirements = new Dictionary<ProtoId<MachinePartPrototype>, int>(machineBoard.Requirements);
        component.MaterialRequirements = new Dictionary<ProtoId<StackPrototype>, int>(machineBoard.StackRequirements);
        component.ComponentRequirements = new Dictionary<string, GenericPartInfo>(machineBoard.ComponentRequirements);
        component.TagRequirements = new Dictionary<ProtoId<TagPrototype>, GenericPartInfo>(machineBoard.TagRequirements);

        component.Progress.Clear();
        component.MaterialProgress.Clear();
        component.ComponentProgress.Clear();
        component.TagProgress.Clear();

        foreach (var (partType, _) in component.Requirements)
        {
            component.Progress[partType] = 0;
        }

        foreach (var (stackType, _) in component.MaterialRequirements)
        {
            component.MaterialProgress[stackType] = 0;
        }

        foreach (var (compName, _) in component.ComponentRequirements)
        {
            component.ComponentProgress[compName] = 0;
        }

        foreach (var (tagName, _) in component.TagRequirements)
        {
            component.TagProgress[tagName] = 0;
        }
    }

    public void RegenerateProgress(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!ContainersReady(component))
            return;

        if (GetActiveBoard(component) is not { } board ||
            !TryComp<MachineBoardComponent>(board, out var machineBoard))
        {
            component.Requirements.Clear();
            component.MaterialRequirements.Clear();
            component.ComponentRequirements.Clear();
            component.TagRequirements.Clear();
            component.Progress.Clear();
            component.MaterialProgress.Clear();
            component.ComponentProgress.Clear();
            component.TagProgress.Clear();
            return;
        }

        ResetProgressAndRequirements(component, machineBoard);

        if (TryComp(uid, out MaterialStorageComponent? storage))
        {
            foreach (var (stackType, _) in component.MaterialRequirements)
            {
                component.MaterialProgress[stackType] = GetStoredRequirementAmount(uid, stackType, storage);
            }
        }

        foreach (var part in component.PartContainer.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(part))
                continue;

            if (TryComp<MachinePartComponent>(part, out var machinePart))
            {
                var type = machinePart.PartType;
                if (!component.Requirements.ContainsKey(type))
                    continue;

                if (!PartMeetsDisplayTierRequirement(part, machinePart, GetRequiredPartRating(component)))
                    continue;

                var quantity = 1;
                if (TryComp<StackComponent>(part, out var partStack))
                    quantity = partStack.Count;

                component.Progress[type] += quantity;
                continue;
            }

            foreach (var (compName, _) in component.ComponentRequirements)
            {
                var registration = Factory.GetRegistration(compName);
                if (!HasComp(part, registration.Type))
                    continue;

                component.ComponentProgress[compName]++;
            }

            if (!TryComp<TagComponent>(part, out var tagComp))
                continue;

            foreach (var tagName in component.TagRequirements.Keys)
            {
                if (_tag.HasTag(tagComp, tagName))
                    component.TagProgress[tagName]++;
            }
        }
    }

    private void FinishFabrication(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!component.HasBoard)
        {
            SetFabricatingState(uid, component, false);
            UpdateUi(uid, component);
            return;
        }

        if (!CanOutput(uid))
        {
            SetFabricatingState(uid, component, false);
            UpdateUi(uid, component);
            return;
        }

        var board = GetActiveBoard(component);
        if (board == null || !TryComp<MachineBoardComponent>(board.Value, out var machineBoard))
        {
            SetFabricatingState(uid, component, false);
            UpdateUi(uid, component);
            return;
        }

        // Re-check resources at finish time — prevents free crafts if state drifted mid-job.
        RegenerateProgress(uid, component);
        if (!IsComplete(component))
        {
            SetFabricatingState(uid, component, false);
            UpdateUi(uid, component);
            return;
        }

        // Consume before spawning so a failed deduct never yields a free weapon.
        if (!TryConsumeRequiredMaterials(uid, component) || !TryConsumeRequiredParts(component))
        {
            SetFabricatingState(uid, component, false);
            UpdateUi(uid, component);
            return;
        }

        var packedPrototype = machineBoard.Prototype;
        var flatpackPrototype = GetOutputFlatpackPrototype(board.Value, component);
        var output = GetOutputCoordinates(uid);
        var flatpack = Spawn(flatpackPrototype, output);
        _flatpack.ConfigureFlatpack(flatpack, packedPrototype, board.Value);

        // Remove the board from the queue immediately. QueueDel alone leaves it in the
        // container until end-of-tick, so TryContinueQueue would reuse the same board.
        _container.Remove(board.Value, component.BoardContainer, reparent: false, force: true);
        QueueDel(board.Value);

        _materialStorage.UpdateMaterialWhitelist(uid);
        RegenerateProgress(uid, component);

        if (TryContinueQueue(uid, component))
        {
            UpdateUi(uid, component);
            return;
        }

        SetFabricatingState(uid, component, false);
        UpdateUi(uid, component);
    }

    private bool TryStartFabrication(EntityUid uid, ShipWeaponFabricatorComponent component, EntityUid? user)
    {
        if (component.Fabricating)
            return false;

        if (GetActiveBoard(component) is not { } board || !IsComplete(component))
            return false;

        if (!IsPowered(uid))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ship-weapon-fabricator-popup-unpowered"), uid, user.Value);

            return false;
        }

        if (!CanOutput(uid))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("ship-weapon-fabricator-popup-blocked"), uid, user.Value);

            return false;
        }

        if (!TryComp<ShipWeaponBoardComponent>(board, out var shipWeaponBoard))
            return false;

        component.FabricationEndTime = _timing.CurTime + shipWeaponBoard.FabricationTime;
        SetFabricatingState(uid, component, true);
        return true;
    }

    private bool TryContinueQueue(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        // Use GetActiveBoard so boards queued for deletion cannot start another job.
        if (GetActiveBoard(component) is not { } board || !IsComplete(component))
            return false;

        if (!IsPowered(uid) || !CanOutput(uid))
            return false;

        if (!TryComp<ShipWeaponBoardComponent>(board, out var shipWeaponBoard))
            return false;

        component.FabricationEndTime = _timing.CurTime + shipWeaponBoard.FabricationTime;
        return true;
    }

    private EntityUid? GetActiveBoard(ShipWeaponFabricatorComponent component)
    {
        foreach (var board in component.BoardContainer.ContainedEntities)
        {
            if (MetaData(board).EntityLifeStage >= EntityLifeStage.Terminating ||
                EntityManager.IsQueuedForDeletion(board))
                continue;

            return board;
        }

        return null;
    }

    private void SetFabricatingState(EntityUid uid, ShipWeaponFabricatorComponent component, bool fabricating)
    {
        if (component.Fabricating == fabricating)
            return;

        component.Fabricating = fabricating;
        _appearance.SetData(uid, ShipWeaponFabricatorVisuals.Fabricating, fabricating);
        _ambientSound.SetAmbience(uid, fabricating);
        UpdatePowerLoad(uid, component);
    }

    private bool CanOutput(EntityUid uid)
    {
        return true;
    }

    private EntProtoId GetOutputFlatpackPrototype(EntityUid board, ShipWeaponFabricatorComponent component)
    {
        if (_tag.HasTag(board, MachineBoard2x2Tag))
            return Flatpack3x3Prototype;

        if (_tag.HasTag(board, MachineBoard2x1Tag))
            return Flatpack2x3Prototype;

        return component.OutputFlatpackPrototype;
    }

    private EntityCoordinates GetOutputCoordinates(EntityUid uid)
    {
        return Transform(uid).Coordinates.Offset(new Vector2(0, -2));
    }

    private bool IsPowered(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
    }

    private void UpdatePowerLoad(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power))
            return;

        power.Load = component.Fabricating
            ? component.PowerLoadActive
            : component.PowerLoadIdle;
    }

    private void UpdateUi(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!ContainersReady(component) || TerminatingOrDeleted(uid))
            return;

        string? boardName = null;
        string? targetName = null;
        string? targetPrototypeId = null;

        if (GetActiveBoard(component) is { } board &&
            TryComp<MachineBoardComponent>(board, out var machineBoard))
        {
            boardName = Name(board);
            targetPrototypeId = machineBoard.Prototype;
            targetName = _prototype.Index<EntityPrototype>(machineBoard.Prototype).Name;
        }

        var state = new ShipWeaponFabricatorState(
            boardName,
            targetName,
            BuildRequirementsText(component),
            BuildLoadedPartsText(component),
            BuildLoadedMaterialEntries(uid, component),
            BuildLoadedPartEntries(component),
            BuildStatusText(uid, component),
            targetPrototypeId,
            component.HasBoard && IsComplete(component) && !component.Fabricating && IsPowered(uid) && CanOutput(uid),
            component.HasBoard && !component.Fabricating,
            component.Fabricating,
            component.BoardContainer.Count,
            BuildQueueText(component));

        _ui.SetUiState(uid, ShipWeaponFabricatorUiKey.Key, state);
    }

    private string BuildStatusText(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!component.HasBoard)
            return Loc.GetString("ship-weapon-fabricator-ui-status-no-board");

        if (component.Fabricating)
        {
            if (component.BoardContainer.Count > 1)
            {
                return Loc.GetString("ship-weapon-fabricator-ui-status-fabricating-queued",
                    ("queued", component.BoardContainer.Count - 1));
            }

            return Loc.GetString("ship-weapon-fabricator-ui-status-fabricating");
        }

        if (!IsPowered(uid))
            return Loc.GetString("ship-weapon-fabricator-ui-status-unpowered");

        if (IsComplete(component))
        {
            if (!CanOutput(uid))
                return Loc.GetString("ship-weapon-fabricator-ui-status-output-blocked");

            return Loc.GetString("ship-weapon-fabricator-ui-status-ready");
        }

        return Loc.GetString("ship-weapon-fabricator-ui-status-awaiting-parts");
    }

    private string? BuildQueueText(ShipWeaponFabricatorComponent component)
    {
        if (component.BoardContainer.Count <= 1)
            return null;

        var queuedNames = component.BoardContainer.ContainedEntities
            .Skip(1)
            .Select(entity => Name(entity))
            .ToArray();

        return Loc.GetString("ship-weapon-fabricator-ui-queue-line",
            ("queued", queuedNames.Length),
            ("names", string.Join(", ", queuedNames)));
    }

    private string BuildRequirementsText(ShipWeaponFabricatorComponent component)
    {
        if (!component.HasBoard)
            return Loc.GetString("ship-weapon-fabricator-ui-no-board-details");

        var builder = new StringBuilder();
        var wroteLine = false;

        foreach (var (part, amount) in component.Requirements)
        {
            var partProto = _prototype.Index(part);
            var name = Loc.GetString("ship-weapon-fabricator-ui-part-with-rating",
                ("name", Loc.GetString(_prototype.Index(partProto.StockPartPrototype).Name)),
                ("rating", GetRequiredPartRating(component)));
            builder.AppendLine(Loc.GetString("ship-weapon-fabricator-ui-requirement-line",
                ("current", Math.Min(component.Progress[part], amount)),
                ("required", amount),
                ("name", name)));
            wroteLine = true;
        }

        foreach (var (material, amount) in component.MaterialRequirements)
        {
            var stack = _prototype.Index(material);
            var name = _prototype.Index(stack.Spawn).Name;
            builder.AppendLine(Loc.GetString("ship-weapon-fabricator-ui-requirement-line",
                ("current", Math.Min(component.MaterialProgress[material], amount)),
                ("required", amount),
                ("name", Loc.GetString(name))));
            wroteLine = true;
        }

        foreach (var (compName, info) in component.ComponentRequirements)
        {
            builder.AppendLine(Loc.GetString("ship-weapon-fabricator-ui-requirement-line",
                ("current", Math.Min(component.ComponentProgress[compName], info.Amount)),
                ("required", info.Amount),
                ("name", _construction.GetExamineName(info))));
            wroteLine = true;
        }

        foreach (var (tagName, info) in component.TagRequirements)
        {
            builder.AppendLine(Loc.GetString("ship-weapon-fabricator-ui-requirement-line",
                ("current", Math.Min(component.TagProgress[tagName], info.Amount)),
                ("required", info.Amount),
                ("name", _construction.GetExamineName(info))));
            wroteLine = true;
        }

        if (!wroteLine)
            return Loc.GetString("ship-weapon-fabricator-ui-no-extra-requirements");

        return builder.ToString().TrimEnd();
    }

    private string BuildLoadedPartsText(ShipWeaponFabricatorComponent component)
    {
        if (component.PartContainer == null)
            return Loc.GetString("ship-weapon-fabricator-ui-no-loaded-parts");

        var requiredRating = GetRequiredPartRating(component);
        var storedParts = new Dictionary<string, (string Name, int Count, int Rating, bool Compatible)>();
        foreach (var entity in component.PartContainer.ContainedEntities)
        {
            if (!TryComp<MachinePartComponent>(entity, out var machinePart))
                continue;

            var protoId = MetaData(entity).EntityPrototype?.ID;
            if (protoId == null)
                continue;

            var quantity = 1;
            if (TryComp<StackComponent>(entity, out var stack))
                quantity = stack.Count;

            var displayName = Name(entity);
            var displayTier = GetPartDisplayTier(entity, machinePart);
            var compatible = !component.HasBoard ||
                             (component.Requirements.ContainsKey(machinePart.PartType) &&
                              displayTier >= requiredRating);
            if (storedParts.TryGetValue(protoId, out var entry))
                storedParts[protoId] = (entry.Name, entry.Count + quantity, entry.Rating, entry.Compatible);
            else
                storedParts[protoId] = (displayName, quantity, displayTier, compatible);
        }

        if (storedParts.Count == 0)
            return Loc.GetString("ship-weapon-fabricator-ui-no-loaded-parts");

        var builder = new StringBuilder();
        foreach (var (_, entry) in storedParts.OrderBy(x => x.Value.Name))
        {
            var key = entry.Compatible
                ? "ship-weapon-fabricator-ui-stored-part-with-rating-line"
                : "ship-weapon-fabricator-ui-stored-part-incompatible-line";
            builder.AppendLine(Loc.GetString(key,
                ("count", entry.Count),
                ("name", entry.Name),
                ("rating", entry.Rating)));
        }

        return builder.ToString().TrimEnd();
    }

    private List<ShipWeaponFabricatorLoadedPartEntry> BuildLoadedPartEntries(ShipWeaponFabricatorComponent component)
    {
        var requiredRating = GetRequiredPartRating(component);
        var storedParts = new Dictionary<string, ShipWeaponFabricatorLoadedPartEntry>();
        foreach (var entity in component.PartContainer.ContainedEntities)
        {
            if (!TryComp<MachinePartComponent>(entity, out var machinePart))
                continue;

            var protoId = MetaData(entity).EntityPrototype?.ID;
            if (protoId == null)
                continue;

            var quantity = 1;
            if (TryComp<StackComponent>(entity, out var stack))
                quantity = stack.Count;

            var displayTier = GetPartDisplayTier(entity, machinePart);
            var compatible = !component.HasBoard ||
                             (component.Requirements.ContainsKey(machinePart.PartType) &&
                              displayTier >= requiredRating);

            if (storedParts.TryGetValue(protoId, out var current))
            {
                storedParts[protoId] = new ShipWeaponFabricatorLoadedPartEntry(
                    current.PartPrototype,
                    current.DisplayName,
                    current.Current + quantity,
                    current.Rating,
                    current.Compatible);
            }
            else
            {
                storedParts[protoId] = new ShipWeaponFabricatorLoadedPartEntry(
                    protoId,
                    Name(entity),
                    quantity,
                    displayTier,
                    compatible);
            }
        }

        return storedParts.Values.OrderBy(x => x.DisplayName).ToList();
    }

    private List<ShipWeaponFabricatorLoadedMaterialEntry> BuildLoadedMaterialEntries(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        var entries = new List<ShipWeaponFabricatorLoadedMaterialEntry>();
        if (!TryComp(uid, out MaterialStorageComponent? storage))
            return entries;

        var required = new Dictionary<ProtoId<MaterialPrototype>, int>();
        if (component.HasBoard)
        {
            foreach (var (stackType, amount) in component.MaterialRequirements)
            {
                if (!TryGetMaterialForRequirement(stackType, out var materialId))
                    continue;

                required.TryAdd(materialId, 0);
                required[materialId] += amount;
            }
        }

        foreach (var (materialId, volume) in _materialStorage.GetStoredMaterials((uid, storage)))
        {
            if (!_prototype.TryIndex<MaterialPrototype>(materialId, out var material))
                continue;

            var sheetVolume = _materialStorage.GetSheetVolume(material);
            var sheets = volume / sheetVolume;
            if (sheets <= 0)
                continue;

            var compatible = !component.HasBoard || required.ContainsKey(materialId);
            required.TryGetValue(materialId, out var requiredSheets);

            entries.Add(new ShipWeaponFabricatorLoadedMaterialEntry(
                materialId,
                Loc.GetString(material.Name),
                sheets,
                compatible ? requiredSheets : null,
                compatible));
        }

        return entries.OrderBy(x => x.DisplayName).ToList();
    }

    private int GetRequiredPartRating(ShipWeaponFabricatorComponent component)
    {
        if (GetActiveBoard(component) is not { } board ||
            !TryComp<ShipWeaponBoardComponent>(board, out var shipWeaponBoard))
            return 1;

        return shipWeaponBoard.RequiredPartRating;
    }

    private int GetPartDisplayTier(EntityUid entity, MachinePartComponent machinePart)
    {
        var protoId = MetaData(entity).EntityPrototype?.ID;
        if (protoId != null && BluespaceStockPartPrototypes.Contains(protoId))
            return 4;

        // rating 1 = Tier 1, rating 3 = Tier 2 (advanced), rating 4 = Tier 3 (super).
        return machinePart.Rating switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 2,
            4 => 3,
            _ => Math.Clamp(machinePart.Rating, 1, 4),
        };
    }

    private bool PartMeetsDisplayTierRequirement(EntityUid entity, MachinePartComponent machinePart, int requiredDisplayTier)
    {
        return GetPartDisplayTier(entity, machinePart) >= requiredDisplayTier;
    }

    private bool ContainersReady(ShipWeaponFabricatorComponent component)
    {
        return component.BoardContainer != null && component.PartContainer != null;
    }

    private void EjectContainedEntities(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!ContainersReady(component))
            return;

        var coordinates = Transform(uid).Coordinates;

        _container.EmptyContainer(component.BoardContainer, true, coordinates);

        _container.EmptyContainer(component.PartContainer, true, coordinates);
    }

    private bool TryConsumeRequiredParts(ShipWeaponFabricatorComponent component)
    {
        foreach (var (partType, amount) in component.Requirements)
        {
            var remaining = amount;
            for (var i = component.PartContainer.ContainedEntities.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var entity = component.PartContainer.ContainedEntities[i];
                if (EntityManager.IsQueuedForDeletion(entity))
                    continue;

                if (!TryComp<MachinePartComponent>(entity, out var machinePart) || machinePart.PartType != partType)
                    continue;

                if (!PartMeetsDisplayTierRequirement(entity, machinePart, GetRequiredPartRating(component)))
                    continue;

                if (TryComp<StackComponent>(entity, out var stack))
                {
                    var consume = Math.Min(stack.Count, remaining);
                    if (!_stack.Use(entity, consume, stack))
                        continue;

                    remaining -= consume;
                    continue;
                }

                // Remove immediately so RegenerateProgress / queue continue cannot recount this part.
                _container.Remove(entity, component.PartContainer, reparent: false, force: true);
                QueueDel(entity);
                remaining--;
            }

            if (remaining > 0)
                return false;
        }

        for (var i = component.PartContainer.ContainedEntities.Count - 1; i >= 0; i--)
        {
            var entity = component.PartContainer.ContainedEntities[i];
            if (!HasComp<MachinePartComponent>(entity))
            {
                _container.Remove(entity, component.PartContainer, reparent: false, force: true);
                QueueDel(entity);
            }
        }

        return true;
    }

    private int GetStoredRequirementAmount(EntityUid uid, ProtoId<StackPrototype> stackType, MaterialStorageComponent storage)
    {
        if (!TryGetMaterialForRequirement(stackType, out var materialId))
            return 0;

        if (!_prototype.TryIndex<MaterialPrototype>(materialId, out var material))
            return 0;

        var storedVolume = _materialStorage.GetMaterialAmount(uid, material, storage);
        var sheetVolume = _materialStorage.GetSheetVolume(material);
        return storedVolume / sheetVolume;
    }

    private bool TryGetMaterialForRequirement(ProtoId<StackPrototype> stackType, out ProtoId<MaterialPrototype> materialId)
    {
        if (_prototype.TryIndex<MaterialPrototype>(stackType, out var directMaterial))
        {
            materialId = directMaterial.ID;
            return true;
        }

        var stack = _prototype.Index(stackType);
        var entity = _prototype.Index<EntityPrototype>(stack.Spawn);

        if (entity.TryGetComponent<PhysicalCompositionComponent>(out var composition, EntityManager.ComponentFactory) &&
            composition.MaterialComposition.Count > 0)
        {
            materialId = composition.MaterialComposition.First().Key;
            return true;
        }

        materialId = default;
        return false;
    }

    private bool TryConsumeRequiredMaterials(EntityUid uid, ShipWeaponFabricatorComponent component)
    {
        if (!TryComp(uid, out MaterialStorageComponent? storage))
            return component.MaterialRequirements.Count == 0;

        foreach (var (stackType, amount) in component.MaterialRequirements)
        {
            if (!TryGetMaterialForRequirement(stackType, out var materialId) ||
                !_prototype.TryIndex<MaterialPrototype>(materialId, out var material))
                return false;

            var volume = amount * _materialStorage.GetSheetVolume(material);
            if (!_materialStorage.TryChangeMaterialAmount(uid, materialId, -volume, storage))
                return false;
        }

        return true;
    }
}
