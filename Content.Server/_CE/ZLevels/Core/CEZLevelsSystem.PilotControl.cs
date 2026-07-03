/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Shuttles.Components;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Pilot vertical flight: the shuttle console's ascend/descend keys drive a grid up
/// and down the z-network. All of it requires a powered gravity generator — without
/// one the ship is plummeting and the pilots have bigger fish to fry.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Seconds of continuously held ascend/descend before a ship parked on a GROUND
    /// layer lifts off. No takeoffs from a bumped key. Sky layers have nothing
    /// holding the ship down — leaving them is instant.
    /// </summary>
    private const float SpoolSeconds = 1.5f;

    /// <summary>
    /// A gap in held input longer than this restarts the takeoff spool.
    /// </summary>
    private static readonly TimeSpan SpoolInputGap = TimeSpan.FromSeconds(0.25);

    /// <summary>
    /// Converts the docked set's lateral acceleration (total thruster force over
    /// mass, in m/s²) into vertical acceleration in levels/s². Heavy ships with few
    /// thrusters climb sluggishly; overbuilt gunboats leap.
    /// </summary>
    private const float VerticalThrustScale = 0.05f;

    private const float MaxVerticalAccel = 0.75f;

    /// <summary>
    /// Speed limit for piloted vertical flight, in levels per second. Free fall
    /// (no gravgen) uses <see cref="GridTerminalVelocity"/> instead.
    /// </summary>
    private const float MaxPilotVerticalSpeed = 0.5f;

    /// <summary>
    /// The gravity generator's own authority: how hard it can damp vertical drift
    /// when hovering, and the settle rate for ships with no working thrusters.
    /// </summary>
    private const float HoverDampAccel = 0.3f;

    /// <summary>
    /// Release-to-settle: a gravgen'd ship idling within this fraction of a plane
    /// drifts onto it and lands. Keep the key held to punch through instead.
    /// </summary>
    private const float SettleZone = 0.25f;

    /// <summary>
    /// Within this fraction of a plane a settling ship counts as touched down.
    /// </summary>
    private const float TouchdownProgress = 0.01f;

    /// <summary>
    /// Descents that end on the plane below get capped to this speed profile:
    /// max(TouchdownSpeed, distance * ApproachGain). Arrive gently, not as a crater.
    /// </summary>
    private const float ApproachGain = 1.2f;
    private const float TouchdownSpeed = 0.06f;

    private readonly Dictionary<EntityUid, float> _pilotVerticalInput = new();

    /// <summary>
    /// Gathers each grid's net ascend/descend input from everyone at its consoles.
    /// Read straight off <see cref="PilotComponent.HeldButtons"/>: it stays current
    /// even for ground-parked ships whose shuttle is disabled, which is exactly
    /// what takeoff needs.
    /// </summary>
    private void CollectPilotVerticalInputs()
    {
        _pilotVerticalInput.Clear();

        var query = EntityQueryEnumerator<PilotComponent>();
        while (query.MoveNext(out _, out var pilot))
        {
            if (pilot.Console is not { } console || TerminatingOrDeleted(console))
                continue;

            var vertical = 0f;
            if ((pilot.HeldButtons & ShuttleButtons.AscendZ) != 0x0)
                vertical += 1f;
            if ((pilot.HeldButtons & ShuttleButtons.DescendZ) != 0x0)
                vertical -= 1f;

            if (vertical == 0f)
                continue;

            if (Transform(console).GridUid is not { } grid)
                continue;

            _pilotVerticalInput[grid] =
                Math.Clamp(_pilotVerticalInput.GetValueOrDefault(grid) + vertical, -1f, 1f);
        }
    }

    /// <summary>
    /// Net vertical input for the grid set occupying a transit map (every set member
    /// shares the map, so any member's consoles count).
    /// </summary>
    private float GetTransitVerticalInput(EntityUid transitMap)
    {
        if (_pilotVerticalInput.Count == 0)
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
    /// every member's thrusters over the set's total mass. Direction doesn't matter
    /// — every engine gimbals for lift.
    /// </summary>
    private float GetVerticalThrustAccel(EntityUid grid)
    {
        var thrust = 0f;
        var mass = 0f;

        foreach (var member in CollectGridSet(grid))
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

    /// <summary>
    /// Ships parked on a level surface lift off (or sink through their own plane)
    /// after their pilots hold ascend/descend for the full spool time.
    /// </summary>
    private void UpdateTakeoffSpool()
    {
        if (_pilotVerticalInput.Count == 0)
            return;

        foreach (var (gridUid, input) in _pilotVerticalInput)
        {
            if (TerminatingOrDeleted(gridUid) || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            // Airborne ships are handled by the transit integrator, not the spool.
            var mapUid = Transform(gridUid).MapUid;
            if (mapUid == null || !HasComp<CEZLevelMapComponent>(mapUid))
                continue;

            // No gravgen, no lift authority.
            if (!TryComp<GravityComponent>(gridUid, out var gravity) || !gravity.Enabled)
                continue;

            var down = input < 0f;
            var grounded = HasComp<CEZGroundLayerComponent>(mapUid);

            // You can't sink through the ground, and there has to be a gap below.
            if (down && (grounded || !TryMapDown(mapUid.Value, out _)))
                continue;

            // Going up needs SOME adjacent gap to become airborne in.
            if (!down && !TryMapUp(mapUid.Value, out _) && !TryMapDown(mapUid.Value, out _))
                continue;

            var faller = EnsureComp<CEZGridFallerComponent>(gridUid);

            // Only ground layers hold a ship down enough to need a spool-up;
            // hovering on a sky layer, the key just works.
            if (grounded)
            {
                var direction = (sbyte)(down ? -1 : 1);
                var now = _timing.CurTime;

                if (faller.SpoolDirection != direction || now - faller.SpoolLastInput > SpoolInputGap)
                {
                    faller.SpoolDirection = direction;
                    faller.SpoolStart = now;
                }

                faller.SpoolLastInput = now;

                if ((now - faller.SpoolStart).TotalSeconds < SpoolSeconds)
                    continue;
            }

            faller.SpoolDirection = 0;

            // Liftoffs use the gap above so the ship doesn't sweep (and smimsh) the
            // pad it's leaving; sinking naturally uses the gap below.
            if (TryEnterTransit((gridUid, grid), preferUpperGap: !down))
                faller.Velocity = down ? TouchdownSpeed : -TouchdownSpeed;
        }
    }
}
