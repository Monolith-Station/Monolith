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
using Content.Shared.Shuttles.BUIStates;
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

        var query = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (query.MoveNext(out var sensUid, out var sens, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            if (!sens.Enabled || !IsSensorPowered(sensUid))
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

        var grids = new List<EntityUid>();

        var gridQuery = EntityQueryEnumerator<GridSensorsComponent, MapGridComponent, TransformComponent, DetectionRangeMultiplierComponent>();
        while (gridQuery.MoveNext(out var gridUid, out var gridSensors, out _, out var xform, out var rangeMul))
        {
            UpdateGridSensors(gridUid, gridSensors, xform, rangeMul);
            grids.Add(gridUid);
        }

        PropagateDatalinks();

        foreach (var gridUid in grids)
        {
            RefreshOpenConsoles(gridUid);
        }
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

        ApplyOmniMultipliers(rangeMul, sensors, hardware);
        Dirty(gridUid, rangeMul);

        var newContacts = new Dictionary<NetEntity, SensorContact>();
        var locallySensed = new HashSet<NetEntity>();

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
            ApplyDirectionalMultipliers(rangeMul, sensors, hardware, bearing);

            var passiveSens = GetBestSensitivity(hardware,
                SensorType.PassiveRadar,
                bearing,
                activeOnly: false);
            var activeSens = GetBestSensitivity(hardware,
                SensorType.ActiveRadar,
                bearing,
                activeOnly: true);
            var standbyActive = GetBestSensitivity(hardware,
                SensorType.ActiveRadar,
                bearing,
                activeOnly: false,
                requireInactive: true);
            passiveSens = MathF.Max(passiveSens, standbyActive * 0.5f);

            if (sensors.GrantBasicSensors)
                passiveSens = MathF.Max(passiveSens, sensors.BasicPassiveRadarMultiplier);

            var irSens = GetBestSensitivity(hardware, SensorType.Infrared, bearing, activeOnly: false);

            if (sensors.GrantBasicSensors)
                irSens = MathF.Max(irSens, sensors.BasicInfraredMultiplier);

            var emissions = GetTargetEmissions(otherUid);
            var thermalSig = 0f;
            if (TryComp<ThermalSignatureComponent>(otherUid, out var heat))
                thermalSig = MathF.Max(heat.TotalHeat, 0f);

            var aabb = otherGrid.LocalAABB;
            var diagonal = MathF.Sqrt(aabb.Width * aabb.Width + aabb.Height * aabb.Height);

            // ARS
            var activeRange = activeSens > 0f ? diagonal * activeSens : 0f;

            // V
            const float visualRangeFloor = 500f;
            var visualMul = sensors.GrantBasicSensors ? sensors.BasicVisualMultiplier : 0f;
            var visualRange = visualMul > 0f
                ? MathF.Max(visualRangeFloor, diagonal * visualMul)
                : 0f;

            // IR
            var irRange = irSens > 0f && thermalSig > 0f
                ? MathF.Sqrt(thermalSig) * irSens
                : 0f;

            // EM
            var esmRange = passiveSens > 0f && emissions > 0f
                ? passiveSens * emissions
                : 0f;

            var seenByArs = activeRange > 0f && dist <= activeRange;
            var seenByEsm = esmRange > 0f && dist <= esmRange;
            var seenByIr = irRange > 0f && dist <= irRange;
            var seenByVisual = visualRange > 0f && dist <= visualRange;

            if (!seenByArs && !seenByEsm && !seenByIr && !seenByVisual)
                continue;

            var level = DetectionLevel.Undetected;
            if (seenByArs || seenByVisual)
                level = DetectionLevel.Detected;
            else if (seenByIr || seenByEsm)
                level = DetectionLevel.PartialDetected;

            if (seenByArs || seenByEsm || seenByIr || seenByVisual)
                level = BestLevel(level, DetectionLevel.Detected);

            var flags = SensorContactFlags.None;
            if (seenByArs)
                flags |= SensorContactFlags.ActiveRadar;
            if (seenByEsm)
                flags |= SensorContactFlags.PassiveRadar;
            if (seenByVisual)
                flags |= SensorContactFlags.Visual;
            if (seenByIr)
                flags |= SensorContactFlags.Infrared;

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
            locallySensed.Add(netOther);
        }

        var decoyQ = EntityQueryEnumerator<RadarCountermeasureComponent, TransformComponent>();
        while (decoyQ.MoveNext(out var decoyUid, out var cm, out var decoyXform))
        {
            if (!cm.Active || cm.DecoyContacts <= 0)
                continue;
            if (decoyXform.MapID != xform.MapID)
                continue;

            var decoyCenter = _xform.GetWorldPosition(decoyXform);
            var toCloud = decoyCenter - ourPos;
            var distCloud = toCloud.Length();
            if (distCloud < 0.01f || distCloud > cm.MaxEffectRange * 1.5f)
                continue;

            var bearingCloud = toCloud.ToWorldAngle();
            ApplyDirectionalMultipliers(rangeMul, sensors, hardware, bearingCloud);

            var activeSens = GetBestSensitivity(hardware, SensorType.ActiveRadar, bearingCloud, activeOnly: true);
            if (activeSens <= 0f)
                continue;

            var decoyRange = cm.DecoyRadarSize * activeSens;
            if (distCloud > decoyRange)
                continue;

            // Primary contact on the cloud entity itself.
            var netDecoy = GetNetEntity(decoyUid);
            newContacts[netDecoy] = new SensorContact
            {
                TargetGrid = netDecoy,
                WorldPosition = decoyCenter,
                Range = distCloud,
                Level = DetectionLevel.Detected,
                Flags = SensorContactFlags.ActiveRadar | SensorContactFlags.Decoy,
                LastSeen = now,
                Designation = "UNK",
            };
            locallySensed.Add(netDecoy);

            // Extra ghost pips around the cloud (share same entity key offset in designation only).
            // Additional independent lock targets would need separate entities; one lockable cloud is enough.
            _ = cm.DecoyContacts;
            _ = cm.DecoySpread;
        }

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
        sensors.LocallySensed = locallySensed;
        Dirty(gridUid, sensors);
    }

    private void RefreshOpenConsoles(EntityUid gridUid)
    {
        var fcQuery = EntityQueryEnumerator<FireControlConsoleComponent, TransformComponent>();
        while (fcQuery.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            if (!_ui.IsUiOpen(consoleUid, FireControlConsoleUiKey.Key))
                continue;

            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }

        var shuttleQuery = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            if (!_ui.IsUiOpen(consoleUid, ShuttleConsoleUiKey.Key))
                continue;

            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }

        var radarQuery = EntityQueryEnumerator<RadarConsoleComponent, TransformComponent>();
        while (radarQuery.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;
            if (!_ui.IsUiOpen(consoleUid, RadarConsoleUiKey.Key))
                continue;

            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }
    }

    private void ApplyOmniMultipliers(
        DetectionRangeMultiplierComponent rangeMul,
        GridSensorsComponent sensors,
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware)
    {
        var visual = sensors.GrantBasicSensors ? sensors.BasicVisualMultiplier : 0f;
        var infrared = sensors.GrantBasicSensors ? sensors.BasicInfraredMultiplier : 0f;

        foreach (var (_, sens, _) in hardware)
        {
            if (sens.SensorType is not (SensorType.Infrared))
                continue;
            if (sens.ArcDegrees < 360f)
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

        foreach (var (_, sens, sensXform) in hardware)
        {
            if (sens.SensorType is not SensorType.Infrared)
                continue;
            if (!InArc(sens, sensXform, bearing))
                continue;

            infrared = MathF.Max(infrared, sens.Sensitivity);
        }

        rangeMul.VisualMultiplier = visual;
        rangeMul.InfraredMultiplier = infrared;
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
            if (activeOnly && !sens.ActiveMode)
                continue;
            if (requireInactive && sens.ActiveMode)
                continue;
            if (!InArc(sens, sensXform, bearing))
                continue;

            var sensitivity = sens.Sensitivity;

            if (type == SensorType.ActiveRadar)
            {
                var mult = _chaff.GetActiveRadarMultiplier(uid, bearing, sens.ChaffResistance);
                sensitivity *= mult;
            }

            best = MathF.Max(best, sensitivity);
        }

        return best;
    }

    private bool InArc(ShipSensorComponent sens, TransformComponent sensXform, Angle targetWorldBearing)
    {
        if (sens.ArcDegrees >= 360f)
            return true;

        Angle center;
        if (sens.GridRelativeArc)
        {
            var gridUid = sensXform.GridUid;
            var gridRot = gridUid != null
                ? _xform.GetWorldRotation(gridUid.Value)
                : _xform.GetWorldRotation(sensXform);
            center = gridRot + Angle.FromDegrees(sens.ArcOffset+90);
        }
        else
        {
            center = Angle.FromDegrees(sens.ArcOffset+90);
        }

        var half = Angle.FromDegrees(sens.ArcDegrees * 0.5f);
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
        var now = _timing.CurTime;
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

        const SensorContactFlags localSensorMask =
            SensorContactFlags.ActiveRadar
            | SensorContactFlags.PassiveRadar
            | SensorContactFlags.Infrared
            | SensorContactFlags.Visual;

        foreach (var (_, members) in byCode)
        {
            if (members.Count < 2)
                continue;

            var union = new Dictionary<NetEntity, SensorContact>();
            foreach (var (_, comp) in members)
            {
                foreach (var net in comp.LocallySensed)
                {
                    if (!comp.Contacts.TryGetValue(net, out var contact))
                        continue;

                    if ((contact.Flags & localSensorMask) == 0)
                        continue;

                    var export = contact with
                    {
                        Flags = contact.Flags & localSensorMask,
                        LastSeen = now,
                    };

                    if (union.TryGetValue(net, out var existing))
                    {
                        union[net] = existing with
                        {
                            Level = BestLevel(existing.Level, export.Level),
                            Flags = existing.Flags | export.Flags,
                            LastSeen = now,
                            WorldPosition = export.WorldPosition,
                            Designation = export.Designation ?? existing.Designation,
                            Range = export.Range,
                        };
                    }
                    else
                    {
                        union[net] = export;
                    }
                }
            }

            foreach (var (uid, comp) in members)
            {
                var changed = false;
                var selfNet = GetNetEntity(uid);
                var ourPos = _xform.GetWorldPosition(uid);

                foreach (var (net, contact) in union)
                {
                    if (net == selfNet)
                        continue;

                    if (comp.LocallySensed.Contains(net))
                    {
                        if (!comp.Contacts.TryGetValue(net, out var own))
                            continue;

                        var tagged = own with
                        {
                            Flags = (own.Flags & localSensorMask) | SensorContactFlags.Datalink,
                        };
                        if (tagged != own)
                        {
                            comp.Contacts[net] = tagged;
                            changed = true;
                        }
                        continue;
                    }

                    var targetPos = contact.WorldPosition;
                    if (TryGetEntity(net, out var targetUid))
                        targetPos = _xform.GetWorldPosition(targetUid.Value);

                    var range = (targetPos - ourPos).Length();

                    if (comp.Contacts.TryGetValue(net, out var existing))
                    {
                        var localBits = existing.Flags & localSensorMask;
                        var merged = existing with
                        {
                            Level = BestLevel(existing.Level, contact.Level),
                            Flags = localBits | SensorContactFlags.Datalink,
                            LastSeen = now,
                            WorldPosition = targetPos,
                            Range = range,
                            Designation = contact.Designation ?? existing.Designation,
                        };

                        if (merged != existing)
                        {
                            comp.Contacts[net] = merged;
                            changed = true;
                        }
                    }
                    else
                    {
                        comp.Contacts[net] = contact with
                        {
                            Flags = SensorContactFlags.Datalink,
                            LastSeen = now,
                            WorldPosition = targetPos,
                            Range = range,
                        };
                        changed = true;
                    }
                }

                if (changed)
                    Dirty(uid, comp);
            }
        }
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
                    BearingDegrees = 0f,
                    Sources = BuildSourceString(contact.Flags),
                    Rcs = 0f,
                    IrSignature = 0f,
                    ActiveRadarWatts = 0f,
                    LastSeen = contact.LastSeen,
                    IsDatalink = IsDatalinkOnly(contact.Flags),
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
                ArcConfigurable = sens.ArcConfigurable,
                GridRelativeArc = sens.GridRelativeArc,
                TransmitWatts = sens.SensorType switch
                {
                    SensorType.ActiveRadar => SensitivityToRadarWatts(sens.Sensitivity),
                    _ => 0f,
                },
            });
        }

        var gridXform = Transform(gridUid.Value);
            state.GridWorldRotationDegrees = (float)_xform.GetWorldRotation(gridXform).Degrees;

        EntityUid? bestRadar = null;
        var bestResistance = float.MaxValue;
        var sensQ = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (sensQ.MoveNext(out var sensUid, out var sens, out var sx))
        {
            if (sx.GridUid != gridUid)
                continue;
            if (sens.SensorType != SensorType.ActiveRadar)
                continue;
            if (!sens.Enabled || !IsSensorPowered(sensUid))
                continue;

            // Prefer emitting ARS; still show interference if only standby exists
            if (sens.ActiveMode)
            {
                if (sens.ChaffResistance < bestResistance || bestRadar == null)
                {
                    bestResistance = sens.ChaffResistance;
                    bestRadar = sensUid;
                }
            }
            else if (bestRadar == null)
            {
                bestResistance = sens.ChaffResistance;
                bestRadar = sensUid;
            }
        }

        state.ChaffArcs = bestRadar != null ? _chaff.BuildInterferenceArcs(bestRadar.Value, bestResistance) : _chaff.BuildInterferenceArcs(gridUid.Value, 0.25f);

        return state;
    }

    private static bool IsDatalinkOnly(SensorContactFlags flags)
    {
        return (flags & SensorContactFlags.Datalink) != 0
               && (flags & ~SensorContactFlags.Datalink) == SensorContactFlags.None;
    }

    private void UpdateSensorPowerLoad(Entity<ShipSensorComponent> ent)
    {
        if (!_powerQuery.TryGetComponent(ent, out var reciever))
            return;

        if (!ent.Comp.Enabled)
            _power.SetLoad(reciever, 0);

        var newLoad = ent.Comp.ActiveMode ? ent.Comp.PowerDrawActive : ent.Comp.PowerDraw;
            _power.SetLoad(reciever, newLoad);
    }

    private bool IsSensorPowered(EntityUid uid)
    {
        return !_powerQuery.HasComponent(uid) || _power.IsPowered(uid);
    }

    private static string BuildSourceString(SensorContactFlags flags)
    {
        var parts = new List<string>(5);
        if ((flags & SensorContactFlags.ActiveRadar) != 0 || (flags & SensorContactFlags.Decoy) != 0)
            parts.Add("ARS");
        if ((flags & SensorContactFlags.PassiveRadar) != 0)
            parts.Add("E");
        if ((flags & SensorContactFlags.Infrared) != 0)
            parts.Add("IR");
        if ((flags & SensorContactFlags.Visual) != 0)
            parts.Add("V");
        if ((flags & SensorContactFlags.Datalink) != 0)
            parts.Add("DL");
        return parts.Count == 0 ? "—" : string.Join('/', parts);
    }

    // me when i lie for larp
    public static float SensitivityToRadarWatts(float sensitivity)
    {
        return MathF.Max(0f, sensitivity) * 25_000f;
    }

    private SensorContactDto ToDto(SensorContact c, EntityUid targetUid, MapGridComponent targetGrid, EntityUid detectedBy)
    {
        var ir = 0f;
        if (TryComp<ThermalSignatureComponent>(targetUid, out var heat))
            ir = MathF.Max(heat.TotalHeat, 0f);

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

        var fromUid = Transform(detectedBy).GridUid ?? detectedBy;
        var delta = _xform.GetWorldPosition(targetUid) - _xform.GetWorldPosition(fromUid);
        var range = delta.Length();

        var worldDeg = (float)delta.ToWorldAngle().Degrees;
        var bearingDegrees = worldDeg;
        bearingDegrees = -180f - bearingDegrees;
        bearingDegrees %= 360f;
        if (bearingDegrees < 0f)
            bearingDegrees += 360f;

        return new SensorContactDto
        {
            Target = c.TargetGrid,
            Name = designation,
            Range = range,
            BearingDegrees = bearingDegrees,
            Sources = BuildSourceString(flags),
            Rcs = targetGrid.LocalAABB.Width * targetGrid.LocalAABB.Height,
            IrSignature = ir,
            ActiveRadarWatts = arWatts,
            LastSeen = c.LastSeen,
            IsDatalink = IsDatalinkOnly(flags),
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

        if (!sens.ArcConfigurable)
            return false;

        var consoleGrid = Transform(consoleUid).GridUid;
        if (consoleGrid == null || Transform(sensUid.Value).GridUid != consoleGrid)
            return false;

        var offset = msg.ArcOffsetDegrees % 360f;
        if (offset < 0f)
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

    private float GetTargetEmissions(EntityUid targetGridUid)
    {
        var arWatts = 0f;

        var q = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var sens, out var xform))
        {
            if (xform.GridUid != targetGridUid)
                continue;
            if (!sens.Enabled || !sens.ActiveMode)
                continue;
            if (!IsSensorPowered(uid))
                continue;

            if (sens.SensorType == SensorType.ActiveRadar)
                arWatts = MathF.Max(arWatts, SensitivityToRadarWatts(sens.Sensitivity));
        }

        return arWatts;
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
