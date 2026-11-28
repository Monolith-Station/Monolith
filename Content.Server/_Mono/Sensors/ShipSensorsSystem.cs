using System.Numerics;
using Content.Server._Mono.Detection;
using Content.Server._Mono.Radar;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Content.Shared._Mono.Sensors.Events;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sensors;

public sealed partial class ShipSensorsSystem : EntitySystem
{
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private RadarCountermeasureSystem _chaff = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private const int MaxDatalinkCodes = 8;

    private EntityQuery<ShipSensorComponent> _sensorQuery;
    private EntityQuery<ApcPowerReceiverComponent> _powerQuery;

    private readonly Dictionary<EntityUid, float> _emissionsCache = new();
    private readonly Dictionary<EntityUid, float> _thermalCache = new();
    private readonly Dictionary<EntityUid, float> _diagonalCache = new();

    private readonly Dictionary<EntityUid, Vector2> _gridWorldPos = new();
    private readonly Dictionary<EntityUid, MapId> _gridMapId = new();
    private readonly Dictionary<EntityUid, Angle> _gridWorldRot = new();

    private readonly Dictionary<EntityUid, List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)>> _hardwareByGrid = new();
    private readonly Dictionary<MapId, List<EntityUid>> _gridsByMap = new();
    private readonly HashSet<(EntityUid Illuminator, EntityUid Locked)> _illuminationPairs = new();

    private readonly List<EntityUid> _observerGrids = new();

    private readonly Dictionary<string, List<(EntityUid Uid, GridSensorsComponent Comp)>> _datalinkByCode = new();
    private readonly Dictionary<NetEntity, SensorContact> _datalinkUnion = new();

    private readonly List<(EntityUid Uid, RadarCountermeasureComponent Comp, Vector2 WorldPos, MapId MapId)> _activeDecoys = new();
    private readonly List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> _emptyHardware = new();

    private float _globalMaxDiagonal;
    private float _globalMaxEmissions;
    private float _globalMaxThermal;

    public override void Initialize()
    {
        base.Initialize();

        _sensorQuery = GetEntityQuery<ShipSensorComponent>();
        _powerQuery = GetEntityQuery<ApcPowerReceiverComponent>();

        SubscribeLocalEvent<GridInitializeEvent>(OnGridInitialized);
        SubscribeLocalEvent<ShipSensorComponent, MapInitEvent>(OnSensorMapInit);
        SubscribeLocalEvent<ShipSensorComponent, GetThermalSignatureEvent>(OnSensorThermalSignature);

        SubscribeLocalEvent<FireControlConsoleComponent, GetRadarSourcesEvent>(OnGetRadarSources);
        SubscribeLocalEvent<ShuttleConsoleComponent, GetRadarSourcesEvent>(OnGetRadarSources);

        Subs.BuiEvents<FireControlConsoleComponent>(SensorsUiKey.Key,
            subs =>
            {
                subs.Event<SensorsToggleHardwareMessage>(OnToggleHardware);
            });

        Subs.BuiEvents<ShuttleConsoleComponent>(SensorsUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUIOpened);
                subs.Event<SensorsToggleHardwareMessage>(OnToggleHardware);
            });

        Subs.BuiEvents<RadarConsoleComponent>(RadarConsoleUiKey.Key,
            subs =>
            {
                subs.Event<SensorsSetEmconMessage>((uid, _, args) =>
                {
                    if (TrySetEmcon(uid, args))
                        RaiseLocalEvent(uid, new SensorsConsoleRefreshEvent());
                });
                subs.Event<SensorsSetArcMessage>((uid, _, args) =>
                {
                    if (TrySetArc(uid, args))
                        RaiseLocalEvent(uid, new SensorsConsoleRefreshEvent());
                });
                subs.Event<SensorsSetDatalinkMessage>((uid, _, args) =>
                {
                    if (TrySetDatalink(uid, args))
                        RaiseLocalEvent(uid, new SensorsConsoleRefreshEvent());
                });
            });
    }

    private void OnGridInitialized(GridInitializeEvent args)
    {
        EnsureComp<GridSensorsComponent>(args.EntityUid);
        EnsureComp<DetectionRangeMultiplierComponent>(args.EntityUid);
    }

    private void OnSensorMapInit(Entity<ShipSensorComponent> ent, ref MapInitEvent args)
    {
        UpdateSensorPowerLoad(ent);
    }

    private void OnSensorThermalSignature(Entity<ShipSensorComponent> ent, ref GetThermalSignatureEvent args)
    {
        if (!ent.Comp.Enabled || !IsSensorPowered(ent) || !ent.Comp.ActiveMode)
            return;

        if (ent.Comp.SensorType is SensorType.ActiveRadar)
            args.Signature += ent.Comp.ActiveThermalSignature;
    }

    private void OnGetRadarSources(EntityUid uid, object component, ref GetRadarSourcesEvent args)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid == null)
            return;

        args.Sources ??= new List<EntityUid>();

        if (_hardwareByGrid.TryGetValue(gridUid.Value, out var hardware) && hardware.Count > 0)
        {
            foreach (var (sensUid, _, _) in hardware)
                args.Sources.Add(sensUid);
        }
        else
        {
            var query = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
            while (query.MoveNext(out var sensUid, out var sens, out var xform))
            {
                if (xform.GridUid != gridUid)
                    continue;
                if (!sens.Enabled || !IsSensorPowered(sensUid))
                    continue;

                args.Sources.Add(sensUid);
            }
        }

        if (args.Sources.Count == 0)
            args.Sources.Add(gridUid.Value);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        BuildSignatureCaches();
        BuildIlluminationCache();
        BuildDecoyCache();

        _observerGrids.Clear();

        var gridQuery = EntityQueryEnumerator<GridSensorsComponent, MapGridComponent, TransformComponent, DetectionRangeMultiplierComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var gridSensors, out _, out var xform, out var rangeMul))
        {
            _hardwareByGrid.TryGetValue(gridUid, out var hardware);
            hardware ??= _emptyHardware;

            if (!gridSensors.GrantBasicSensors && hardware.Count == 0)
                continue;

            UpdateGridSensors(gridUid, gridSensors, xform, rangeMul);
            _observerGrids.Add(gridUid);
        }

        PropagateDatalinks();
        RefreshOpenConsoles();
    }
}
