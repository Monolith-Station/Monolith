/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Bounces a grid flying at a z-level off the walls standing on that level's terrain.
///
/// This cannot go through the physics solver: the engine only collides a grid's tile geometry
/// against ANOTHER grid's tile geometry (grid-versus-loose-entity is a documented gap), and a wall
/// is an anchored entity, not a tile, so no fixture we could add is reachable from a moving hull. So
/// the hull-versus-wall test (<see cref="CESharedZLevelsSystem.GetWallContacts"/>) and the response
/// are done here by hand. Floors are the skid system's job; this is only the vertical obstructions.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// How much of the speed driven into a wall is returned as rebound. Below
    /// <see cref="WallBounceMinSpeed"/> the hull just stops against the wall instead of jittering on
    /// tiny reflected velocities.
    /// </summary>
    private const float WallRestitution = 0.4f;

    /// <summary>Speed (m/s) into a wall under which the hull stops dead rather than rebounding.</summary>
    private const float WallBounceMinSpeed = 0.4f;

    /// <summary>
    /// Ignore penetrations shallower than this (m) so a hull resting flush against a wall isn't
    /// nudged every tick by floating-point noise.
    /// </summary>
    private const float WallPenetrationDeadZone = 0.01f;

    private readonly List<CESharedZLevelsSystem.CEWallContact> _wallContacts = new();

    /// <summary>
    /// Pushes any flying grid out of the walls it has driven into and rebounds it.
    /// </summary>
    private void UpdateWallCollision()
    {
        var query = EntityQueryEnumerator<CEZGridFallerComponent, MapGridComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var body, out var xform))
        {
            if (body.BodyType == BodyType.Static) // Stopped.
                continue;

            if (xform.MapUid is not { } map
                || !_zMapQuery.HasComp(map) // wall collision only applies on Z levels
                || !_mapGridQuery.HasComp(map)) // only relevant if the map has a mapgrid
                continue;

            _wallContacts.Clear();

            GetWallContacts(uid, _wallContacts);
            if (_wallContacts.Count == 0)
                continue;

            ResolveWallCollision(uid, body);
        }
    }

    private void ResolveWallCollision(EntityUid grid, PhysicsComponent body)
    {
        // Per-axis minimum translation: separate each contacting hull tile along whichever axis needs
        // the least movement, keeping the deepest overlap found against a wall on each side. Corners
        // (walls on two sides) resolve on both axes independently, which is the right escape. Named by
        // the side the WALL is on, so the sign of the fix and the reflected velocity stay in step.
        var wallPosX = 0f; // a wall on the hull's +X side (hull rammed it moving +X)
        var wallNegX = 0f;
        var wallPosY = 0f;
        var wallNegY = 0f;

        foreach (var contact in _wallContacts)
        {
            var ship = contact.ShipTile;
            var wall = contact.WallTile;

            var overlapX = MathF.Min(ship.Right, wall.Right) - MathF.Max(ship.Left, wall.Left);
            var overlapY = MathF.Min(ship.Top, wall.Top) - MathF.Max(ship.Bottom, wall.Bottom);

            if (overlapX <= WallPenetrationDeadZone || overlapY <= WallPenetrationDeadZone)
                continue;

            if (overlapX < overlapY)
            {
                if (ship.Center.X < wall.Center.X)
                    wallPosX = MathF.Max(wallPosX, overlapX); // wall to the hull's right
                else
                    wallNegX = MathF.Max(wallNegX, overlapX);
            }
            else
            {
                if (ship.Center.Y < wall.Center.Y)
                    wallPosY = MathF.Max(wallPosY, overlapY);
                else
                    wallNegY = MathF.Max(wallNegY, overlapY);
            }
        }

        // Push away from whichever side carries a wall: a wall on +X shoves the hull in -X.
        var displacement = new Vector2(wallNegX - wallPosX, wallNegY - wallPosY);
        if (displacement == Vector2.Zero)
            return;

        _transform.SetWorldPosition(grid, _transform.GetWorldPosition(grid) + displacement);

        // Rebound only the velocity components actually driving into a wall, one axis at a time, so a
        // grazing hit on one axis leaves motion on the other untouched.
        var velocity = body.LinearVelocity;
        velocity.X = ReflectAxis(velocity.X, wallOnPositiveSide: wallPosX > 0f, wallOnNegativeSide: wallNegX > 0f);
        velocity.Y = ReflectAxis(velocity.Y, wallOnPositiveSide: wallPosY > 0f, wallOnNegativeSide: wallNegY > 0f);

        if (velocity != body.LinearVelocity)
            _physics.SetLinearVelocity(grid, velocity, body: body);
    }

    /// <summary>
    /// Reflects one velocity axis if it is moving into a wall on that side: a wall on the +side
    /// reverses +velocity, a wall on the -side reverses -velocity. Slow impacts stop instead of
    /// bouncing, so the hull doesn't jitter on tiny reflected speeds.
    /// </summary>
    private static float ReflectAxis(float v, bool wallOnPositiveSide, bool wallOnNegativeSide)
    {
        if (wallOnPositiveSide && v > 0f || wallOnNegativeSide && v < 0f)
            return MathF.Abs(v) < WallBounceMinSpeed ? 0f : -v * WallRestitution;

        return v;
    }
}
