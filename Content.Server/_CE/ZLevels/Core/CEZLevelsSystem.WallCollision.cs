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

public sealed partial class CEZLevelsSystem
{
    private const float WallRestitution = 0.4f;

    private const float WallBounceMinSpeed = 0.4f;

    private const float WallPenetrationDeadZone = 0.01f;

    private readonly List<CESharedZLevelsSystem.CEWallContact> _wallContacts = new();

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
        var wallPosX = 0f; // a wall on the hull's +X side (hull rammed it moving +X)
        var wallNegX = 0f;
        var wallPosY = 0f;
        var wallNegY = 0f;

        foreach (var contact in _wallContacts)
        {
            if (contact.Depth <= WallPenetrationDeadZone)
                continue;

            if (contact.Normal.Y == 0f && contact.Normal.X < 0f)
                wallPosX = MathF.Max(wallPosX, contact.Depth);
            else if (contact.Normal.Y == 0f && contact.Normal.X > 0f)
                wallNegX = MathF.Max(wallNegX, contact.Depth);
            else if (contact.Normal.X == 0f && contact.Normal.Y < 0f)
                wallPosY = MathF.Max(wallPosY, contact.Depth);
            else if (contact.Normal.X == 0f && contact.Normal.Y > 0f)
                wallNegY = MathF.Max(wallNegY, contact.Depth);
        }

        var displacement = new Vector2(wallNegX - wallPosX, wallNegY - wallPosY);
        var angledNormal = Vector2.Zero;
        var angledDepth = WallPenetrationDeadZone;
        foreach (var contact in _wallContacts)
        {
            if (contact.Normal.X == 0f || contact.Normal.Y == 0f)
                continue;

            var remaining = contact.Depth - Vector2.Dot(displacement, contact.Normal);
            if (remaining <= angledDepth)
                continue;

            angledDepth = remaining;
            angledNormal = contact.Normal;
        }

        displacement += angledNormal * angledDepth;
        if (displacement == Vector2.Zero)
            return;

        _transform.SetWorldPosition(grid, _transform.GetWorldPosition(grid) + displacement);

        var velocity = body.LinearVelocity;
        velocity.X = ReflectAxis(velocity.X, wallOnPositiveSide: wallPosX > 0f, wallOnNegativeSide: wallNegX > 0f);
        velocity.Y = ReflectAxis(velocity.Y, wallOnPositiveSide: wallPosY > 0f, wallOnNegativeSide: wallNegY > 0f);
        var normalSpeed = Vector2.Dot(velocity, angledNormal);
        if (normalSpeed < 0f)
        {
            var rebound = -normalSpeed < WallBounceMinSpeed ? 0f : -normalSpeed * WallRestitution;
            velocity += angledNormal * (rebound - normalSpeed);
        }

        if (velocity != body.LinearVelocity)
            _physics.SetLinearVelocity(grid, velocity, body: body);
    }

    private static float ReflectAxis(float v, bool wallOnPositiveSide, bool wallOnNegativeSide)
    {
        if (wallOnPositiveSide && v > 0f || wallOnNegativeSide && v < 0f)
            return MathF.Abs(v) < WallBounceMinSpeed ? 0f : -v * WallRestitution;

        return v;
    }
}
