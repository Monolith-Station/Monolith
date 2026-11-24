using System.Numerics;
using Content.Server._Mono.FireControl;
using Content.Server.Power.EntitySystems;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Sensors;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Mono.Sensors;

public sealed partial class RadarLockSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private RadarCountermeasureSystem _chaff = default!;
    [Dependency] private PowerReceiverSystem _power = default!;

    public bool HasLockCapableRadar(EntityUid grid)
    {
        return HasCapableRadar(grid, requireLock: true, requireGuidance: false);
    }

    public bool HasGuidanceCapableRadar(EntityUid grid)
    {
        return HasCapableRadar(grid, requireLock: false, requireGuidance: true);
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
        var ourGrid = Transform(console).GridUid;
        if (ourGrid == null)
            return false;

        if (!HasLockCapableRadar(ourGrid.Value))
            return false;

        if (!TryComp<GridSensorsComponent>(ourGrid, out var sensors))
            return false;

        var targetGrid = Transform(targetEntity).GridUid ?? targetEntity;
        var net = GetNetEntity(targetGrid);
        if (!sensors.Contacts.TryGetValue(net, out var contact))
            return false;

        var hasArs = (contact.Flags & SensorContactFlags.ActiveRadar) != 0
                     || (contact.Flags & SensorContactFlags.Datalink) != 0;
        if (!hasArs)
            return false;

        if (IsBearingChaffed(ourGrid.Value, targetGrid))
        {
            var support =
                (contact.Flags & SensorContactFlags.PassiveRadar) != 0
                || (contact.Flags & SensorContactFlags.Infrared) != 0
                || (contact.Flags & SensorContactFlags.Visual) != 0;
            if (!support)
                return false;
        }

        return true;
    }

    public bool IsLockValid(EntityUid console, FireControlConsoleComponent consoleComp)
    {
        if (consoleComp.LockedTarget is not { } target)
            return false;

        if (!Exists(target))
        {
            consoleComp.LockedTarget = null;
            return false;
        }

        if (!CanLock(console, target))
        {
            consoleComp.LockedTarget = null;
            return false;
        }

        return true;
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

        return worst < 0.55f;
    }

    public bool IsBearingChaffedForConsole(EntityUid console, EntityUid targetEntity)
    {
        var ourGrid = Transform(console).GridUid;
        var tgtGrid = Transform(targetEntity).GridUid ?? targetEntity;
        if (ourGrid == null)
            return false;
        return IsBearingChaffed(ourGrid.Value, tgtGrid);
    }

    public RadarLockStateDto BuildLockState(EntityUid console, FireControlConsoleComponent consoleComp)
    {
        var dto = new RadarLockStateDto
        {
            RadarTrackEnabled = consoleComp.RadarTrackEnabled,
            LockedTarget = consoleComp.LockedTarget is { } t ? GetNetEntity(t) : null,
        };

        if (!IsLockValid(console, consoleComp) || consoleComp.LockedTarget is not { } target)
        {
            dto.LockValid = false;
            return dto;
        }

        dto.LockValid = true;
        dto.TargetWorldPosition = _xform.GetWorldPosition(target);
        dto.Chaffed = Transform(console).GridUid is { } og && IsBearingChaffed(og, Transform(target).GridUid ?? target);

        if (Transform(console).GridUid is not { } ourGrid)
            return dto;

        var ourPos = _xform.GetWorldPosition(ourGrid);
        var ourVel = TryComp<PhysicsComponent>(ourGrid, out var op) ? op.LinearVelocity : Vector2.Zero;
        var tgtPos = dto.TargetWorldPosition.Value;
        var tgtVel = TryComp<PhysicsComponent>(target, out var tp) ? tp.LinearVelocity : Vector2.Zero;

        var pairs = CollectLeadSpeeds(console, consoleComp);
        if (pairs.Count == 0)
            return dto;

        var avg = 0f;
        foreach (var (_, speed) in pairs)
        {
            avg += speed;
        }
        avg /= pairs.Count;

        dto.AverageLeadWorld = ComputeLeadPoint(ourPos, ourVel, tgtPos, tgtVel, avg);
        dto.WeaponLeads = new List<RadarWeaponLeadDto>(pairs.Count);
        foreach (var (weapon, speed) in pairs)
        {
            dto.WeaponLeads.Add(new RadarWeaponLeadDto
            {
                Weapon = GetNetEntity(weapon),
                ProjectileSpeed = speed,
                LeadWorld = ComputeLeadPoint(ourPos, ourVel, tgtPos, tgtVel, speed),
            });
        }

        return dto;
    }

    public List<(EntityUid Weapon, float Speed)> CollectLeadSpeeds(
        EntityUid console,
        FireControlConsoleComponent consoleComp)
    {
        var result = new List<(EntityUid, float)>();

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

            result.Add((weapon, speed));
        }

        return result;
    }

    public static Vector2 ComputeLeadPoint(
        Vector2 shooterPos,
        Vector2 shooterVel,
        Vector2 targetPos,
        Vector2 targetVel,
        float projectileSpeed)
    {
        if (projectileSpeed < 0.01f)
            return targetPos;

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
                return targetPos;
            var sqrt = MathF.Sqrt(disc);
            var t1 = (-b + sqrt) / (2f * a);
            var t2 = (-b - sqrt) / (2f * a);
            t = t1 > 0f && t2 > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
        }

        if (t < 0f || float.IsNaN(t) || float.IsInfinity(t))
            return targetPos;

        return targetPos + (targetVel - shooterVel) * t;
    }
}
