using System.Numerics;
using Content.Server._Mono.FireControl;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Content.Shared.Coordinates;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sensors;

public sealed partial class RadarLockSystem : EntitySystem
{
    private const float LeadMinQuality = 0.25f;
    private const float CoastSeconds = 2.5f;
    private const float QualityRecoverPerSec = 0.55f;
    private const float QualityDropPerSec = 1.1f;
    private const float ChaffHardBreak = 0.55f;

    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private RadarCountermeasureSystem _chaff = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;

    public bool HasLockCapableRadar(EntityUid grid)
    {
        return HasCapableRadar(grid, requireLock: true, requireGuidance: false);
    }

    public bool HasGuidanceCapableRadar(EntityUid grid)
    {
        return HasCapableRadar(grid, requireLock: false, requireGuidance: true);
    }

    public bool HasIllumination(EntityUid ourGrid, EntityUid target)
    {
        if (HasGuidanceCapableRadar(ourGrid) && CanLockFromGrid(ourGrid, target))
            return true;

        if (!TryComp<GridSensorsComponent>(ourGrid, out var us))
            return false;

        foreach (var code in us.DatalinkCodes)
        {
            var q = EntityQueryEnumerator<GridSensorsComponent, TransformComponent>();
            while (q.MoveNext(out var otherGrid, out var otherSens, out _))
            {
                if (otherGrid == ourGrid)
                    continue;
                if (!otherSens.DatalinkCodes.Contains(code))
                    continue;
                if (!HasGuidanceCapableRadar(otherGrid))
                    continue;
                if (CanLockFromGrid(otherGrid, target))
                    return true;
            }
        }

        return false;
    }

    public bool CanLockFromGrid(EntityUid ourGrid, EntityUid targetEntity)
    {
        if (!HasLockCapableRadar(ourGrid))
            return false;
        if (!TryComp<GridSensorsComponent>(ourGrid, out var sensors))
            return false;

        var targetGrid = Transform(targetEntity).GridUid ?? targetEntity;
        var net = GetNetEntity(targetGrid);
        if (!sensors.Contacts.TryGetValue(net, out var contact))
            return false;

        if ((contact.Flags & (SensorContactFlags.ActiveRadar | SensorContactFlags.Datalink)) == 0)
            return false;

        if (!IsBearingChaffed(ourGrid, targetGrid))
            return false;

        return true;
    }

    private bool HasCapableRadar(EntityUid grid, bool requireLock, bool requireGuidance)
    {
        var q = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var sens, out var xform))
        {
            if (xform.GridUid != grid)
                continue;
            if (sens.SensorType != SensorType.ActiveRadar)
                continue;
            if (!sens.Enabled || !sens.ActiveMode)
                continue;
            if (!_power.IsPowered(uid))
                continue;
            if (requireLock && !sens.CanProvideLock)
                continue;
            if (requireGuidance && !sens.CanProvideGuidance)
                continue;
            return true;
        }

        return false;
    }

    public bool CanLock(EntityUid console, EntityUid targetEntity)
    {
        return TryGetContact(console, targetEntity, out _, out var contact)
               && HasLocalFcr(Transform(console).GridUid)
               && ((contact.Flags & (SensorContactFlags.ActiveRadar | SensorContactFlags.Datalink)) != 0)
               && !IsHardChaffed(console, targetEntity, contact);
    }

    public bool CanProvideLead(EntityUid console, EntityUid targetEntity)
    {
        return TryGetContact(console, targetEntity, out _, out var contact)
               && HasLocalFcr(Transform(console).GridUid)
               && ((contact.Flags & SensorContactFlags.ActiveRadar) != 0 || (contact.Flags & SensorContactFlags.Decoy) != 0)
               && !IsHardChaffed(console, targetEntity, contact);
    }

    private bool HasLocalFcr(EntityUid? grid)
    {
        return grid is { } g && HasLockCapableRadar(g);
    }

    private bool TryGetContact(
        EntityUid console,
        EntityUid targetEntity,
        out EntityUid ourGrid,
        out SensorContact contact)
    {
        contact = default;
        ourGrid = default;

        var grid = Transform(console).GridUid;
        if (grid == null)
            return false;

        if (!TryComp<GridSensorsComponent>(grid, out var sensors))
            return false;

        var targetGrid = Transform(targetEntity).GridUid ?? targetEntity;
        var net = GetNetEntity(targetGrid);
        if (!sensors.Contacts.TryGetValue(net, out contact))
            return false;

        ourGrid = grid.Value;
        return true;
    }

    private bool IsHardChaffed(EntityUid console, EntityUid targetEntity, SensorContact contact)
    {
        var ourGrid = Transform(console).GridUid;
        if (ourGrid == null)
            return false;

        var targetGrid = Transform(targetEntity).GridUid ?? targetEntity;
        if (!IsBearingChaffed(ourGrid.Value, targetGrid))
            return false;

        var support =
            (contact.Flags & SensorContactFlags.PassiveRadar) != 0
            || (contact.Flags & SensorContactFlags.Infrared) != 0
            || (contact.Flags & SensorContactFlags.Visual) != 0;
        return !support;
    }

    public bool IsLockValid(EntityUid console, FireControlConsoleComponent consoleComp)
    {
        if (consoleComp.LockedTarget is not { } target)
            return false;

        if (!Exists(target))
        {
            ClearLock(consoleComp);
            return false;
        }

        if (CanLock(console, target))
            return true;

        if (consoleComp.CoastUntil is { } until && _timing.CurTime < until)
            return true;

        ClearLock(consoleComp);
        return false;
    }

    private static void ClearLock(FireControlConsoleComponent consoleComp)
    {
        consoleComp.LockedTarget = null;
        consoleComp.LockQuality = 0f;
        consoleComp.CoastUntil = null;
    }

    public bool IsBearingChaffed(EntityUid ourGrid, EntityUid targetGrid)
    {
        if (!Exists(ourGrid) || !Exists(targetGrid))
            return false;

        var ourPos = _xform.GetWorldPosition(ourGrid);
        var tgtPos = _xform.GetWorldPosition(targetGrid);
        var delta = tgtPos - ourPos;
        if (delta.LengthSquared() < 0.01f)
            return false;

        var bearing = delta.ToWorldAngle();
        var worst = 1f;

        var q = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var sens, out var xform))
        {
            if (xform.GridUid != ourGrid)
                continue;
            if (sens.SensorType != SensorType.ActiveRadar)
                continue;
            if (!sens.Enabled || !sens.ActiveMode || !sens.CanProvideLock)
                continue;
            if (!_power.IsPowered(uid))
                continue;

            var m = _chaff.GetActiveRadarMultiplier(uid, bearing, sens.ChaffResistance);
            if (m < worst)
                worst = m;
        }

        return worst < ChaffHardBreak;
    }

    public float GetBearingAttenuation(EntityUid ourGrid, EntityUid targetGrid)
    {
        if (!Exists(ourGrid) || !Exists(targetGrid))
            return 1f;

        var ourPos = _xform.GetWorldPosition(ourGrid);
        var tgtPos = _xform.GetWorldPosition(targetGrid);
        var delta = tgtPos - ourPos;
        if (delta.LengthSquared() < 0.01f)
            return 1f;

        var bearing = delta.ToWorldAngle();
        var worst = 1f;

        var q = EntityQueryEnumerator<ShipSensorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var sens, out var xform))
        {
            if (xform.GridUid != ourGrid)
                continue;
            if (sens.SensorType != SensorType.ActiveRadar)
                continue;
            if (!sens.Enabled || !sens.ActiveMode || !sens.CanProvideLock)
                continue;
            if (!_power.IsPowered(uid))
                continue;

            var m = _chaff.GetActiveRadarMultiplier(uid, bearing, sens.ChaffResistance);
            if (m < worst)
                worst = m;
        }

        return worst;
    }

    public bool IsBearingChaffedForConsole(EntityUid console, EntityUid targetEntity)
    {
        var ourGrid = Transform(console).GridUid;
        var tgtGrid = Transform(targetEntity).GridUid ?? targetEntity;
        if (ourGrid == null)
            return false;
        return IsBearingChaffed(ourGrid.Value, tgtGrid);
    }

    public override void Update(float frameTime)
    {
        var q = EntityQueryEnumerator<FireControlConsoleComponent>();
        while (q.MoveNext(out var uid, out var console))
        {
            if (console.LockedTarget is not { } target)
            {
                console.LockQuality = 0f;
                console.CoastUntil = null;
                continue;
            }

            if (!Exists(target))
            {
                ClearLock(console);
                continue;
            }

            var live = CanLock(uid, target);
            if (live)
            {
                var ourGrid = Transform(uid).GridUid;
                var tgtGrid = Transform(target).GridUid ?? target;
                var atten = ourGrid is { } og ? GetBearingAttenuation(og, tgtGrid) : 1f;
                var leadOk = CanProvideLead(uid, target);

                var targetQ = leadOk ? Math.Clamp(atten, 0f, 1f) : Math.Clamp(atten * 0.35f, 0f, 0.35f);
                if (TryGetContact(uid, target, out _, out var contact)
                    && (contact.Flags & SensorContactFlags.Decoy) != 0)
                {
                    targetQ = MathF.Min(targetQ, 0.4f);
                }
                console.LockQuality = console.LockQuality < targetQ ? Math.Min(targetQ, console.LockQuality + QualityRecoverPerSec * frameTime) : Math.Max(targetQ, console.LockQuality - QualityDropPerSec * frameTime);

                console.CoastPos = _xform.GetWorldPosition(target);
                console.CoastVel = TryComp<PhysicsComponent>(target, out var tp) ? tp.LinearVelocity : Vector2.Zero;
                console.CoastUntil = _timing.CurTime + TimeSpan.FromSeconds(CoastSeconds);
            }
            else if (console.CoastUntil is { } until && _timing.CurTime < until)
            {
                console.LockQuality = Math.Max(0f, console.LockQuality - QualityDropPerSec * frameTime);
            }
            else
            {
                ClearLock(console);
            }
        }
    }

    public RadarLockStateDto BuildLockState(EntityUid console, FireControlConsoleComponent consoleComp)
    {
        var dto = new RadarLockStateDto
        {
            RadarTrackEnabled = consoleComp.RadarTrackEnabled,
            LockedTarget = consoleComp.LockedTarget is { } t ? GetNetEntity(t) : null,
            Quality = consoleComp.LockQuality,
            ServerTime = _timing.CurTime,
        };

        if (!IsLockValid(console, consoleComp) || consoleComp.LockedTarget is not { } target)
        {
            dto.LockValid = false;
            return dto;
        }

        dto.LockValid = true;

        var live = CanLock(console, target);
        dto.Coasting = !live;

        Vector2 tgtPos;
        Vector2 tgtVel;
        if (live)
        {
            tgtPos = _xform.GetWorldPosition(target);
            tgtVel = TryComp<PhysicsComponent>(target, out var tp) ? tp.LinearVelocity : Vector2.Zero;
        }
        else
        {
            var dt = (float)(_timing.CurTime - (consoleComp.CoastUntil!.Value - TimeSpan.FromSeconds(CoastSeconds))).TotalSeconds;
            dt = Math.Clamp(dt, 0f, CoastSeconds);
            tgtPos = consoleComp.CoastPos + consoleComp.CoastVel * dt;
            tgtVel = consoleComp.CoastVel;
        }

        dto.TargetWorldPosition = tgtPos;
        dto.TargetWorldVelocity = tgtVel;
        dto.Chaffed = Transform(console).GridUid is { } og
                      && IsBearingChaffed(og, Transform(target).GridUid ?? target);

        if (consoleComp.LockQuality < LeadMinQuality)
            return dto;

        if (!live || !CanProvideLead(console, target))
            return dto;

        if (Transform(console).GridUid is not { } ourGrid)
            return dto;

        var ourVel = TryComp<PhysicsComponent>(ourGrid, out var op) ? op.LinearVelocity : Vector2.Zero;
        var pairs = CollectLeadOrigins(console, consoleComp);
        if (pairs.Count == 0)
            return dto;

        var avgSpeed = 0f;
        var avgOrigin = Vector2.Zero;
        foreach (var (origin, speed) in pairs)
        {
            avgSpeed += speed;
            avgOrigin += origin;
        }

        avgSpeed /= pairs.Count;
        avgOrigin /= pairs.Count;

        var atten = GetBearingAttenuation(ourGrid, Transform(target).GridUid ?? target);
        var q = Math.Clamp(consoleComp.LockQuality, 0f, 1f);
        var track = Math.Clamp(q * atten, 0f, 1f);
        var jam = 1f - track;

        var range = (tgtPos - ourGrid.ToCoordinates().Position).Length();
        var jitter = jam * Math.Clamp(range * 0.22f, 40f, 400f);
        if (atten < 0.55f)
            jitter *= 2f;
        var contactFlags = SensorContactFlags.None;
        if (TryGetContact(console, target, out _, out var contact))
            contactFlags = contact.Flags;
        if ((contactFlags & SensorContactFlags.Decoy) != 0)
            jitter = MathF.Max(jitter, Math.Clamp(range * 0.35f, 80f, 500f));

        var (avgLead, avgT) = ComputeLeadPoint(avgOrigin, ourVel, tgtPos, tgtVel, avgSpeed);
        dto.AverageLeadWorld = avgLead;
        dto.TimeToIntercept = avgT > 0.05f && avgT < 120f ? avgT : null;

        dto.WeaponLeads = new List<RadarWeaponLeadDto>(pairs.Count);
        foreach (var (origin, speed, weapon) in CollectLeadWeapons(console, consoleComp))
        {
            var (lead, tti) = ComputeLeadPoint(origin, ourVel, tgtPos, tgtVel, speed);
            dto.WeaponLeads.Add(new RadarWeaponLeadDto
            {
                Weapon = GetNetEntity(weapon),
                ProjectileSpeed = speed,
                LeadWorld = lead,
                TimeToIntercept = tti,
            });
        }

        return dto;

        Vector2 Jitter(Vector2 lead, EntityUid seedEnt)
        {
            if (jitter < 1f)
                return lead;

            var seed = seedEnt.GetHashCode() ^ (int)(_timing.CurTick.Value * 397);
            var rng = new Random(seed);
            var ang = (float)(rng.NextDouble() * Math.PI * 2.0);
            var mag = jitter * (0.5f + (float)rng.NextDouble() * 0.5f);
            return lead + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * mag;
        }
    }

    private List<(Vector2 Origin, float Speed)> CollectLeadOrigins(
        EntityUid console,
        FireControlConsoleComponent consoleComp)
    {
        var result = new List<(Vector2, float)>();
        foreach (var (origin, speed, _) in CollectLeadWeapons(console, consoleComp))
        {
            result.Add((origin, speed));
        }
        return result;
    }

    public List<(Vector2 Origin, float Speed, EntityUid Weapon)> CollectLeadWeapons(
        EntityUid console,
        FireControlConsoleComponent consoleComp)
    {
        var result = new List<(Vector2, float, EntityUid)>();

        if (consoleComp.ConnectedServer is not { } serverUid ||
            !TryComp<FireControlServerComponent>(serverUid, out var server))
            return result;

        foreach (var weapon in server.Controlled)
        {
            if (!Exists(weapon))
                continue;
            if (HasComp<BallisticLeadIgnoreComponent>(weapon))
                continue;
            if (!TryComp<GunComponent>(weapon, out var gun))
                continue;

            var speed = gun.ProjectileSpeedModified > 0.01f
                ? gun.ProjectileSpeedModified
                : gun.ProjectileSpeed;
            if (speed < 0.01f)
                continue;

            var origin = _xform.GetWorldPosition(weapon);
            result.Add((origin, speed, weapon));
        }

        return result;
    }

    public List<(EntityUid Weapon, float Speed)> CollectLeadSpeeds(
        EntityUid console,
        FireControlConsoleComponent consoleComp)
    {
        var result = new List<(EntityUid, float)>();
        foreach (var (_, speed, weapon) in CollectLeadWeapons(console, consoleComp))
        {
            result.Add((weapon, speed));
        }
        return result;
    }

    /// <summary>
    /// Returns aim point (world) and intercept time. Aim point accounts for shooter velocity.
    /// </summary>
    public static (Vector2 Lead, float Time) ComputeLeadPoint(
        Vector2 shooterPos,
        Vector2 shooterVel,
        Vector2 targetPos,
        Vector2 targetVel,
        float projectileSpeed)
    {
        if (projectileSpeed < 0.01f)
            return (targetPos, 0f);

        var relPos = targetPos - shooterPos;
        var relVel = targetVel - shooterVel;

        var a = Vector2.Dot(relVel, relVel) - projectileSpeed * projectileSpeed;
        var b = 2f * Vector2.Dot(relPos, relVel);
        var c = Vector2.Dot(relPos, relPos);

        float t;
        if (MathF.Abs(a) < 1e-4f)
        {
            t = MathF.Abs(b) < 1e-4f ? 0f : -c / b;
        }
        else
        {
            var disc = b * b - 4f * a * c;
            if (disc < 0f)
                return (targetPos, 0f);
            var sqrt = MathF.Sqrt(disc);
            var t1 = (-b + sqrt) / (2f * a);
            var t2 = (-b - sqrt) / (2f * a);
            t = t1 > 0f && t2 > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
        }

        if (t < 0f || float.IsNaN(t) || float.IsInfinity(t))
            return (targetPos, 0f);

        return (targetPos + (targetVel - shooterVel) * t, t);
    }

    public static (Vector2 Lead, float Time) ComputeMissileLeadPoint(
        Vector2 shooterPos,
        Vector2 shooterVel,
        Vector2 targetPos,
        Vector2 targetVel,
        float projectileSpeed)
    {
        if (projectileSpeed < 0.01f)
            return (targetPos, 0f);

        var relPos = targetPos - shooterPos;
        var relVel = targetVel;
        var a = Vector2.Dot(relVel, relVel) - projectileSpeed * projectileSpeed;
        var b = 2f * Vector2.Dot(relPos, relVel);
        var c = Vector2.Dot(relPos, relPos);

        float t;
        if (MathF.Abs(a) < 1e-4f)
            t = MathF.Abs(b) < 1e-4f ? 0f : -c / b;
        else
        {
            var disc = b * b - 4f * a * c;
            if (disc < 0f)
                return (targetPos, 0f);
            var sqrt = MathF.Sqrt(disc);
            var t1 = (-b + sqrt) / (2f * a);
            var t2 = (-b - sqrt) / (2f * a);
            t = t1 > 0f && t2 > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
        }

        if (t < 0f || float.IsNaN(t) || float.IsInfinity(t))
            return (targetPos, 0f);

        t = MathF.Min(t, 30f);
        return (targetPos + targetVel * t, t);
    }
}
