using Content.Server._Mono.Radar;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Sensors;

public sealed partial class ShipSensorsSystem
{
    private void BuildSignatureCaches()
    {
        _emissionsCache.Clear();
        _thermalCache.Clear();
        _diagonalCache.Clear();
        _gridWorldPos.Clear();
        _gridMapId.Clear();
        _gridWorldRot.Clear();
        _gridsByMap.Clear();

        foreach (var list in _hardwareByGrid.Values)
            list.Clear();

        var sensQ = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (sensQ.MoveNext(out var uid, out var sens, out var xform))
        {
            if (xform.GridUid is not { } grid)
                continue;

            if (!_hardwareByGrid.TryGetValue(grid, out var list))
            {
                list = new List<(EntityUid, ShipSensorComponent, TransformComponent)>(8);
                _hardwareByGrid[grid] = list;
            }

            if (!sens.Enabled || !IsSensorPowered(uid))
                continue;

            list.Add((uid, sens, xform));

            if (sens.ActiveMode && sens.SensorType == SensorType.ActiveRadar)
            {
                var watts = SensitivityToRadarWatts(sens.Sensitivity);
                if (!_emissionsCache.TryGetValue(grid, out var cur) || watts > cur)
                    _emissionsCache[grid] = watts;
            }
        }

        var globalMaxDiagonal = 0f;
        var globalMaxEmissions = 0f;
        var globalMaxThermal = 0f;

        var gridQ = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (gridQ.MoveNext(out var gridUid, out var grid, out var xform))
        {
            var aabb = grid.LocalAABB;
            var diagonal = MathF.Sqrt(aabb.Width * aabb.Width + aabb.Height * aabb.Height);
            _diagonalCache[gridUid] = diagonal;
            if (diagonal > globalMaxDiagonal)
                globalMaxDiagonal = diagonal;

            _gridWorldPos[gridUid] = _xform.GetWorldPosition(xform);
            _gridMapId[gridUid] = xform.MapID;
            _gridWorldRot[gridUid] = _xform.GetWorldRotation(xform);

            if (!_gridsByMap.TryGetValue(xform.MapID, out var mapList))
            {
                mapList = new List<EntityUid>(32);
                _gridsByMap[xform.MapID] = mapList;
            }
            mapList.Add(gridUid);

            var thermal = 0f;
            if (TryComp<ThermalSignatureComponent>(gridUid, out var heat))
                thermal = MathF.Max(heat.TotalHeat, 0f);
            _thermalCache[gridUid] = thermal;
            if (thermal > globalMaxThermal)
                globalMaxThermal = thermal;

            if (!_emissionsCache.ContainsKey(gridUid))
                _emissionsCache[gridUid] = 0f;
            else if (_emissionsCache[gridUid] > globalMaxEmissions)
                globalMaxEmissions = _emissionsCache[gridUid];

            if (!_hardwareByGrid.ContainsKey(gridUid))
                _hardwareByGrid[gridUid] = new List<(EntityUid, ShipSensorComponent, TransformComponent)>();
        }

        _globalMaxDiagonal = globalMaxDiagonal;
        _globalMaxEmissions = globalMaxEmissions;
        _globalMaxThermal = globalMaxThermal;
    }

    private void BuildIlluminationCache()
    {
        _illuminationPairs.Clear();

        var q = EntityQueryEnumerator<FireControlConsoleComponent, TransformComponent>();
        while (q.MoveNext(out _, out var console, out var xform))
        {
            if (console.LockedTarget is not { } locked)
                continue;

            if (xform.GridUid is not { } illuminatorGrid)
                continue;

            var lockedGrid = Transform(locked).GridUid ?? locked;
            _illuminationPairs.Add((illuminatorGrid, lockedGrid));
        }
    }

    private void BuildDecoyCache()
    {
        _activeDecoys.Clear();

        var q = EntityQueryEnumerator<RadarCountermeasureComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var cm, out var xform))
        {
            if (!cm.Active || cm.DecoyContacts <= 0)
                continue;

            _activeDecoys.Add((uid, cm, _xform.GetWorldPosition(xform), xform.MapID));
        }
    }
}
