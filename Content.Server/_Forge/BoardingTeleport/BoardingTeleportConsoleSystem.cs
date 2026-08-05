using System.Numerics;

using Content.Server.Atmos.EntitySystems;

using Content.Server.DeviceNetwork.Systems;

using Content.Server.Power.Components;

using Content.Server.Shuttles.Components;

using Content.Server.Shuttles.Systems;

using Content.Shared._Crescent.ShipShields;

using Content.Shared._Forge.BoardingTeleport;

using Content.Shared._Forge.BoardingTeleport.Components;

using Content.Shared.DeviceNetwork.Components;

using Content.Shared.DeviceNetwork.Systems;

using Content.Shared.Maps;
using Content.Shared.Pinpointer;

using Content.Shared.Popups;

using Content.Shared.Power;

using Content.Shared.Shuttles.BUIStates;

using Content.Shared.Shuttles.Components;

using Content.Shared.Shuttles.Systems;

using Content.Shared.Shuttles.UI.MapObjects;

using Content.Shared.Tiles;
using Robust.Server.GameObjects;

using Robust.Shared.Map;

using Robust.Shared.Map.Components;

using Robust.Shared.Physics.Components;

using Robust.Shared.Random;

using Robust.Shared.Timing;



namespace Content.Server._Forge.BoardingTeleport;



public sealed partial class BoardingTeleportConsoleSystem : EntitySystem

{

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    [Dependency] private readonly DeviceListSystem _deviceList = default!;

    [Dependency] private readonly IMapManager _mapManager = default!;

    [Dependency] private readonly SharedMapSystem _map = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;

    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly BoardingTeleportEngineSystem _engine = default!;

    [Dependency] private readonly BoardingTeleportLockSystem _lock = default!;



    public override void Initialize()

    {

        base.Initialize();



        SubscribeLocalEvent<BoardingTeleportConsoleComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, DeviceListUpdateEvent>(OnDeviceListUpdated);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportSelectGridMessage>(OnSelectGrid);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportSelectGridEntityMessage>(OnSelectGridEntity);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportSelectTileMessage>(OnSelectTile);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportBackMessage>(OnBack);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportClearTargetMessage>(OnClearTarget);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportSelectModeMessage>(OnSelectMode);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportSelectPlatformSlotMessage>(OnSelectPlatformSlot);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportSyncVolleyMessage>(OnSyncVolley);

        SubscribeLocalEvent<BoardingTeleportConsoleComponent, BoardingTeleportToggleSharedLandingMessage>(OnToggleSharedLanding);

    }



    private void OnStartup(Entity<BoardingTeleportConsoleComponent> ent, ref ComponentStartup args)

    {

        SyncPlatforms(ent);

        _engine.TryLinkConsole(ent);

    }



    private void OnShutdown(Entity<BoardingTeleportConsoleComponent> ent, ref ComponentShutdown args)

    {

        _engine.UnlinkConsole(ent);

        if (!TryComp<DeviceListComponent>(ent, out var deviceList))
            return;

        foreach (var device in _deviceList.GetAllDevices(ent.Owner, deviceList))

        {

            if (TryComp<BoardingTeleportPlatformComponent>(device, out var platform) &&

                platform.LinkedConsole == ent.Owner)

            {

                platform.LinkedConsole = null;

                Dirty(device, platform);

            }

        }

    }



    private void OnDeviceListUpdated(Entity<BoardingTeleportConsoleComponent> ent, ref DeviceListUpdateEvent args)

    {

        foreach (var oldDevice in args.OldDevices)

        {

            if (TryComp<BoardingTeleportPlatformComponent>(oldDevice, out var oldPlatform) &&

                oldPlatform.LinkedConsole == ent.Owner)

            {

                oldPlatform.LinkedConsole = null;

                Dirty(oldDevice, oldPlatform);

            }

        }



        SyncPlatforms(ent);

    }



    private void OnUiOpened(Entity<BoardingTeleportConsoleComponent> ent, ref BoundUIOpenedEvent args)

    {

        _engine.TryLinkConsole(ent);

        UpdateUi(ent);

    }



    private void OnSelectGrid(Entity<BoardingTeleportConsoleComponent> ent, ref BoardingTeleportSelectGridMessage args)

    {

        if (!TryGetScannerGrid(ent.Owner, out var scannerGrid))

        {

            SetStatus(ent, BoardingTeleportStatus.NoGrid);

            return;

        }

        _engine.TryLinkConsole(ent);

        if (!_engine.TryGetLinkedEngine(ent.Owner, ent.Comp, out _, out var engine))

        {

            SetStatus(ent, BoardingTeleportStatus.NoEngine);

            return;

        }



        var status = BoardingTeleportStatus.InvalidTarget;

        if (!TryGetTargetGrid(args.Coordinates, out var targetGrid) ||

            !TryValidateTargetGrid(ent, scannerGrid, targetGrid, engine, out status))

        {

            SetStatus(ent, status);

            return;

        }



        SelectTargetGrid(ent, targetGrid);

    }



    private void OnSelectGridEntity(Entity<BoardingTeleportConsoleComponent> ent, ref BoardingTeleportSelectGridEntityMessage args)

    {

        if (!TryGetScannerGrid(ent.Owner, out var scannerGrid))

        {

            SetStatus(ent, BoardingTeleportStatus.NoGrid);

            return;

        }

        _engine.TryLinkConsole(ent);

        if (!_engine.TryGetLinkedEngine(ent.Owner, ent.Comp, out _, out var engine))

        {

            SetStatus(ent, BoardingTeleportStatus.NoEngine);

            return;

        }



        var targetGrid = GetEntity(args.Grid);

        if (!TryValidateTargetGrid(ent, scannerGrid, targetGrid, engine, out var status))

        {

            SetStatus(ent, status);

            return;

        }



        SelectTargetGrid(ent, targetGrid);

    }



    private void OnSelectTile(Entity<BoardingTeleportConsoleComponent> ent, ref BoardingTeleportSelectTileMessage args)

    {

        var grid = GetEntity(args.Grid);

        if (ent.Comp.TargetGrid != grid)

        {

            SetStatus(ent, BoardingTeleportStatus.InvalidLanding);

            return;

        }



        if (!TryGetLandingCoordinates(grid, args.Tile, out var coordinates))
        {
            SetStatus(ent, BoardingTeleportStatus.InvalidLanding);
            return;
        }

        _lock.EnsurePlatformLandings(ent.Comp, Math.Max(1, _lock.GetOrderedPlatforms(ent.Owner).Count));
        var slot = Math.Clamp(ent.Comp.SelectedPlatformSlot, 0, BoardingTeleportConstants.MaxPlatformLandingSlots - 1);
        var netCoords = GetNetCoordinates(coordinates);

        if (ent.Comp.UseSharedLandingZone)
        {
            ent.Comp.LandingCoordinates = coordinates;
            for (var i = 0; i < ent.Comp.PlatformLandings.Count; i++)
                ent.Comp.PlatformLandings[i] = netCoords;
        }
        else
        {
            while (ent.Comp.PlatformLandings.Count <= slot)
                ent.Comp.PlatformLandings.Add(null);
            ent.Comp.PlatformLandings[slot] = netCoords;
            if (slot == 0)
                ent.Comp.LandingCoordinates = coordinates;
        }

        _lock.MarkLockEstablished(ent.Comp);
        ent.Comp.Status = BoardingTeleportStatus.LandingSelected;

        Dirty(ent);

        UpdateUi(ent);

    }



    private void OnBack(Entity<BoardingTeleportConsoleComponent> ent, ref BoardingTeleportBackMessage args)

    {

        ent.Comp.Page = BoardingTeleportPage.Sector;

        ent.Comp.LandingCoordinates = null;
        ent.Comp.PlatformLandings.Clear();
        ent.Comp.LockEstablishedAt = null;
        _lock.ResetLockTiming(ent.Comp);
        ent.Comp.Status = ent.Comp.TargetGrid == null ? BoardingTeleportStatus.None : BoardingTeleportStatus.TargetSelected;

        Dirty(ent);

        UpdateUi(ent);

    }



    private void OnClearTarget(Entity<BoardingTeleportConsoleComponent> ent, ref BoardingTeleportClearTargetMessage args)

    {

        ent.Comp.TargetGrid = null;

        ent.Comp.LandingCoordinates = null;
        ent.Comp.PlatformLandings.Clear();
        ent.Comp.LockEstablishedAt = null;
        _lock.ResetLockTiming(ent.Comp);

        ent.Comp.Page = BoardingTeleportPage.Sector;

        ent.Comp.Status = BoardingTeleportStatus.None;

        Dirty(ent);

        UpdateUi(ent);

    }



    private void OnSelectMode(Entity<BoardingTeleportConsoleComponent> ent, ref BoardingTeleportSelectModeMessage args)

    {

        if (ent.Comp.Mode == args.Mode)

            return;



        ent.Comp.Mode = args.Mode;

        Dirty(ent);

        UpdateUi(ent);

    }



    public void UpdateUi(Entity<BoardingTeleportConsoleComponent> ent)

    {

        if (!_ui.HasUi(ent.Owner, BoardingTeleportUiKey.Key))

            return;



        var navState = GetNavState(ent.Owner);

        var mapState = new ShuttleMapInterfaceState(

            FTLState.Available,

            default,

            new List<ShuttleBeaconObject>(),

            new List<ShuttleExclusionObject>(),

            default,

            ShuttleBioScanStatus.None,

            true);



        TryGetPrimaryPlatform(ent.Owner, out var platformUid, out var platform);

        var distanceScale = GetDistanceScale(ent);

        var experimental = platform?.ExperimentalRisk ?? false;

        var experimentalScatter = platform?.ExperimentalScatterControl ?? false;

        var apcRatio = platformUid is { } pUid && TryComp<ApcPowerReceiverComponent>(pUid, out var apc)

            ? BoardingTeleportBalance.GetApcReceivedRatio(apc.PowerReceived, apc.Load)

            : null;



        float? cooldownSeconds = null;

        if (platform != null && _timing.CurTime < platform.NextUse)

            cooldownSeconds = (float) (platform.NextUse - _timing.CurTime).TotalSeconds;

        _engine.TryLinkConsole(ent);

        float? engineRange = null;
        float? engineMaxTargetVelocity = null;
        if (_engine.TryGetLinkedEngine(ent.Owner, ent.Comp, out _, out var linkedEngine))
        {
            engineRange = linkedEngine.Range;
            engineMaxTargetVelocity = linkedEngine.MaxTargetVelocity;
        }

        _lock.GetLockPenalties(ent.Comp, out var lockScatterPenalty, out var lockRiskPenalty, out _);
        var displayScatter = GetModeScatter(ent, ent.Comp.Mode);
        displayScatter = BoardingTeleportBalance.ApplyLockScatterPenalty(displayScatter, lockScatterPenalty);
        if (ent.Comp.TargetGrid is { } uiTarget && _lock.TryGetScramblerEffect(uiTarget, out var uiScrambler))
            displayScatter = BoardingTeleportBalance.ApplyScramblerScatterBonus(displayScatter, uiScrambler.ScatterBonus);

        var displayRisk = BoardingTeleportBalance.ComputeDisplayedRiskPercent(ent.Comp.Mode, distanceScale, experimental);
        displayRisk = BoardingTeleportBalance.ApplyLockRiskPenalty(displayRisk / 100f, lockRiskPenalty) * 100f;
        if (ent.Comp.TargetGrid is { } uiTargetRisk && _lock.TryGetScramblerEffect(uiTargetRisk, out var uiScramblerRisk))
            displayRisk = BoardingTeleportBalance.ApplyScramblerRiskBonus(displayRisk / 100f, uiScramblerRisk.RiskBonus) * 100f;

        var lockAge = _lock.GetEffectiveLockAge(ent.Comp);
        var platforms = BuildPlatformUiEntries(ent.Owner, ent.Comp);

        var state = new BoardingTeleportBoundUserInterfaceState(

            ent.Comp.Page,

            navState,

            mapState,

            ent.Comp.TargetGrid is { } targetGrid ? GetNetEntity(targetGrid) : null,

            ent.Comp.LandingCoordinates is { } landing ? GetNetCoordinates(landing) : null,

            ent.Comp.Status,

            ent.Comp.Mode,

            GetModeDelay(ent, ent.Comp.Mode),

            displayScatter,

            displayRisk,

            BoardingTeleportBalance.GetApcRiskBonus(apcRatio) * 100f,

            cooldownSeconds,

            platform?.ReturnWindowSeconds ?? BoardingTeleportPlatformComponent.DefaultReturnWindowSeconds,

            engineRange,

            engineMaxTargetVelocity,

            lockAge,

            lockScatterPenalty,

            lockRiskPenalty * 100f,

            ent.Comp.SelectedPlatformSlot,

            ent.Comp.UseSharedLandingZone,

            GetSelectedLandingNet(ent.Comp),

            platforms);



        _ui.SetUiState(ent.Owner, BoardingTeleportUiKey.Key, state);

    }



    public bool TryValidateDeparture(

        EntityUid consoleUid,

        BoardingTeleportConsoleComponent console,

        EntityCoordinates landing,

        out BoardingTeleportStatus status)

    {

        status = BoardingTeleportStatus.None;



        if (!TryGetScannerGrid(consoleUid, out var scannerGrid))

        {

            status = BoardingTeleportStatus.NoGrid;

            return false;

        }

        if (BoardingTeleportShieldHelper.HasActiveTeleportImmuneShield(EntityManager, scannerGrid))
        {
            status = BoardingTeleportStatus.SourceShieldBlocksTeleport;
            return false;
        }

        _engine.TryLinkConsole((consoleUid, console));

        if (!TryValidateEngine(consoleUid, console, out status))
            return false;

        _lock.GetLockPenalties(console, out _, out _, out var lockExpired);
        if (lockExpired)
        {
            status = BoardingTeleportStatus.LockExpired;
            return false;
        }

        if (!_engine.TryGetLinkedEngine(consoleUid, console, out _, out var engine))
        {
            status = BoardingTeleportStatus.NoEngine;
            return false;
        }



        if (console.TargetGrid is not { } targetGrid ||

            !TryValidateTargetGrid((consoleUid, console), scannerGrid, targetGrid, engine, out status))

        {

            return false;

        }



        if (!TryValidateLandingCoordinates(landing, out status))

            return false;



        if (landing.EntityId != targetGrid)

        {

            status = BoardingTeleportStatus.InvalidLanding;

            return false;

        }



        return true;

    }



    public bool TryResolveLanding(

        EntityUid platformUid,

        BoardingTeleportPlatformComponent platform,

        BoardingTeleportConsoleComponent console,

        EntityCoordinates center,

        BoardingTeleportInsertionMode mode,

        float distanceScale,

        out EntityCoordinates landing)

    {

        landing = EntityCoordinates.Invalid;



        if (!TryValidateDeparture(platform.LinkedConsole ?? EntityUid.Invalid, console, center, out _))

            return false;



        var experimentalScatter = platform.ExperimentalScatterControl;
        if (platform.LinkedConsole is { } linkedConsole &&
            _engine.TryGetLinkedEngine(linkedConsole, console, out _, out var linkedEngine))
        {
            experimentalScatter |= linkedEngine.ExperimentalPhaseShift;
        }

        ApplyLockAndScramblerToBalance(console, console.TargetGrid, out var lockScatter, out var lockRisk, out var scramScatter, out var scramRisk, out _);

        var scatterRadius = BoardingTeleportBalance.ComputeScatterRadius(mode, distanceScale, experimentalScatter);
        scatterRadius = BoardingTeleportBalance.ApplyLockScatterPenalty(scatterRadius, lockScatter);
        scatterRadius = BoardingTeleportBalance.ApplyScramblerScatterBonus(scatterRadius, scramScatter);

        if (experimentalScatter && TryFindPhaseShiftLanding(center, out landing))

            return true;



        if (TryFindScatteredLanding(center, scatterRadius, out landing))

            return true;



        if (TryValidateLandingCoordinates(center, out _))

        {

            landing = center;

            return true;

        }



        return false;

    }



    public bool TryResolveHome(
        EntityUid platformUid,
        BoardingTeleportPlatformComponent platform,
        BoardingTeleportAnchorComponent anchor,
        EntityUid user,
        out EntityCoordinates home,
        out bool homeUnreachable,
        bool allowExpired = false)
    {
        home = EntityCoordinates.Invalid;
        homeUnreachable = false;

        var homePlatform = GetEntity(anchor.HomePlatform);
        if (!Exists(homePlatform))
        {
            homeUnreachable = true;
            return false;
        }

        if (homePlatform != platformUid)
            return false;

        if (!allowExpired && anchor.ExpiresAt is { } expires && _timing.CurTime >= expires)
            return false;

        home = Transform(homePlatform).Coordinates;

        if (!home.IsValid(EntityManager) || !Exists(home.EntityId))
        {
            homeUnreachable = true;
            return false;
        }

        return true;
    }



    public bool TryValidateLandingCoordinates(EntityCoordinates coordinates, out BoardingTeleportStatus status)

    {

        status = BoardingTeleportStatus.InvalidLanding;



        if (!coordinates.IsValid(EntityManager) || !Exists(coordinates.EntityId))

            return false;



        if (!TryComp<MapGridComponent>(coordinates.EntityId, out var grid))

            return false;



        var tile = _map.CoordinatesToTile(coordinates.EntityId, grid, coordinates);

        return TryGetLandingCoordinates(coordinates.EntityId, tile, out _);

    }



    public bool TryFindScatteredLanding(EntityCoordinates center, float radius, out EntityCoordinates result)
    {
        result = EntityCoordinates.Invalid;

        if (!TryComp<MapGridComponent>(center.EntityId, out var grid))
            return false;

        var originTile = _map.CoordinatesToTile(center.EntityId, grid, center);

        if (radius <= 0.01f)
            return TryGetLandingCoordinates(center.EntityId, originTile, out result);

        // Pick a random tile inside a disk (radius in tile/meter units — matches the UI scatter circle).
        for (var i = 0; i < BoardingTeleportConstants.ScatterSampleAttempts; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var dist = MathF.Sqrt(_random.NextFloat()) * radius;
            var offset = new Vector2i(
                (int) MathF.Round(dist * MathF.Cos(angle)),
                (int) MathF.Round(dist * MathF.Sin(angle)));

            if (offset == Vector2i.Zero && i < BoardingTeleportConstants.ScatterSampleAttempts - 1)
                continue;

            var tile = originTile + offset;
            if (TryGetLandingCoordinates(center.EntityId, tile, out result))
                return true;
        }

        return TryGetLandingCoordinates(center.EntityId, originTile, out result);
    }



    /// <summary>

    /// Experimental: snap to a random cardinal-adjacent tile instead of a continuous offset.

    /// </summary>

    public bool TryFindPhaseShiftLanding(EntityCoordinates center, out EntityCoordinates result)

    {

        result = center;



        if (!TryComp<MapGridComponent>(center.EntityId, out var grid))

            return false;



        var originTile = _map.CoordinatesToTile(center.EntityId, grid, center);

        Span<Vector2i> neighbors = stackalloc Vector2i[]

        {

            originTile + Vector2i.Up,

            originTile + Vector2i.Down,

            originTile + Vector2i.Left,

            originTile + Vector2i.Right,

        };



        for (var attempt = 0; attempt < BoardingTeleportConstants.PhaseShiftNeighborAttempts; attempt++)

        {

            var tile = neighbors[_random.Next(neighbors.Length)];

            if (TryGetLandingCoordinates(center.EntityId, tile, out var coords))

            {

                result = coords;

                return true;

            }

        }



        return TryValidateLandingCoordinates(center, out _) && TryGetLandingCoordinates(center.EntityId, originTile, out result);

    }



    public float GetModeDelay(Entity<BoardingTeleportConsoleComponent> ent, BoardingTeleportInsertionMode mode)

    {

        var baseDelay = 10f;

        if (TryGetPrimaryPlatform(ent.Owner, out _, out var platform) && platform != null)

            baseDelay = platform.DepartureDelay;



        return BoardingTeleportBalance.ComputeDepartureDelay(baseDelay, mode, GetDistanceScale(ent), returning: false);

    }



    public float GetModeScatter(Entity<BoardingTeleportConsoleComponent> ent, BoardingTeleportInsertionMode mode)

    {

        var experimentalScatter = TryGetPrimaryPlatform(ent.Owner, out _, out var platform) && platform is { ExperimentalScatterControl: true };

        return BoardingTeleportBalance.ComputeScatterRadius(mode, GetDistanceScale(ent), experimentalScatter);

    }



    public float GetDistanceScale(EntityUid fromUid, EntityUid? targetGrid)

    {

        if (targetGrid is not { } grid || !Exists(grid))

            return 1f;



        var from = _transform.GetWorldPosition(fromUid);

        var to = _transform.GetWorldPosition(grid);

        return BoardingTeleportBalance.GetDistanceScale(Vector2.Distance(from, to));

    }



    public float GetDistanceScale(EntityUid platformUid, EntityCoordinates destination)

    {

        var from = _transform.GetMapCoordinates(platformUid);

        var to = destination.ToMap(EntityManager, _transform);

        if (from.MapId != to.MapId)

            return BoardingTeleportConstants.MaxDistanceScale;



        return BoardingTeleportBalance.GetDistanceScale(Vector2.Distance(from.Position, to.Position));

    }



    public float GetDestabilizationChance(

        BoardingTeleportInsertionMode mode,

        float distanceScale,

        BoardingTeleportConsoleComponent? console,

        EntityUid? consoleUid,

        EntityUid platformUid,

        bool experimental = false)

    {

        float? apcRatio = null;

        if (TryComp<ApcPowerReceiverComponent>(platformUid, out var apc))

            apcRatio = BoardingTeleportBalance.GetApcReceivedRatio(apc.PowerReceived, apc.Load);



        var linearVel = 0f;

        var angularVel = 0f;

        var maxLinear = BoardingTeleportConstants.DefaultMaxTargetVelocity;

        var maxAngular = BoardingTeleportConstants.DefaultMaxTargetAngularVelocity;



        if (console != null &&

            consoleUid is { } validConsoleUid &&

            _engine.TryGetLinkedEngine(validConsoleUid, console, out _, out var engine))

        {

            maxLinear = engine.MaxTargetVelocity * engine.VelocityToleranceMultiplier;

            maxAngular = engine.MaxTargetAngularVelocity * engine.VelocityToleranceMultiplier;

            experimental |= engine.ExperimentalRiskBoost;

            if (console.TargetGrid is { } targetGrid &&

                TryComp<PhysicsComponent>(targetGrid, out var targetBody))

            {

                linearVel = targetBody.LinearVelocity.Length();

                angularVel = targetBody.AngularVelocity;

            }

        }



        var chance = BoardingTeleportBalance.ComputeDestabilizationChance(

            mode,

            distanceScale,

            experimental,

            apcRatio,

            linearVel,

            angularVel,

            maxLinear,

            maxAngular);

        if (console != null)
        {
            ApplyLockAndScramblerToBalance(console, console.TargetGrid, out _, out var lockRisk, out _, out var scramRisk, out _);
            chance = BoardingTeleportBalance.ApplyLockRiskPenalty(chance, lockRisk);
            chance = BoardingTeleportBalance.ApplyScramblerRiskBonus(chance, scramRisk);
        }

        return chance;

    }



    private NavInterfaceState GetNavState(EntityUid uid)

    {

        var docks = _shuttleConsole.GetAllDocks();

        if (!TryComp<RadarConsoleComponent>(uid, out var radar) ||

            !TryComp<TransformComponent>(uid, out var xform))

        {

            return new NavInterfaceState(SharedRadarConsoleSystem.DefaultMaxRange, null, null, docks, Shared._NF.Shuttles.Events.InertiaDampeningMode.Dampen);

        }



        return _shuttleConsole.GetNavState((uid, radar, xform), docks);

    }



    private void SyncPlatforms(Entity<BoardingTeleportConsoleComponent> ent)

    {

        if (!TryComp<DeviceListComponent>(ent, out var deviceList))

            return;

        foreach (var device in _deviceList.GetAllDevices(ent.Owner, deviceList))

        {

            if (!TryComp<BoardingTeleportPlatformComponent>(device, out var platform))

                continue;



            platform.LinkedConsole = ent.Owner;

            Dirty(device, platform);

        }

    }



    public bool TryGetScannerGrid(EntityUid uid, out EntityUid scannerGrid)

    {

        var xform = Transform(uid);

        if (xform.GridUid == null)

        {

            scannerGrid = default;

            return false;

        }



        scannerGrid = xform.GridUid.Value;

        return true;

    }



    private bool TryGetPrimaryPlatform(EntityUid consoleUid, out EntityUid platformUid, out BoardingTeleportPlatformComponent? platform)

    {

        if (!TryComp<DeviceListComponent>(consoleUid, out var deviceList))

        {

            platformUid = default;

            platform = null;

            return false;

        }

        foreach (var device in _deviceList.GetAllDevices(consoleUid, deviceList))

        {

            if (TryComp<BoardingTeleportPlatformComponent>(device, out platform))

            {

                platformUid = device;

                return true;

            }

        }



        platformUid = default;

        platform = null;

        return false;

    }



    private bool TryGetTargetGrid(MapCoordinates coordinates, out EntityUid targetGrid)

    {

        if (_mapManager.TryFindGridAt(coordinates, out targetGrid, out _))

            return true;



        targetGrid = default;

        return false;

    }



    public bool TryValidateTargetGrid(

        Entity<BoardingTeleportConsoleComponent> ent,

        EntityUid scannerGrid,

        EntityUid targetGrid,

        BoardingTeleportEngineComponent engine,

        out BoardingTeleportStatus status)

    {

        if (!Exists(targetGrid) || !HasComp<MapGridComponent>(targetGrid))

        {

            status = BoardingTeleportStatus.InvalidTarget;

            return false;

        }



        if (targetGrid == scannerGrid)

        {

            status = BoardingTeleportStatus.InvalidTarget;

            return false;

        }

        // Cross-map locks would hit admin arenas / dormant ADS maps — same map only.
        if (_transform.GetMapId(scannerGrid) != _transform.GetMapId(targetGrid))

        {

            status = BoardingTeleportStatus.InvalidTarget;

            return false;

        }

        if (IsGridInActiveFtl(targetGrid))

        {

            status = BoardingTeleportStatus.TargetInFtl;

            return false;

        }

        if (!BoardingTeleportShieldHelper.CanEngineBypassTargetShield(EntityManager, targetGrid, engine, out _))

        {

            status = BoardingTeleportShieldHelper.HasActiveTeleportImmuneShield(EntityManager, targetGrid)
                ? BoardingTeleportStatus.TargetShielded
                : BoardingTeleportStatus.TargetShieldTooStrong;

            return false;

        }

        if (_lock.IsFriendlyTarget(scannerGrid, targetGrid, engine.BlockFriendlyTargets))

        {

            status = BoardingTeleportStatus.TargetFriendly;

            return false;

        }

        if (_lock.TryGetScramblerEffect(targetGrid, out var scrambler) && scrambler.BlocksLock)

        {

            status = BoardingTeleportStatus.TargetScrambled;

            return false;

        }



        if (engine.MinimumTargetMass > 0f &&

            (!TryComp<PhysicsComponent>(targetGrid, out var massBody) ||

             massBody.Mass < engine.MinimumTargetMass))

        {

            status = BoardingTeleportStatus.InvalidTarget;

            return false;

        }



        var scannerPos = _transform.GetWorldPosition(scannerGrid);

        var targetPos = _transform.GetWorldPosition(targetGrid);

        if (Vector2.Distance(scannerPos, targetPos) > engine.Range)

        {

            status = BoardingTeleportStatus.TargetTooFar;

            return false;

        }



        if (TryComp<PhysicsComponent>(targetGrid, out var body) &&

            (body.LinearVelocity.LengthSquared() > MathF.Pow(engine.MaxTargetVelocity * engine.VelocityToleranceMultiplier, 2) ||

             MathF.Abs(body.AngularVelocity) > engine.MaxTargetAngularVelocity * engine.VelocityToleranceMultiplier))

        {

            status = BoardingTeleportStatus.TargetMoving;

            return false;

        }



        status = BoardingTeleportStatus.None;

        return true;

    }



    private bool IsGridInActiveFtl(EntityUid gridUid)

    {

        if (!TryComp<FTLComponent>(gridUid, out var ftl))

            return false;



        return (ftl.State & (FTLState.Starting | FTLState.Travelling | FTLState.Arriving)) != 0;

    }



    private void SelectTargetGrid(Entity<BoardingTeleportConsoleComponent> ent, EntityUid targetGrid)

    {

        var previousTarget = ent.Comp.TargetGrid;

        ent.Comp.TargetGrid = targetGrid;

        ent.Comp.LandingCoordinates = null;
        ent.Comp.LockEstablishedAt = null;
        _lock.ResetLockTiming(ent.Comp);

        if (previousTarget != null && previousTarget != targetGrid)
            StartEngineCooldown(ent.Owner, ent.Comp);

        ent.Comp.Page = BoardingTeleportPage.Grid;

        ent.Comp.Status = BoardingTeleportStatus.TargetSelected;

        EnsureComp<NavMapComponent>(targetGrid);

        Dirty(ent);

        UpdateUi(ent);

    }



    public bool TryGetLandingCoordinates(EntityUid gridUid, Vector2i tile, out EntityCoordinates coordinates)

    {

        coordinates = EntityCoordinates.Invalid;



        if (!TryComp<MapGridComponent>(gridUid, out var grid))

            return false;

        if (!_map.TryGetTileRef(gridUid, grid, tile, out _))

            return false;



        coordinates = _map.GridTileToLocal(gridUid, grid, tile);

        return true;

    }



    private void SetStatus(Entity<BoardingTeleportConsoleComponent> ent, BoardingTeleportStatus status)

    {

        ent.Comp.Status = status;

        Dirty(ent);

        _popup.PopupEntity(Loc.GetString($"boarding-teleport-status-{status}"), ent.Owner);

        UpdateUi(ent);

    }



    private float GetDistanceScale(Entity<BoardingTeleportConsoleComponent> ent)

    {

        if (!TryGetScannerGrid(ent.Owner, out var scannerGrid))

            return 1f;



        return GetDistanceScale(scannerGrid, ent.Comp.TargetGrid);

    }

}

