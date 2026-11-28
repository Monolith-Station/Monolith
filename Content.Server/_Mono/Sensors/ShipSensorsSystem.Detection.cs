using Content.Server._Mono.Radar;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.Sensors;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Sensors;

public sealed partial class ShipSensorsSystem
{
    private void UpdateGridSensors(
        EntityUid gridUid,
        GridSensorsComponent sensors,
        TransformComponent xform,
        DetectionRangeMultiplierComponent rangeMul)
    {
        if (!_gridWorldPos.TryGetValue(gridUid, out var ourPos))
            ourPos = _xform.GetWorldPosition(xform);

        var now = _timing.CurTime;

        if (!_hardwareByGrid.TryGetValue(gridUid, out var hardware))
            hardware = _emptyHardware;

        ApplyOmniMultipliers(gridUid, rangeMul, sensors, hardware);

        var maxActiveSens = 0f;
        var maxPassiveSens = 0f;
        var maxStandby = 0f;
        var maxIrSens = 0f;
        foreach (var (_, sens, _) in hardware)
        {
            switch (sens.SensorType)
            {
                case SensorType.ActiveRadar:
                    if (sens.ActiveMode)
                        maxActiveSens = MathF.Max(maxActiveSens, sens.Sensitivity);
                    else
                        maxStandby = MathF.Max(maxStandby, sens.Sensitivity);
                    break;
                case SensorType.PassiveRadar:
                    maxPassiveSens = MathF.Max(maxPassiveSens, sens.Sensitivity);
                    break;
                case SensorType.Infrared:
                    maxIrSens = MathF.Max(maxIrSens, sens.Sensitivity);
                    break;
            }
        }

        maxPassiveSens = MathF.Max(maxPassiveSens, maxStandby * 0.5f);
        if (sensors.GrantBasicSensors)
        {
            maxPassiveSens = MathF.Max(maxPassiveSens, sensors.BasicPassiveRadarMultiplier);
            maxIrSens = MathF.Max(maxIrSens, sensors.BasicInfraredMultiplier);
        }

        var visualMul = sensors.GrantBasicSensors ? sensors.BasicVisualMultiplier : 0f;

        var maxRange = 0f;
        maxRange = MathF.Max(maxRange, _globalMaxDiagonal * maxActiveSens);
        maxRange = MathF.Max(maxRange, maxPassiveSens * _globalMaxEmissions);
        maxRange = MathF.Max(maxRange, MathF.Sqrt(MathF.Max(0f, _globalMaxThermal)) * maxIrSens);
        if (visualMul > 0f)
            maxRange = MathF.Max(maxRange, MathF.Min(3000f, MathF.Max(500f, _globalMaxDiagonal * visualMul * 12f)));

        var newContacts = new Dictionary<NetEntity, SensorContact>();
        var locallySensed = new HashSet<NetEntity>();

        if (!_gridsByMap.TryGetValue(xform.MapID, out var mapGrids))
        {
            sensors.Contacts = newContacts;
            sensors.LocallySensed = locallySensed;
            Dirty(gridUid, sensors);
            return;
        }

        foreach (var otherUid in mapGrids)
        {
            if (otherUid == gridUid)
                continue;

            if (!_gridWorldPos.TryGetValue(otherUid, out var otherPos))
                continue;

            var delta = otherPos - ourPos;
            var dist = delta.Length();
            if (dist <= 0.001f || dist > maxRange)
                continue;

            var bearing = delta.ToWorldAngle();
            GetSensitivities(hardware, bearing, out var passiveSens, out var activeSens, out var standbyActive, out var irSens);

            passiveSens = MathF.Max(passiveSens, standbyActive * 0.5f);
            if (sensors.GrantBasicSensors)
            {
                passiveSens = MathF.Max(passiveSens, sensors.BasicPassiveRadarMultiplier);
                irSens = MathF.Max(irSens, sensors.BasicInfraredMultiplier);
            }

            _emissionsCache.TryGetValue(otherUid, out var emissions);
            _thermalCache.TryGetValue(otherUid, out var thermalSig);
            if (!_diagonalCache.TryGetValue(otherUid, out var diagonal))
                diagonal = 0f;

            var activeRange = activeSens > 0f ? diagonal * activeSens : 0f;

            const float visualRangeFloor = 500f;
            const float visualMaxRange = 3000f;
            var visualRange = visualMul > 0f
                ? MathF.Min(visualMaxRange, MathF.Max(visualRangeFloor, diagonal * visualMul * 12f))
                : 0f;

            var irRange = irSens > 0f && thermalSig > 0f
                ? MathF.Sqrt(thermalSig) * irSens
                : 0f;

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

        foreach (var (decoyUid, cm, decoyCenter, decoyMap) in _activeDecoys)
        {
            if (decoyMap != xform.MapID)
                continue;

            var toCloud = decoyCenter - ourPos;
            var distCloud = toCloud.Length();
            if (distCloud < 0.01f || distCloud > cm.MaxEffectRange * 1.5f)
                continue;

            var bearingCloud = toCloud.ToWorldAngle();
            GetSensitivities(hardware, bearingCloud, out _, out var activeSens, out _, out _);
            if (activeSens <= 0f)
                continue;

            var decoyRange = cm.DecoyRadarSize * activeSens;
            if (distCloud > decoyRange)
                continue;

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
        }

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

    private void ApplyOmniMultipliers(
        EntityUid gridUid,
        DetectionRangeMultiplierComponent rangeMul,
        GridSensorsComponent sensors,
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware)
    {
        var visual = sensors.GrantBasicSensors ? sensors.BasicVisualMultiplier : 0f;
        var infrared = sensors.GrantBasicSensors ? sensors.BasicInfraredMultiplier : 0f;

        foreach (var (_, sens, _) in hardware)
        {
            if (sens.SensorType is not SensorType.Infrared)
                continue;
            if (sens.ArcDegrees < 360f)
                continue;
            infrared = MathF.Max(infrared, sens.Sensitivity);
        }

        if (Math.Abs(rangeMul.VisualMultiplier - visual) < 1 && Math.Abs(rangeMul.InfraredMultiplier - infrared) < 1)
            return;

        rangeMul.VisualMultiplier = visual;
        rangeMul.InfraredMultiplier = infrared;
        Dirty(gridUid, rangeMul);
    }

    private void GetSensitivities(
        List<(EntityUid Uid, ShipSensorComponent Sens, TransformComponent Xform)> hardware,
        Angle bearing,
        out float passive,
        out float active,
        out float standby,
        out float ir)
    {
        passive = 0f;
        active = 0f;
        standby = 0f;
        ir = 0f;

        foreach (var (uid, sens, sensXform) in hardware)
        {
            if (!InArc(sens, sensXform, bearing))
                continue;

            switch (sens.SensorType)
            {
                case SensorType.PassiveRadar:
                    passive = MathF.Max(passive, sens.Sensitivity);
                    break;
                case SensorType.ActiveRadar:
                {
                    var s = sens.Sensitivity * _chaff.GetActiveRadarMultiplier(uid, bearing, sens.ChaffResistance);
                    if (sens.ActiveMode)
                        active = MathF.Max(active, s);
                    else
                        standby = MathF.Max(standby, s);
                    break;
                }
                case SensorType.Infrared:
                    ir = MathF.Max(ir, sens.Sensitivity);
                    break;
            }
        }
    }

    private bool InArc(ShipSensorComponent sens, TransformComponent sensXform, Angle targetWorldBearing)
    {
        if (sens.ArcDegrees >= 360f)
            return true;

        Angle center;
        if (sens.GridRelativeArc)
        {
            var gridUid = sensXform.GridUid;
            Angle gridRot;
            if (gridUid != null && _gridWorldRot.TryGetValue(gridUid.Value, out var cached))
                gridRot = cached;
            else if (gridUid != null)
                gridRot = _xform.GetWorldRotation(gridUid.Value);
            else
                gridRot = _xform.GetWorldRotation(sensXform);

            center = gridRot + Angle.FromDegrees(sens.ArcOffset + 90);
        }
        else
        {
            center = Angle.FromDegrees(sens.ArcOffset + 90);
        }

        var half = Angle.FromDegrees(sens.ArcDegrees * 0.5f);
        var delta = Angle.ShortestDistance(center, targetWorldBearing);
        return Math.Abs(delta.Theta) <= half.Theta;
    }

    private static DetectionLevel BestLevel(DetectionLevel a, DetectionLevel b)
    {
        return (int)a <= (int)b ? a : b;
    }
}
