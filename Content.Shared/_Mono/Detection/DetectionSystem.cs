using Content.Shared._Mono.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using System;

namespace Content.Shared._Mono.Detection;

/// <summary>
///     Handles the logic for grid and entity detection.
/// </summary>
public sealed class DetectionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

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

        var comp = EnsureComp<DetectionRangeMultiplierComponent>(byUid);

        if (comp.AlwaysDetect)
            return DetectionLevel.Detected;

        var gridAABB = grid.Comp.LocalAABB;
        var gridDiagonal = MathF.Sqrt(gridAABB.Width * gridAABB.Width + gridAABB.Height * gridAABB.Height);
        var visualSig = gridDiagonal;
        var visualRadius = visualSig * comp.VisualMultiplier * _visualMul;

        var thermalSig = TryComp<ThermalSignatureComponent>(grid, out var sigComp) ? MathF.Max(sigComp.TotalHeat, 0f) : 0f;
        var thermalRadius = MathF.Sqrt(thermalSig) * comp.InfraredMultiplier * _thermalMul;

        if (TryComp<DetectedAtRangeMultiplierComponent>(grid, out var compAt))
        {
            visualRadius *= compAt.VisualMultiplier;
            thermalRadius *= compAt.InfraredMultiplier;
            visualRadius += compAt.VisualBias;
        }

        var outlineRadius = thermalRadius * comp.InfraredOutlinePortion;
        outlineRadius = MathF.Max(outlineRadius, visualRadius);

        var level = DetectionLevel.Undetected;

        var xform = Transform(grid);
        var byXform = Transform(byUid);
        if (xform.Coordinates.TryDistance(EntityManager, byXform.Coordinates, out var distance))
        {
            if (distance <= outlineRadius) // accounts for visual radius
                level = DetectionLevel.Detected;
            else if (distance < thermalRadius)
                level = DetectionLevel.PartialDetected;
        }

        // maybe make this also take IFF being on into account?
        return level;
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
}

public enum DetectionLevel : int
{
    Detected = 0,
    PartialDetected = 1,
    Undetected = 2
}

public enum MassLevel : int
{
    Unknown = 0,
    Small = 1,
    Medium = 2,
    Large = 3,
    Huge = 4,
    Supermassive = 5
}
