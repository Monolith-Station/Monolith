using Content.Server.Administration.Logs;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.RandomMetadata;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Silicons.StationAi;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Forge.Silicons.StationAi;

public sealed class StationAiPersonalitySystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan CustomizationCooldown = TimeSpan.FromSeconds(60);
    private readonly Dictionary<EntityUid, SsdRelease> _ssdReleases = new();
    private readonly List<EntityUid> _dueSsdReleases = new();
    private TimeSpan _nextSsdCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiPersonalityComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<StationAiPersonalityComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<StationAiPersonalityComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<StationAiPersonalityComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<StationAiPersonalityComponent, ComponentShutdown>(OnPersonalityShutdown);
        SubscribeLocalEvent<StationAiHeldComponent, OpenStationAiCustomizationEvent>(OnOpenCustomization);
        SubscribeLocalEvent<StationAiHeldComponent, StationAiCustomizationApplyMessage>(OnApplyCustomization);
        SubscribeLocalEvent<StationAiScreenComponent, ComponentStartup>(OnCoreStartup);
        SubscribeLocalEvent<StationAiScreenComponent, EntInsertedIntoContainerMessage>(OnCoreInsert);
        SubscribeLocalEvent<StationAiScreenComponent, EntRemovedFromContainerMessage>(OnCoreRemove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ssdReleases.Count == 0 || _timing.CurTime < _nextSsdCheck)
            return;

        _nextSsdCheck = _timing.CurTime + TimeSpan.FromSeconds(1);
        _dueSsdReleases.Clear();
        foreach (var (brain, release) in _ssdReleases)
        {
            if (_timing.CurTime >= release.Deadline)
                _dueSsdReleases.Add(brain);
        }

        foreach (var brain in _dueSsdReleases)
        {
            if (!_ssdReleases.TryGetValue(brain, out var release))
                continue;

            if (TryReleaseDisconnectedAi(brain, release))
                _ssdReleases.Remove(brain);
        }
    }

    private void OnMindAdded(EntityUid uid, StationAiPersonalityComponent component, MindAddedMessage args)
    {
        _ssdReleases.Remove(uid);
        var newOwner = component.OwnerMind != args.Mind.Owner;
        component.OwnerMind = args.Mind.Owner;
        component.Occupant = args.Mind.Comp.UserId;

        if (newOwner || component.PersonalityName == null)
        {
            component.PersonalityName = GetNewPersonalityName(uid);
            component.Screen = GetDefaultScreen(uid);
            component.Color = Color.White;
            component.NextCustomization = TimeSpan.Zero;
        }

        ApplyIdentity(uid, component, args.Mind);
        RaiseAvailabilityChanged(uid);
    }

    private void OnMindRemoved(EntityUid uid, StationAiPersonalityComponent component, MindRemovedMessage args)
    {
        if (_ssdReleases.TryGetValue(uid, out var release) && release.Mind == args.Mind.Owner)
            _ssdReleases.Remove(uid);

        component.Occupant = null;
        ReopenGhostRole(uid);
        if (_stationAi.TryGetCore(uid, out var core))
            SetCoreEmpty(core.Owner);
        RaiseAvailabilityChanged(uid, true);
    }

    private void ReopenGhostRole(EntityUid brain)
    {
        if (!TryComp(brain, out GhostRoleComponent? role) || !role.ReregisterOnGhost)
            return;

        EnsureComp<GhostTakeoverAvailableComponent>(brain);
        _ghostRoles.ReregisterGhostRole((brain, role));
    }

    private void OnPlayerAttached(Entity<StationAiPersonalityComponent> ent, ref PlayerAttachedEvent args)
    {
        RaiseAvailabilityChanged(ent.Owner);
        if (!_ssdReleases.TryGetValue(ent.Owner, out var release) ||
            release.User != args.Player.UserId ||
            !TryGetOwnedMind(ent.Owner, out var mind) ||
            mind.Owner != release.Mind)
        {
            return;
        }

        _ssdReleases.Remove(ent.Owner);
    }

    private void OnPlayerDetached(Entity<StationAiPersonalityComponent> ent, ref PlayerDetachedEvent args)
    {
        RaiseAvailabilityChanged(ent.Owner, true);
        if (args.Player.Status is not (SessionStatus.Disconnected or SessionStatus.Zombie) ||
            !TryGetOwnedMind(ent.Owner, out var mind) ||
            mind.Comp.UserId != args.Player.UserId)
        {
            return;
        }

        var timeout = Math.Max(1f, _configuration.GetCVar(ForgeCVars.StationAiSsdGracePeriod));
        _ssdReleases[ent.Owner] = new SsdRelease(
            mind.Owner,
            args.Player.UserId,
            _timing.CurTime + TimeSpan.FromSeconds(timeout));
    }

    private void OnPersonalityShutdown(Entity<StationAiPersonalityComponent> ent, ref ComponentShutdown args)
    {
        _ssdReleases.Remove(ent.Owner);
        RaiseAvailabilityChanged(ent.Owner, true);
    }

    private void OnOpenCustomization(Entity<StationAiHeldComponent> ent, ref OpenStationAiCustomizationEvent args)
    {
        if (args.Handled ||
            !TryComp(ent.Owner, out StationAiPersonalityComponent? personality) ||
            !IsCurrentController(ent.Owner))
        {
            return;
        }

        UpdateUi(ent.Owner, personality);
        args.Handled = _ui.TryOpenUi(ent.Owner, StationAiCustomizationUiKey.Key, ent.Owner);
    }

    private void OnApplyCustomization(Entity<StationAiHeldComponent> ent, ref StationAiCustomizationApplyMessage args)
    {
        if (args.Actor != ent.Owner ||
            !TryComp(ent.Owner, out StationAiPersonalityComponent? personality) ||
            !IsCurrentController(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-customization-not-controller"), ent.Owner, args.Actor, PopupType.MediumCaution);
            return;
        }

        if (_timing.CurTime < personality.NextCustomization)
        {
            var seconds = Math.Max(1, (int) Math.Ceiling((personality.NextCustomization - _timing.CurTime).TotalSeconds));
            _popup.PopupEntity(Loc.GetString("station-ai-customization-cooldown", ("seconds", seconds)), ent.Owner, args.Actor, PopupType.MediumCaution);
            UpdateUi(ent.Owner, personality);
            return;
        }

        if (!_prototypes.HasIndex<StationAiScreenPrototype>(args.Screen))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-customization-invalid-screen"), ent.Owner, args.Actor, PopupType.MediumCaution);
            return;
        }

        var forceNamePrefix = GetForceNamePrefix(ent.Owner);
        if (!StationAiCustomizationValidator.TryNormalizeNamePart(
                args.Name,
                forceNamePrefix,
                _configuration.GetCVar(CCVars.RestrictedNames),
                _configuration.GetCVar(CCVars.ICNameCase),
                out _,
                out var name))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-customization-invalid-name"), ent.Owner, args.Actor, PopupType.MediumCaution);
            return;
        }

        if (!StationAiCustomizationValidator.TryNormalizeColor(args.Color, out var color))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-customization-invalid-color"), ent.Owner, args.Actor, PopupType.MediumCaution);
            return;
        }

        if (personality.PersonalityName == name && personality.Screen == args.Screen && personality.Color == color)
            return;

        personality.PersonalityName = name;
        personality.Screen = args.Screen;
        personality.Color = color;
        personality.NextCustomization = _timing.CurTime + CustomizationCooldown;
        ApplyIdentity(ent.Owner, personality);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} customized station AI {ToPrettyString(ent.Owner):entity} as '{name}' with screen '{args.Screen}' and color '{color}'.");
        _popup.PopupEntity(Loc.GetString("station-ai-customization-applied"), ent.Owner, args.Actor);
        UpdateUi(ent.Owner, personality);
    }

    private void OnCoreStartup(Entity<StationAiScreenComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.OriginalName ??= Name(ent.Owner);
        RefreshCore(ent.Owner);
    }

    private void OnCoreInsert(Entity<StationAiScreenComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == StationAiCoreComponent.Container)
        {
            RefreshCore(ent.Owner);
            RaiseAvailabilityChanged(args.Entity);
        }
    }

    private void OnCoreRemove(Entity<StationAiScreenComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == StationAiCoreComponent.Container)
        {
            SetCoreEmpty(ent.Owner);
            RaiseAvailabilityChanged(args.Entity, true);
        }
    }

    private void RaiseAvailabilityChanged(EntityUid brain, bool remove = false)
    {
        var ev = new StationAiAvailabilityChangedEvent(remove);
        RaiseLocalEvent(brain, ref ev);
    }

    private void RefreshCore(EntityUid core)
    {
        if (!TryComp(core, out StationAiCoreComponent? coreComponent) ||
            !_stationAi.TryGetHeld((core, coreComponent), out var brain) ||
            !TryComp(brain, out StationAiPersonalityComponent? personality) ||
            !TryGetOwnedMind(brain, out _))
        {
            SetCoreEmpty(core);
            return;
        }

        SetCoreOccupied(core, personality);
    }

    private void ApplyIdentity(
        EntityUid brain,
        StationAiPersonalityComponent personality,
        Entity<MindComponent>? mind = null)
    {
        var name = personality.PersonalityName ?? GetNewPersonalityName(brain);
        personality.PersonalityName = name;
        _metadata.SetEntityName(brain, name);

        if (mind == null && TryGetOwnedMind(brain, out var ownedMind))
            mind = ownedMind;

        if (mind != null)
        {
            mind.Value.Comp.CharacterName = name;
            Dirty(mind.Value.Owner, mind.Value.Comp);
        }

        if (_stationAi.TryGetCore(brain, out var core) && TryGetOwnedMind(brain, out _))
            SetCoreOccupied(core.Owner, personality);
    }

    private void SetCoreOccupied(EntityUid core, StationAiPersonalityComponent personality)
    {
        if (!TryComp(core, out StationAiScreenComponent? component))
            return;

        _metadata.SetEntityName(core, personality.PersonalityName ?? Loc.GetString("station-ai-default-name"));
        component.Screen = personality.Screen;
        component.Color = personality.Color;
        component.Occupied = true;
        Dirty(core, component);
    }

    private void SetCoreEmpty(EntityUid core)
    {
        if (!TryComp(core, out StationAiScreenComponent? component))
            return;

        component.OriginalName ??= Name(core);
        _metadata.SetEntityName(core, component.OriginalName);
        component.Screen = component.DefaultScreen;
        component.Color = Color.White;
        component.Occupied = false;
        Dirty(core, component);
    }

    private bool IsCurrentController(EntityUid brain)
    {
        if (!HasComp<ActorComponent>(brain) || !_stationAi.TryGetCore(brain, out var core))
            return false;

        return _stationAi.TryGetHeld(core, out var held) && held == brain;
    }

    private bool TryGetOwnedMind(EntityUid brain, out Entity<MindComponent> mind)
    {
        mind = default;
        if (!TryComp(brain, out MindContainerComponent? container) ||
            container.Mind is not { } mindUid ||
            !TryComp(mindUid, out MindComponent? mindComponent) ||
            mindComponent.OwnedEntity != brain ||
            mindComponent.UserId == null)
        {
            return false;
        }

        mind = (mindUid, mindComponent);
        return true;
    }

    private void UpdateUi(EntityUid brain, StationAiPersonalityComponent personality)
    {
        var remaining = Math.Max(0, (int) Math.Ceiling((personality.NextCustomization - _timing.CurTime).TotalSeconds));
        var forceNamePrefix = GetForceNamePrefix(brain);
        var fullName = personality.PersonalityName ?? Name(brain);
        var editableName = StationAiCustomizationValidator.GetEditableNamePart(fullName, forceNamePrefix);
        _ui.SetUiState(brain,
            StationAiCustomizationUiKey.Key,
            new StationAiCustomizationState(
                editableName,
                forceNamePrefix,
                personality.Screen,
                personality.Color,
                remaining));
    }

    private string GetNewPersonalityName(EntityUid brain)
    {
        if (TryComp(brain, out RandomMetadataComponent? random) && random.NameSegments is { Count: > 0 })
            return _randomMetadata.GetRandomFromSegments(random.NameSegments, random.NameSeparator);

        return Loc.GetString("station-ai-default-name");
    }

    private string GetForceNamePrefix(EntityUid brain)
    {
        if (!_containers.TryGetContainingContainer(brain, out var container) ||
            container.ID != StationAiCoreComponent.Container ||
            !TryComp(container.Owner, out StationAiScreenComponent? screen))
        {
            return string.Empty;
        }

        return screen.ForceNamePrefix.Trim();
    }

    private ProtoId<StationAiScreenPrototype> GetDefaultScreen(EntityUid brain)
    {
        return _stationAi.TryGetCore(brain, out var core) &&
               TryComp(core.Owner, out StationAiScreenComponent? screen)
            ? screen.DefaultScreen
            : (ProtoId<StationAiScreenPrototype>) "StationAiScreenDefault";
    }

    private bool TryReleaseDisconnectedAi(EntityUid brain, SsdRelease release)
    {
        if (TerminatingOrDeleted(brain) ||
            !TryGetOwnedMind(brain, out var mind) ||
            mind.Owner != release.Mind ||
            mind.Comp.UserId != release.User)
        {
            return true;
        }

        if (_players.TryGetSessionById(release.User, out var session) &&
            session.Status is not (SessionStatus.Disconnected or SessionStatus.Zombie))
        {
            return true;
        }

        _mind.TransferTo(mind.Owner, null, mind: mind.Comp);
        var released = !TryGetOwnedMind(brain, out var currentMind) || currentMind.Owner != release.Mind;
        if (released)
            ReopenGhostRole(brain);
        return released;
    }

    private readonly record struct SsdRelease(EntityUid Mind, NetUserId User, TimeSpan Deadline);
}
