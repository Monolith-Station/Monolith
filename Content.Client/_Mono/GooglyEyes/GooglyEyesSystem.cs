using Content.Shared._Mono.GooglyEyes;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Mono.GooglyEyes;

public sealed partial class GooglyEyesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<GooglyEyesComponent>();

        while (query.MoveNext(out var uid, out var eyes))
        {
            if (!_sprite.TryGetLayer(uid, eyes.Layer, out var layer, true))
                continue;

            var eyePos = eyes.Coordinates;
            var worldRotation = _transform.GetWorldRotation(uid);
            var worldVel = _physics.GetMapLinearVelocity(uid);
            var relEyeVel = eyes.Velocity - worldVel;

            var deltaVel = Vector2.Zero;

            var newPos = eyePos + relEyeVel * frameTime;
            var radius = newPos.Length();
            // if we went out of range, snap to range and kill normal velocity
            if (radius > eyes.Radius)
            {
                var normPos = newPos / radius;
                newPos = normPos * eyes.Radius;

                var normVel = normPos * Vector2.Dot(normPos, relEyeVel);
                deltaVel -= normVel * (1f + eyes.Bounciness);
            }

            var frictionVel = relEyeVel + deltaVel;
            deltaVel += -frictionVel * (1f - MathF.Pow(eyes.Friction, frameTime));

            eyes.Velocity += deltaVel;
            eyes.Coordinates = newPos;
            var newOffset = (-worldRotation).RotateVec(newPos);
            _sprite.LayerSetOffset(layer, newOffset);
        }
    }
}
