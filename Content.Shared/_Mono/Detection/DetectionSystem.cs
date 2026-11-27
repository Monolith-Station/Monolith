using Content.Shared._Mono.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Content.Shared._Mono.Sensors;
using System;
using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;

namespace Content.Shared._Mono.Detection;

/// <summary>
///     Handles the logic for grid and entity detection.
/// </summary>
public sealed partial class DetectionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public EntityUid? GetRadarNetwork(EntityUid? map)
    {
        if (TryComp<CEZTransitMapComponent>(map, out var transit))
            map = transit.LowerMap ?? transit.UpperMap;

        return TryComp<CEZMapComponent>(map, out var layer) ? layer.NetworkUid : null;
    }

    public bool SharesRadarSpace(EntityUid? first, EntityUid? second)
    {
        return first != null && second != null &&
            (first == second || GetRadarNetwork(first) is { } network && network == GetRadarNetwork(second));
    }

    private float _thermalMul;
    private float _visualMul;
    private float _mediumMass = 300;
    private float _largeMass = 600;
    private float _hugeMass = 1000;
    private float _supermassiveMass = 2000;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MonoCVars.ThermalDetectionMultiplier, value => _thermalMul = value, true);
        Subs.CVar(_cfg, MonoCVars.VisualDetectionMultiplier, value => _visualMul = value, true);
    }

    public DetectionLevel IsGridDetected(Entity<MapGridComponent?> grid, EntityUid byUid)
    {
        if (!Resolve(grid, ref grid.Comp))
            return DetectionLevel.Undetected;

        var byXform = Transform(byUid);
        var observerGrid = byXform.GridUid ?? byUid;

        var comp = EnsureComp<DetectionRangeMultiplierComponent>(observerGrid);

        if (comp.AlwaysDetect)
            return DetectionLevel.Detected;

        var gridAABB = grid.Comp.LocalAABB;
        var gridDiagonal = MathF.Sqrt(gridAABB.Width * gridAABB.Width + gridAABB.Height * gridAABB.Height);

        const float visualRangeFloor = 500f;
        const float visualMaxRange = 3000f;
        var visualRadius = comp.VisualMultiplier > 0f
            ? MathF.Min(visualMaxRange, MathF.Max(visualRangeFloor, gridDiagonal * comp.VisualMultiplier * _visualMul * 12))
            : 0f;

        var thermalSig = TryComp<ThermalSignatureComponent>(grid, out var sigComp)
            ? MathF.Max(sigComp.TotalHeat, 0f)
            : 0f;
        var thermalRadius = MathF.Sqrt(thermalSig) * comp.InfraredMultiplier * _thermalMul;

        if (TryComp<DetectedAtRangeMultiplierComponent>(grid, out var compAt))
        {
            visualRadius *= compAt.VisualMultiplier;
            thermalRadius *= compAt.InfraredMultiplier;
            visualRadius += compAt.VisualBias;
            if (comp.VisualMultiplier > 0f)
                visualRadius = MathF.Min(visualMaxRange, MathF.Max(visualRangeFloor, visualRadius));
        }

        var level = DetectionLevel.Undetected;

        var xform = Transform(grid);
        if (!SharesRadarSpace(xform.MapUid, byXform.MapUid))
            return DetectionLevel.Undetected;

        var distance = Vector2.Distance(_xform.GetWorldPosition(xform), _xform.GetWorldPosition(byXform));
        if (IsActiveRadarDetected(observerGrid, grid, gridDiagonal, distance, byXform, xform)
            || (visualRadius > 0f && distance <= visualRadius))
            level = DetectionLevel.Detected;
        else if ((thermalRadius > 0f && distance < thermalRadius)
                 || IsEsmDetected(observerGrid, grid, distance, byXform, xform))
            level = DetectionLevel.PartialDetected;

        if (TryComp<GridSensorsComponent>(observerGrid, out var gs))
        {
            var netTarget = GetNetEntity(grid.Owner);
            if (gs.Contacts.TryGetValue(netTarget, out var contact)
                && contact.Level != DetectionLevel.Undetected)
            {
                level = BestLevel(level, contact.Level);
            }
        }

        return level;
    }

    private static DetectionLevel BestLevel(DetectionLevel a, DetectionLevel b)
    {
        return (int)a <= (int)b ? a : b;
    }

    public DetectionLevel IsGridDetected(Entity<MapGridComponent?> grid, IEnumerable<EntityUid> byUids)
    {
        var bestLevel = DetectionLevel.Undetected;
        foreach (var uid in byUids)
        {
            var level = IsGridDetected(grid, uid);
            if (level == DetectionLevel.Detected)
                return level;

            if ((int)level < (int)bestLevel)
                bestLevel = level;
        }
        return bestLevel;
    }

    public MassLevel CheckMass(Entity<MapGridComponent?> grid)
    {
        var physics = Comp<PhysicsComponent>(grid);

        if (physics.FixturesMass >= _supermassiveMass)
            return MassLevel.Supermassive;
        if (physics.FixturesMass >= _hugeMass)
            return MassLevel.Huge;
        if (physics.FixturesMass >= _largeMass)
            return MassLevel.Large;
        if (physics.FixturesMass >= _mediumMass)
            return MassLevel.Medium;
        if (physics.FixturesMass >= 0)
            return MassLevel.Small;
        return MassLevel.Unknown;
    }

    public string HandleUnknownMassLabel(Entity<MapGridComponent?> grid)
    {
        var massLevel = CheckMass(grid);
        var massLevelKey = massLevel.ToString().ToLowerInvariant();

        return Loc.GetString("shuttle-console-signature-unknown", ("mass", massLevelKey));
    }

    private bool IsActiveRadarDetected(
        EntityUid observerGrid,
        EntityUid targetGrid,
        float targetDiagonal,
        float distance,
        TransformComponent byXform,
        TransformComponent targetXform)
    {
        if (byXform.MapID != targetXform.MapID)
            return false;

        var delta = _xform.GetWorldPosition(targetXform) - _xform.GetWorldPosition(byXform);
        var bearing = delta.ToWorldAngle();

        var bestSens = 0f;
        var query = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var sens, out var sensXform))
        {
            if (sensXform.GridUid != observerGrid)
                continue;
            if (sens.SensorType != SensorType.ActiveRadar)
                continue;
            if (!sens.Enabled || !sens.ActiveMode)
                continue;

            if (sens.ArcDegrees < 360f)
            {
                Angle center;
                if (sens.GridRelativeArc)
                {
                    var gridRot = _xform.GetWorldRotation(observerGrid);
                    center = gridRot + Angle.FromDegrees(sens.ArcOffset + 90);
                }
                else
                {
                    center = Angle.FromDegrees(sens.ArcOffset + 90);
                }

                var half = Angle.FromDegrees(sens.ArcDegrees * 0.5f);
                var angularDelta = Angle.ShortestDistance(center, bearing);
                if (Math.Abs(angularDelta.Theta) > half.Theta)
                    continue;
            }

            bestSens = MathF.Max(bestSens, sens.Sensitivity);
        }

        if (bestSens <= 0f)
            return false;

        return distance <= targetDiagonal * bestSens;
    }

    private bool IsEsmDetected(
        EntityUid observerGrid,
        EntityUid targetGrid,
        float distance,
        TransformComponent byXform,
        TransformComponent targetXform)
    {
        if (byXform.MapID != targetXform.MapID)
            return false;

        var delta = _xform.GetWorldPosition(targetXform) - _xform.GetWorldPosition(byXform);
        var bearing = delta.ToWorldAngle();

        var passiveSens = 0f;
        var obsQuery = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (obsQuery.MoveNext(out _, out var sens, out var sx))
        {
            if (sx.GridUid != observerGrid)
                continue;
            if (sens.SensorType != SensorType.PassiveRadar)
                continue;
            if (!sens.Enabled)
                continue;

            if (sens.ArcDegrees < 360f)
            {
                Angle center;
                if (sens.GridRelativeArc)
                    center = _xform.GetWorldRotation(observerGrid) + Angle.FromDegrees(sens.ArcOffset + 90);
                else
                    center = Angle.FromDegrees(sens.ArcOffset + 90);

                var half = Angle.FromDegrees(sens.ArcDegrees * 0.5f);
                if (Math.Abs(Angle.ShortestDistance(center, bearing).Theta) > half.Theta)
                    continue;
            }

            passiveSens = MathF.Max(passiveSens, sens.Sensitivity);
        }

        if (passiveSens <= 0f)
            return false;

        var tgtQuery = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (tgtQuery.MoveNext(out _, out var esens, out var ex))
        {
            if (ex.GridUid != targetGrid)
                continue;
            if (!esens.Enabled || !esens.ActiveMode)
                continue;

            if (esens.SensorType != SensorType.ActiveRadar)
                continue;

            var watts = esens.Sensitivity * 25_000f;
            if (distance <= passiveSens * watts)
                return true;
        }

        return false;
    }
}

public enum DetectionLevel
{
    Detected = 0,
    PartialDetected = 1,
    Undetected = 2,
}

public enum MassLevel
{
    Unknown = 0,
    Small = 1,
    Medium = 2,
    Large = 3,
    Huge = 4,
    Supermassive = 5,
}
