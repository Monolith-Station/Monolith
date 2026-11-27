/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>Vertical shuttle controls require a powered gravity generator.</summary>
public sealed partial class CEZLevelsSystem
{
    [Dependency] private IRobustRandom _random = default!;

    private const float SpoolSeconds = 1.5f;

    private static readonly EntProtoId LiftoffDustProto = "CEZLiftoffDust";

    private const float SpoolLiftoffVelocity = 1.5f;

    private static readonly TimeSpan SpoolInputGap = TimeSpan.FromSeconds(0.25);

    private const float VerticalThrustScale = 0.05f;
    private const float MaxVerticalAccel = 0.75f;

    /// <summary>Piloted speed cap in levels per second.</summary>
    private const float MaxPilotVerticalSpeed = 0.5f;

    /// <summary>Vertical acceleration from the gravity generator alone.</summary>
    private const float HoverDampAccel = 0.3f;

    /// <summary>
    /// Release-to-settle: a gravgen'd ship idling within this fraction of a plane drifts onto it and lands.
    /// </summary>
    private const float SettleZone = 0.25f;

    /// <summary>
    /// Within this fraction of a plane a settling ship counts as touched down.
    /// </summary>
    private const float TouchdownProgress = 0.01f;

    /// <summary>
    /// Descents that end on the plane below get slowed down to this.
    /// </summary>
    private const float ApproachGain = 1.2f;
    private const float TouchdownSpeed = 0.06f;

    /// <summary>
    /// A settling ship only exits transit once its vertical speed is at most this (levels/second).
    /// </summary>
    private const float ExitTransitMaxSpeed = 0.1f;

    // Keep the ceiling visible while the ship is pinned against it.
    private const float CeilingClearance = 0.05f;

    private readonly Dictionary<EntityUid, float> _pilotVerticalInput = new();

    private readonly HashSet<EntityUid> _spoolingGrids = new();
    private readonly HashSet<EntityUid> _spoolingGridsThisTick = new();

    private void CollectPilotVerticalInputs()
    {
        _pilotVerticalInput.Clear();

        var query = EntityQueryEnumerator<PilotComponent>();
        while (query.MoveNext(out _, out var pilot))
        {
            if (pilot.Console is not { } console || TerminatingOrDeleted(console))
                continue;

            if (Transform(console).GridUid is not { } grid)
                continue;

            var vertical = 0f;
            if ((pilot.HeldButtons & ShuttleButtons.AscendZ) != 0x0)
                vertical += 1f;
            if ((pilot.HeldButtons & ShuttleButtons.DescendZ) != 0x0)
                vertical -= 1f;

            if (vertical == 0f)
                continue;

            _pilotVerticalInput[grid] =
                Math.Clamp(_pilotVerticalInput.GetValueOrDefault(grid) + vertical, -1f, 1f);
        }
    }

    private float GetTransitVerticalInput(EntityUid transitMap)
    {
        if (_pilotVerticalInput.Count == 0 ||
            TryComp<CEZTransitMapComponent>(transitMap, out var transit) && transit.AutoLand)
            return 0f;

        var total = 0f;
        foreach (var (grid, input) in _pilotVerticalInput)
        {
            if (!TerminatingOrDeleted(grid) && Transform(grid).MapUid == transitMap)
                total += input;
        }

        return Math.Clamp(total, -1f, 1f);
    }

    /// <summary>
    /// Vertical acceleration available to a docked set, in levels/s²: the sum of
    /// every member's thrusters over the set's total mass.
    /// </summary>
    private float GetVerticalThrustAccel(EntityUid grid)
    {
        var thrust = 0f;
        var mass = 0f;

        foreach (var member in CollectTransitSet(grid))
        {
            if (TryComp<ShuttleComponent>(member, out var shuttle))
            {
                foreach (var directional in shuttle.LinearThrust)
                    thrust += directional;
            }

            if (TryComp<PhysicsComponent>(member, out var body))
                mass += body.FixturesMass;
        }

        if (thrust <= 0f || mass <= 0f)
            return 0f;

        return Math.Clamp(thrust / mass * VerticalThrustScale, 0f, MaxVerticalAccel);
    }

    private void UpdateTakeoffSpool()
    {
        _spoolingGridsThisTick.Clear();

        foreach (var (gridUid, input) in _pilotVerticalInput)
        {
            if (TerminatingOrDeleted(gridUid) || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var mapUid = Transform(gridUid).MapUid;
            if (mapUid == null || !HasComp<CEZMapComponent>(mapUid))
                continue;

            if (!TryComp<GravityComponent>(gridUid, out var gravity) || !gravity.Enabled)
                continue;

            var down = input < 0f;

            if (!down && !TryMapUp(mapUid.Value, out _))
            {
                var ev = new CEZOpenSkyExitAttemptEvent(mapUid.Value, true);
                RaiseLocalEvent(gridUid, ref ev);
                if (ev.Handled)
                    continue;
            }

            // Launch from terrain requires spool-up.
            var grounded = HasGroundContact(gridUid);

            if (down && (grounded || !TryMapDown(mapUid.Value, out _)))
                continue;

            if (!down && !TryMapUp(mapUid.Value, out _))
                continue;

            var faller = EnsureComp<CEZGridFallerComponent>(gridUid);

            if (grounded)
            {
                var now = _timing.CurTime;

                if (now - faller.SpoolLastInput > SpoolInputGap)
                    faller.SpoolStart = now;

                faller.SpoolLastInput = now;

                var remaining = SpoolSeconds - (float)(now - faller.SpoolStart).TotalSeconds;
                if (remaining > 0f)
                {
                    if (ZPhysicsQuery.TryComp(gridUid, out var spoolPhys))
                        SetLaunchCountdown((gridUid, spoolPhys), remaining);
                    _spoolingGridsThisTick.Add(gridUid);
                    continue;
                }
            }

            if (TryEnterTransit((gridUid, grid), preferUpperGap: !down))
            {
                if (grounded && !down)
                {
                    faller.Velocity = -SpoolLiftoffVelocity;
                    SpawnLiftoffDust(gridUid, mapUid.Value);
                }
                else
                {
                    faller.Velocity = down ? TouchdownSpeed : -TouchdownSpeed;
                }
            }
        }

        foreach (var prev in _spoolingGrids)
        {
            if (!_spoolingGridsThisTick.Contains(prev) && ZPhysicsQuery.TryComp(prev, out var prevPhys))
                SetLaunchCountdown((prev, prevPhys), 0f);
        }

        _spoolingGrids.Clear();
        _spoolingGrids.UnionWith(_spoolingGridsThisTick);
    }

    private void SpawnLiftoffDust(EntityUid grid, EntityUid groundMap)
    {
        var hullTiles = new HashSet<Vector2i>();
        var puffTiles = new HashSet<Vector2i>();
        var toGround = _transform.GetInvWorldMatrix(groundMap);
        foreach (var member in CollectGridSet(grid))
        {
            if (!TryComp<MapGridComponent>(member, out var memberGrid))
                continue;

            hullTiles.Clear();
            puffTiles.Clear();
            var size = memberGrid.TileSize;
            var toGroundLocal = _transform.GetWorldMatrix(member) * toGround;
            var tiles = _map.GetAllTilesEnumerator(member, memberGrid);
            while (tiles.MoveNext(out var tile))
                hullTiles.Add(tile.Value.GridIndices);

            foreach (var tile in hullTiles)
            {
                for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 1; x++)
                {
                    var neighbor = tile + new Vector2i(x, y);
                    if (hullTiles.Contains(neighbor) || !puffTiles.Add(neighbor))
                        continue;

                    var puff = (new Vector2(neighbor.X + 0.5f, neighbor.Y + 0.5f) + _random.NextVector2(0.25f)) * size;
                    Spawn(LiftoffDustProto, new EntityCoordinates(groundMap, Vector2.Transform(puff, toGroundLocal)));
                }
            }
        }
    }
}
