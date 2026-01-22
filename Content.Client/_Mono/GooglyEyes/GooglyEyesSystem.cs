using Content.Shared._Mono.GooglyEyes;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Mono.GooglyEyes;

public sealed partial class GooglyEyesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
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
            var newPos = eyePos + relEyeVel * frameTime;

            var radius = newPos.Length();
            // if we went out of range, snap to range and kill normal velocity
            if (radius > eyes.Radius)
            {
                var normPos = newPos / radius;
                newPos = normPos * eyes.Radius;

                var normVel = normPos * Vector2.Dot(normPos, relEyeVel);
                relEyeVel -= normVel * (1f + eyes.Bounciness);
            }

            relEyeVel *= MathF.Exp(-eyes.Friction * frameTime);

            if (relEyeVel.Length() < eyes.LookMaxVelocity && _random.Prob(eyes.LookProb * frameTime) && _player.LocalEntity is { } player)
            {
                var worldPos = _transform.GetWorldPosition(uid);
                var playerPos = _transform.GetWorldPosition(player);
                var lookDir = playerPos - worldPos;
                lookDir.Normalize(); // does zero check
                var lookPos = eyes.Radius * lookDir;
                var lookDelta = (lookPos - eyePos).Length();
                var velDelta = lookDelta * eyes.Friction;
                relEyeVel += lookDir * velDelta;
            }

            eyes.Velocity = relEyeVel + worldVel;
            eyes.Coordinates = newPos;
            var newOffset = (-worldRotation).RotateVec(newPos);
            _sprite.LayerSetOffset(layer, newOffset);
        }
    }
}
