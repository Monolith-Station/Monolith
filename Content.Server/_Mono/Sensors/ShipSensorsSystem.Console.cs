using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Content.Shared._Mono.Sensors.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Sensors;

public sealed partial class ShipSensorsSystem
{
    private void RefreshOpenConsoles()
    {
        if (_observerGrids.Count == 0)
            return;

        var observerSet = new HashSet<EntityUid>(_observerGrids);

        var fcQuery = EntityQueryEnumerator<FireControlConsoleComponent, TransformComponent>();
        while (fcQuery.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid is not { } g || !observerSet.Contains(g))
                continue;
            if (!_ui.IsUiOpen(consoleUid, FireControlConsoleUiKey.Key))
                continue;
            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }

        var shuttleQuery = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid is not { } g || !observerSet.Contains(g))
                continue;
            if (!_ui.IsUiOpen(consoleUid, ShuttleConsoleUiKey.Key))
                continue;
            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }

        var radarQuery = EntityQueryEnumerator<RadarConsoleComponent, TransformComponent>();
        while (radarQuery.MoveNext(out var consoleUid, out _, out var xform))
        {
            if (xform.GridUid is not { } g || !observerSet.Contains(g))
                continue;
            if (!_ui.IsUiOpen(consoleUid, RadarConsoleUiKey.Key))
                continue;
            RaiseLocalEvent(consoleUid, new SensorsConsoleRefreshEvent());
        }
    }

    public void OnUIOpened(EntityUid uid, ShuttleConsoleComponent component, BoundUIOpenedEvent args)
    {
        BuildBuiState(uid);
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
                    IsIlluminator = IsIlluminatingUs(gridUid.Value, contact.TargetGrid),
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
        if (_hardwareByGrid.TryGetValue(gridUid.Value, out var hardware))
        {
            foreach (var (sensUid, sens, _) in hardware)
            {
                if (sens.SensorType != SensorType.ActiveRadar)
                    continue;

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
        }

        state.ChaffArcs = bestRadar != null
            ? _chaff.BuildInterferenceArcs(bestRadar.Value, bestResistance)
            : _chaff.BuildInterferenceArcs(gridUid.Value, 0.25f);

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
        {
            _power.SetLoad(reciever, 0);
            return;
        }

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

    public static float SensitivityToRadarWatts(float sensitivity)
    {
        return MathF.Max(0f, sensitivity) * 25_000f;
    }

    private SensorContactDto ToDto(SensorContact c, EntityUid targetUid, MapGridComponent targetGrid, EntityUid detectedBy)
    {
        _thermalCache.TryGetValue(targetUid, out var ir);
        if (ir <= 0f && TryComp<ThermalSignatureComponent>(targetUid, out var heat))
            ir = MathF.Max(heat.TotalHeat, 0f);

        var flags = c.Flags;

        _emissionsCache.TryGetValue(targetUid, out var arWatts);

        var designation = c.Designation;
        if (TryComp<IFFComponent>(targetUid, out var iff))
            designation = iff.Address;

        var fromUid = Transform(detectedBy).GridUid ?? detectedBy;

        if (!_gridWorldPos.TryGetValue(fromUid, out var fromPos))
            fromPos = _xform.GetWorldPosition(fromUid);
        if (!_gridWorldPos.TryGetValue(targetUid, out var toPos))
            toPos = _xform.GetWorldPosition(targetUid);

        var delta = toPos - fromPos;
        var range = delta.Length();

        var worldDeg = (float)delta.ToWorldAngle().Degrees;
        var bearingDegrees = worldDeg;
        bearingDegrees = -180f - bearingDegrees;
        bearingDegrees %= 360f;
        if (bearingDegrees < 0f)
            bearingDegrees += 360f;

        var sentRcs = 0f;
        if ((c.Flags & SensorContactFlags.ActiveRadar) != 0)
            sentRcs = targetGrid.LocalAABB.Width * targetGrid.LocalAABB.Height;
        var sentIr = 0f;
        if ((c.Flags & SensorContactFlags.Infrared) != 0)
            sentIr = ir;
        var sentEm = 0f;
        if ((c.Flags & SensorContactFlags.PassiveRadar) != 0)
            sentEm = arWatts;

        return new SensorContactDto
        {
            Target = c.TargetGrid,
            Name = designation,
            Range = range,
            BearingDegrees = bearingDegrees,
            Sources = BuildSourceString(flags),
            Rcs = sentRcs,
            IrSignature = sentIr,
            ActiveRadarWatts = sentEm,
            LastSeen = c.LastSeen,
            IsDatalink = IsDatalinkOnly(flags),
            IsIlluminator = IsIlluminatingUs(fromUid, c.TargetGrid)
        };
    }

    private bool IsIlluminatingUs(EntityUid ourGrid, NetEntity contactNet)
    {
        if (!TryGetEntity(contactNet, out var other))
            return false;

        var otherGrid = Transform(other.Value).GridUid ?? other.Value;
        return _illuminationPairs.Contains((otherGrid, ourGrid));
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
        return _emissionsCache.GetValueOrDefault(targetGridUid);
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
