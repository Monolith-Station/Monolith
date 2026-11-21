using System.Linq;
using Content.Server._Mono.Detection;
using Content.Server._Mono.Radar;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Content.Shared._Mono.Sensors.Events;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sensors;

/// <summary>
/// this handles things
/// </summary>
public sealed partial class ShipSensorsSystem : EntitySystem
{
    [Dependency] private DetectionSystem _detection = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private const int MaxDatalinkCodes = 8;

    private EntityQuery<ShipSensorComponent> _sensorQuery;
    private EntityQuery<ApcPowerReceiverComponent> _powerQuery;

    public override void Initialize()
    {
        base.Initialize();

        _sensorQuery = GetEntityQuery<ShipSensorComponent>();
        _powerQuery = GetEntityQuery<ApcPowerReceiverComponent>();

        SubscribeLocalEvent<GridInitializeEvent>(OnGridInitialized);
        SubscribeLocalEvent<ShipSensorComponent, MapInitEvent>(OnSensorMapInit);
        SubscribeLocalEvent<ShipSensorComponent, GetThermalSignatureEvent>(OnSensorThermalSignature);

        SubscribeLocalEvent<FireControlConsoleComponent, GetRadarSourcesEvent>(OnGetRadarSources);
        // Mono: ShuttleConsoleComponent is outside _Mono
        SubscribeLocalEvent<ShuttleConsoleComponent, GetRadarSourcesEvent>(OnGetRadarSources);

        Subs.BuiEvents<FireControlConsoleComponent>(SensorsUiKey.Key,
            subs =>
        {
            subs.Event<SensorsToggleHardwareMessage>(OnToggleHardware);
        });

        // Mono: shuttle console BUI
        Subs.BuiEvents<ShuttleConsoleComponent>(SensorsUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUIOpened);
            subs.Event<SensorsSetDatalinkMessage>(OnSetDatalink);
            subs.Event<SensorsToggleHardwareMessage>(OnToggleHardware);
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

        if (ent.Comp.SensorType is SensorType.ActiveRadar or SensorType.Jammer)
            args.Signature += ent.Comp.ActiveThermalSignature;
    }

    private void OnGetRadarSources(EntityUid uid, object component, ref GetRadarSourcesEvent args)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid == null)
            return;

        args.Sources ??= new List<EntityUid>();

        var query = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (query.MoveNext(out var sensUid, out var sens, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            if (!sens.Enabled || !IsSensorPowered(sensUid))
                continue;
            if (sens.SensorType == SensorType.Jammer)
                continue;

            args.Sources.Add(sensUid);
        }

        if (args.Sources.Count == 0)
            args.Sources.Add(gridUid.Value);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var gridQuery = EntityQueryEnumerator<GridSensorsComponent, MapGridComponent, TransformComponent, DetectionRangeMultiplierComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var gridSensors, out _, out var xform, out var rangeMul))
        {
            UpdateGridSensors(gridUid, gridSensors, xform, rangeMul);
        }

        PropagateDatalinks();
    }

    private void UpdateGridSensors(
        EntityUid gridUid,
        GridSensorsComponent sensors,
        TransformComponent xform,
        DetectionRangeMultiplierComponent rangeMul)
    {
        var ourPos = _xform.GetWorldPosition(xform);
        var now = _timing.CurTime;

        var hardware = new List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)>();

        var sensEnum = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (sensEnum.MoveNext(out var sensUid, out var sens, out var sensXform))
        {
            if (sensXform.GridUid != gridUid)
                continue;

            UpdateSensorPowerLoad((sensUid, sens));

            if (!sens.Enabled || !IsSensorPowered(sensUid))
                continue;

            hardware.Add((sensUid, sens, sensXform));
        }

        // Omni aggregate written onto the grid for DetectionSystem + any other consumers.
        ApplyOmniMultipliers(rangeMul, sensors, hardware);
        Dirty(gridUid, rangeMul);

        var newContacts = new Dictionary<NetEntity, SensorContact>();

        var otherEnum = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (otherEnum.MoveNext(out var otherUid, out var otherGrid, out var otherXform))
        {
            if (otherUid == gridUid || otherXform.MapID != xform.MapID)
                continue;

            var otherPos = _xform.GetWorldPosition(otherXform);
            var delta = otherPos - ourPos;
            var dist = delta.Length();
            if (dist <= 0.001f)
                continue;

            var bearing = delta.ToWorldAngle();

            // Per-bearing multipliers so arcs matter; push onto grid then call stock IsGridDetected.
            ApplyDirectionalMultipliers(rangeMul, sensors, hardware, bearing);

            var passiveRadar = GetBestSensitivity(hardware, SensorType.PassiveRadar, bearing, activeOnly: false);
            var activeRadar = GetBestSensitivity(hardware, SensorType.ActiveRadar, bearing, activeOnly: true);
            // Active arrays in standby still contribute reduced passive-like radar
            var standbyActive = GetBestSensitivity(hardware, SensorType.ActiveRadar, bearing, activeOnly: false, requireInactive: true);
            passiveRadar = MathF.Max(passiveRadar, standbyActive * 0.5f);
            if (sensors.GrantBasicSensors)
                passiveRadar = MathF.Max(passiveRadar, sensors.BasicPassiveRadarMultiplier);

            var visualIrLevel = _detection.IsGridDetected((otherUid, otherGrid), gridUid);
            var passiveSens = GetBestSensitivity(hardware, SensorType.PassiveRadar, bearing, activeOnly: false);
            var emissions = GetTargetEmissions(otherUid);
            // For the noobs
            // Passive radar is like hiding in a dark room
            // Active radar is like a flashlight (or just a lantern I guess, that's more fitting for omnidirectional)
            // Jamming is like throwing a fucking flashbang
            var esmTrack = passiveSens * emissions.ActiveRadarWatts + passiveSens * emissions.JamStrength * 30;
            var activeSens = GetBestSensitivity(hardware,
                SensorType.ActiveRadar,
                bearing,
                activeOnly: true);
            var activeTrack = activeSens > 0f
                              && RadarRange(otherGrid, activeSens) >= dist;
            var radarLevel = EvaluateRadarLevel(otherGrid, dist, passiveRadar, activeRadar);
            var level = visualIrLevel;
            if (activeTrack || esmTrack >= dist)
                level = BestLevel(level, DetectionLevel.Detected);

            if (level == DetectionLevel.Undetected)
                continue;

            var flags = SensorContactFlags.None;

            if (visualIrLevel != DetectionLevel.Undetected)
                flags |= SensorContactFlags.VisualIr;

            if (esmTrack >= dist)      // they emit + you have passive
                flags |= SensorContactFlags.PassiveRadar;

            if (activeTrack)   // you emit + reflected track
                flags |= SensorContactFlags.ActiveRadar;

            if (IsBearingJammed(hardware, bearing))
                flags |= SensorContactFlags.Jammed;

            // Re-apply directional so the check above matched; flags for radar:
            if (radarLevel != DetectionLevel.Undetected)
            {
                if (activeRadar > 0f && RadarRange(otherGrid, activeRadar) >= dist)
                    flags |= SensorContactFlags.ActiveRadar;
                else if (passiveRadar > 0f && RadarRange(otherGrid, passiveRadar) >= dist)
                    flags |= SensorContactFlags.PassiveRadar;
            }

            if (IsBearingJammed(hardware, bearing))
                flags |= SensorContactFlags.Jammed;

            var netOther = GetNetEntity(otherUid);
            newContacts[netOther] = new SensorContact
            {
                TargetGrid = netOther,
                WorldPosition = otherPos,
                Range = dist,
                Level = level,
                Flags = flags,
                LastSeen = now,
                Designation = MetaData(otherUid).EntityName
            };
        }

        // Restore omni multipliers for external readers between ticks
        ApplyOmniMultipliers(rangeMul, sensors, hardware);
        Dirty(gridUid, rangeMul);

        foreach (var (key, old) in sensors.Contacts)
        {
            if (newContacts.ContainsKey(key))
                continue;
            if (now - old.LastSeen <= sensors.ContactStaleTime)
                newContacts[key] = old;
        }

        sensors.Contacts = newContacts;
        Dirty(gridUid, sensors);

        RefreshOpenConsoles(gridUid);
    }

    private void RefreshOpenConsoles(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<FireControlConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (!_ui.IsUiOpen(consoleUid, FireControlConsoleUiKey.Key))
                continue;

            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }
    }

    /// <summary>
    ///     Writes max visual/IR (and omni radar) into the grid's DetectionRangeMultiplierComponent.
    /// </summary>
    private void ApplyOmniMultipliers(
        DetectionRangeMultiplierComponent rangeMul,
        GridSensorsComponent sensors,
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware)
    {
        var visual = sensors.GrantBasicSensors ? sensors.BasicVisualMultiplier : 0f;
        var infrared = sensors.GrantBasicSensors ? sensors.BasicInfraredMultiplier : 0f;

        // Omni IR/visual: only sensors with full 360 arcs, plus basics
        foreach (var (uid, sens, sensXform) in hardware)
        {
            if (sens.SensorType is not (SensorType.Infrared))
                continue;
            if (sens.ArcDegrees < 360f)
                continue;
            if (IsSensorJammedEntity(uid, sens, sensXform))
                continue;
            infrared = MathF.Max(infrared, sens.Sensitivity);
        }

        rangeMul.VisualMultiplier = visual;
        rangeMul.InfraredMultiplier = infrared;
    }

    private void ApplyDirectionalMultipliers(
        DetectionRangeMultiplierComponent rangeMul,
        GridSensorsComponent sensors,
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware,
        Angle bearing)
    {
        var visual = sensors.GrantBasicSensors ? sensors.BasicVisualMultiplier : 0f;
        var infrared = sensors.GrantBasicSensors ? sensors.BasicInfraredMultiplier : 0f;

        foreach (var (uid, sens, sensXform) in hardware)
        {
            if (sens.SensorType is not SensorType.Infrared)
                continue;
            if (!InArc(sens, sensXform, bearing))
                continue;
            if (IsSensorJammedEntity(uid, sens, sensXform))
                continue;

            infrared = MathF.Max(infrared, sens.Sensitivity);
        }

        rangeMul.VisualMultiplier = visual;
        rangeMul.InfraredMultiplier = infrared;
    }

    /// <summary>
    ///     Radar range using the same grid-diagonal proxy DetectionSystem uses for visual signature.
    /// </summary>
    private static float RadarRange(MapGridComponent grid, float multiplier)
    {
        if (multiplier <= 0f)
            return 0f;

        var aabb = grid.LocalAABB;
        var diagonal = MathF.Sqrt(aabb.Width * aabb.Width + aabb.Height * aabb.Height);
        return diagonal * multiplier;
    }

    private DetectionLevel EvaluateRadarLevel(
        MapGridComponent targetGrid,
        float distance,
        float passiveMul,
        float activeMul)
    {
        var range = MathF.Max(RadarRange(targetGrid, passiveMul), RadarRange(targetGrid, activeMul));
        if (range <= 0f || distance > range)
            return DetectionLevel.Undetected;

        // Treat radar track as full Detected (outline-equivalent).
        return DetectionLevel.Detected;
    }

    private float GetBestSensitivity(
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware,
        SensorType type,
        Angle bearing,
        bool activeOnly,
        bool requireInactive = false)
    {
        var best = 0f;
        foreach (var (uid, sens, sensXform) in hardware)
        {
            if (sens.SensorType != type)
                continue;
            if (activeOnly && !sens.ActiveMode)   // EMCON/STBY → not used for ARS
                continue;
            if (requireInactive && sens.ActiveMode)
                continue;
            if (!InArc(sens, sensXform, bearing))
                continue;
            if (IsSensorJammedEntity(uid, sens, sensXform))
                continue;

            best = MathF.Max(best, sens.Sensitivity);
        }
        return best;
    }

    private bool IsBearingJammed(
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware,
        Angle bearing)
    {
        foreach (var (uid, sens, sensXform) in hardware)
        {
            if (sens.SensorType == SensorType.Jammer)
                continue;
            if (!InArc(sens, sensXform, bearing))
                continue;
            if (IsSensorJammedEntity(uid, sens, sensXform))
                return true;
        }

        return false;
    }

    private bool IsSensorJammedEntity(EntityUid sensorUid, ShipSensorComponent sens, TransformComponent sensXform)
    {
        var sensorPos = _xform.GetWorldPosition(sensXform);
        var mapId = sensXform.MapID;

        var jamEnum = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (jamEnum.MoveNext(out var jamUid, out var jam, out var jamXform))
        {
            if (jam.SensorType != SensorType.Jammer || !jam.Enabled || !jam.ActiveMode)
                continue;
            if (!IsSensorPowered(jamUid))
                continue;
            if (jamXform.MapID != mapId)
                continue;
            if (jamXform.GridUid != null && jamXform.GridUid == sensXform.GridUid)
                continue;
            if (jamXform.GridUid is { } jamGrid &&
                sensXform.GridUid is { } sensGrid &&
                SharesDatalink(jamGrid, sensGrid))
                continue;

            var jamPos = _xform.GetWorldPosition(jamXform);
            var toSensor = sensorPos - jamPos;
            if (toSensor.LengthSquared() < 0.0001f)
                continue;

            if (!InArc(jam, jamXform, toSensor.ToWorldAngle()))
                continue;

            var resistance = sens.JammingResistance * sens.Sensitivity;

            if (jam.JamStrength > resistance)
                return true;
        }

        return false;
    }

    private bool InArc(ShipSensorComponent sens, TransformComponent sensXform, Angle targetWorldBearing)
    {
        if (sens.ArcDegrees >= 360f)
            return true;

        // world rotation instead of grid relative for ease of use i'd rather friend myself than adjust it for every little turn man
        var center = Angle.FromDegrees(sens.ArcOffset);
        var half = Angle.FromDegrees(sens.ArcDegrees / 2f);
        var delta = Angle.ShortestDistance(center, targetWorldBearing);
        return Math.Abs(delta.Theta) <= half.Theta;
    }

    private static DetectionLevel BestLevel(DetectionLevel a, DetectionLevel b)
    {
        return (int)a <= (int)b ? a : b;
    }

    public void OnUIOpened(EntityUid uid, ShuttleConsoleComponent component, BoundUIOpenedEvent args)
    {
        BuildBuiState(uid);
    }

    private void PropagateDatalinks()
    {
        var byCode = new Dictionary<string, List<(EntityUid Uid, GridSensorsComponent Comp)>>();

        var q = EntityQueryEnumerator<GridSensorsComponent>();
        while (q.MoveNext(out var uid, out var comp))
        {
            foreach (var code in comp.DatalinkCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                var key = NormalizeCode(code);
                if (!byCode.TryGetValue(key, out var list))
                {
                    list = new List<(EntityUid, GridSensorsComponent)>();
                    byCode[key] = list;
                }

                list.Add((uid, comp));
            }
        }

        foreach (var (_, members) in byCode)
        {
            if (members.Count < 2)
                continue;

            var union = new Dictionary<NetEntity, SensorContact>();
            foreach (var (_, comp) in members)
            {
                foreach (var (net, contact) in comp.Contacts)
                {
                    union.TryAdd(net, contact);
                }
            }

            foreach (var (uid, comp) in members)
            {
                var changed = false;
                var selfNet = GetNetEntity(uid);

                foreach (var (net, contact) in union)
                {
                    if (net == selfNet || comp.Contacts.ContainsKey(net))
                        continue;

                    comp.Contacts[net] = contact with { Flags = contact.Flags | SensorContactFlags.Datalink };
                    changed = true;
                }

                if (changed)
                    Dirty(uid, comp);
            }
        }
    }

    private void OnSetDatalink(EntityUid uid, object component, SensorsSetDatalinkMessage args)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid == null || !TryComp<GridSensorsComponent>(gridUid, out var sensors))
            return;

        sensors.DatalinkCodes = args.Codes
            .Select(NormalizeCode)
            .Where(static c => c.Length > 0)
            .Distinct()
            .Take(MaxDatalinkCodes)
            .ToHashSet();

        Dirty(gridUid.Value, sensors);
        BuildBuiState(uid);
    }

    public bool SharesDatalink(EntityUid aGrid, EntityUid bGrid)
    {
        if (!TryComp<GridSensorsComponent>(aGrid, out var a) ||
            !TryComp<GridSensorsComponent>(bGrid, out var b))
            return false;

        if (a.DatalinkCodes.Count == 0 || b.DatalinkCodes.Count == 0)
            return false;

        foreach (var code in a.DatalinkCodes)
        {
            if (b.DatalinkCodes.Contains(code))
                return true;
        }
        return false;
    }

    private void OnToggleHardware(EntityUid uid, object component, SensorsToggleHardwareMessage args)
    {
        if (!TryGetEntity(args.Sensor, out var sensUid))
            return;

        if (!_sensorQuery.TryGetComponent(sensUid.Value, out var sens))
            return;

        var consoleGrid = Transform(uid).GridUid;
        if (consoleGrid == null || Transform(sensUid.Value).GridUid != consoleGrid)
            return;

        if (args.Enabled is { } enabled)
            sens.Enabled = enabled;

        if (args.ActiveMode is { } active)
            sens.ActiveMode = active;

        Dirty(sensUid.Value, sens);
        UpdateSensorPowerLoad((sensUid.Value, sens));
        BuildBuiState(uid);
    }

    public SensorsBuiState? BuildBuiState(EntityUid consoleUid)
    {
        var gridUid = Transform(consoleUid).GridUid;
        if (gridUid == null || !TryComp<GridSensorsComponent>(gridUid, out var sensors))
            return null;

        var state = new SensorsBuiState
        {
            DatalinkCodes = sensors.DatalinkCodes.ToList()
        };

        foreach (var (_, contact) in sensors.Contacts)
        {
            if (!TryGetEntity(contact.TargetGrid, out var targetUid) ||
                !TryComp<MapGridComponent>(targetUid, out var targetGrid))
            {
                state.Contacts.Add(new SensorContactDto
                {
                    Target = contact.TargetGrid,
                    Name = contact.Designation,
                    Range = contact.Range,
                    Level = contact.Level,
                    Sources = BuildSourceString(contact.Flags),
                    Rcs = 0f,
                    IrSignature = 0f,
                    ActiveRadarWatts = 0f,
                    JamStrength = 0f
                });
                continue;
            }

            state.Contacts.Add(ToDto(contact, targetUid.Value, targetGrid, consoleUid));
        }

        var query = EntityQueryEnumerator<ShipSensorComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var sensUid, out var sens, out var xform, out var meta))
        {
            if (xform.GridUid != gridUid)
                continue;

            state.Hardware.Add(new SensorHardwareDto
            {
                Entity = GetNetEntity(sensUid),
                Name = meta.EntityName,
                SensorType = sens.SensorType,
                Enabled = sens.Enabled,
                ActiveMode = sens.ActiveMode,
                Powered = IsSensorPowered(sensUid),
                Sensitivity = sens.Sensitivity,
                ArcDegrees = sens.ArcDegrees,
                ArcOffset = sens.ArcOffset,
                DisplayRange = DisplayRangeFor(sens),
                TransmitWatts = sens.SensorType switch
                {
                    SensorType.ActiveRadar => SensitivityToRadarWatts(sens.Sensitivity),
                    SensorType.Jammer => SensitivityToJamWatts(sens.JamStrength),
                    _ => 0f
                },
                JamStrength = sens.JamStrength
            });
        }

        return state;
    }

    private void UpdateSensorPowerLoad(Entity<ShipSensorComponent> ent)
    {
        if (!_powerQuery.TryGetComponent(ent, out var receiver))
            return;

        if (!ent.Comp.Enabled)
        {
            receiver.Load = 0f;
            return;
        }

        receiver.Load = ent.Comp.ActiveMode ? ent.Comp.PowerDrawActive : ent.Comp.PowerDraw;
    }

    private bool IsSensorPowered(EntityUid uid)
    {
        if (!_powerQuery.HasComponent(uid))
            return true;

        return _power.IsPowered(uid);
    }

    private static string BuildSourceString(SensorContactFlags flags)
    {
        var parts = new List<string>(4);
        if ((flags & SensorContactFlags.ActiveRadar) != 0)
            parts.Add("ARS");
        if ((flags & SensorContactFlags.PassiveRadar) != 0)
            parts.Add("E");
        if ((flags & SensorContactFlags.VisualIr) != 0)
            parts.Add("IR");
        return parts.Count == 0 ? "—" : string.Join('/', parts);
    }

    // me when i lie for larp
    public static float SensitivityToRadarWatts(float sensitivity)
        => MathF.Max(0f, sensitivity) * 25_000f;

    public static float SensitivityToJamWatts(float jamStrength)
        => MathF.Max(0f, jamStrength) * 10_000f;

    private SensorContactDto ToDto(SensorContact c, EntityUid targetUid, MapGridComponent targetGrid, EntityUid detectedBy)
    {
        var ir = 0f;
        if (TryComp<ThermalSignatureComponent>(targetUid, out var heat))
            ir = MathF.Max(heat.TotalHeat, 0f);

        var jam = 0f;
        var jamEnum = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (jamEnum.MoveNext(out _, out var sens, out var xform))
        {
            if (xform.GridUid != targetUid)
                continue;
            if (sens.SensorType != SensorType.Jammer || !sens.Enabled || !sens.ActiveMode)
                continue;
            jam = MathF.Max(jam, sens.JamStrength);
        }

        var flags = c.Flags;

        var arWatts = 0f;
        var sensEnum = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (sensEnum.MoveNext(out _, out var sens, out var xform))
        {
            if (xform.GridUid != targetUid)
                continue;
            if (sens.SensorType != SensorType.ActiveRadar || !sens.Enabled || !sens.ActiveMode)
                continue;
            arWatts = MathF.Max(arWatts, SensitivityToRadarWatts(sens.Sensitivity));
        }

        var designation = c.Designation;
        if (TryComp<IFFComponent>(targetUid, out var iff))
            designation = iff.Address;

        var bearing = (c.WorldPosition - _xform.GetWorldPosition(detectedBy)).ToWorldAngle();
        var bearingDegrees = (int)((bearing.Degrees % 360f + 360f) % 360f);

        return new SensorContactDto
        {
            Target = c.TargetGrid,
            Name = designation,
            Range = c.Range,
            BearingDegrees = bearingDegrees,
            Level = c.Level,
            Sources = BuildSourceString(flags),
            Rcs = targetGrid.LocalAABB.Width * targetGrid.LocalAABB.Height,
            IrSignature = ir,
            ActiveRadarWatts = arWatts,
            JamStrength = jam
        };
    }

    private static float DisplayRangeFor(ShipSensorComponent sens, MapGridComponent? grid = null)
    {
        float refDiagonal = 20f;
        if (grid != null)
        {
            var aabb = grid.LocalAABB;
            refDiagonal = MathF.Sqrt(aabb.Width * aabb.Width + aabb.Height * aabb.Height);
            refDiagonal = MathF.Max(refDiagonal, 1f);
        }

        // Reference emitter for ESM range readout (notional strong shipborne ARS).
        const float refEmitterRange = 80f;

        return sens.SensorType switch
        {
            // ESM: range vs a reference emitter, scaled by sensitivity — NOT hull diagonal search
            SensorType.PassiveRadar => refEmitterRange * sens.Sensitivity,

            // Active search: only meaningful while emitting; still show full capability in the list
            SensorType.ActiveRadar => refDiagonal * sens.Sensitivity,

            SensorType.Infrared => MathF.Sqrt(500f) * sens.Sensitivity,

            SensorType.Jammer => refDiagonal * sens.JamStrength,

            _ => 0f
        };
    }

    public bool TrySetEmcon(EntityUid consoleUid, SensorsSetEmconMessage msg)
    {
        if (!TryGetEntity(msg.Sensor, out var sensUid))
            return false;

        if (!_sensorQuery.TryGetComponent(sensUid.Value, out var sens))
            return false;

        var consoleGrid = Transform(consoleUid).GridUid;
        if (consoleGrid == null || Transform(sensUid.Value).GridUid != consoleGrid)
            return false;

        sens.Enabled = msg.Enabled;
        sens.ActiveMode = msg.ActiveMode;

        Dirty(sensUid.Value, sens);
        UpdateSensorPowerLoad((sensUid.Value, sens));
        return true;
    }

    public bool TrySetArc(EntityUid consoleUid, SensorsSetArcMessage msg)
    {
        if (!TryGetEntity(msg.Sensor, out var sensUid))
            return false;

        if (!_sensorQuery.TryGetComponent(sensUid.Value, out var sens))
            return false;

        var consoleGrid = Transform(consoleUid).GridUid;
        if (consoleGrid == null || Transform(sensUid.Value).GridUid != consoleGrid)
            return false;

        // Normalize to [-180, 180]
        var offset = msg.ArcOffsetDegrees % 360f;
        if (offset > 180f)
            offset -= 360f;
        if (offset < -180f)
            offset += 360f;

        sens.ArcOffset = offset;

        Dirty(sensUid.Value, sens);
        return true;
    }

    public bool TrySetDatalink(EntityUid consoleUid, SensorsSetDatalinkMessage msg)
    {
        var gridUid = Transform(consoleUid).GridUid;
        if (gridUid == null || !TryComp<GridSensorsComponent>(gridUid, out var sensors))
            return false;

        sensors.DatalinkCodes = msg.Codes
            .Select(NormalizeCode)
            .Where(static c => c.Length > 0)
            .Distinct()
            .Take(MaxDatalinkCodes)
            .ToHashSet();

        Dirty(gridUid.Value, sensors);
        return true;
    }

    private readonly record struct TargetEmissions(float ActiveRadarWatts, float JamStrength)
    {
        public bool IsEmitting => ActiveRadarWatts > 0f || JamStrength > 0f;
    }

    private TargetEmissions GetTargetEmissions(EntityUid targetGridUid)
    {
        var arWatts = 0f;
        var jam = 0f;

        var q = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var sens, out var xform))
        {
            if (xform.GridUid != targetGridUid)
                continue;
            if (!sens.Enabled || !sens.ActiveMode) // STBY = not radiating
                continue;
            if (!IsSensorPowered(uid))
                continue;

            if (sens.SensorType == SensorType.ActiveRadar)
                arWatts = MathF.Max(arWatts, SensitivityToRadarWatts(sens.Sensitivity));
            else if (sens.SensorType == SensorType.Jammer)
                jam = MathF.Max(jam, sens.JamStrength);
        }

        return new TargetEmissions(arWatts, jam);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
