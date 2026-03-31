// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Map.Components;
using Content.Server.Shuttles.Components;

namespace Content.Server._Lua.Physics;

[UsedImplicitly]
public sealed class AutoUnstuckSystem : EntitySystem
{
    private static readonly Vector2[] StuckOffsets =
    {
        new(2f, 0f),
        new(-2f, 0f),
        new(0f, 2f),
        new(0f, -2f),
    };

    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private readonly Dictionary<EntityUid, float> _stuckTime = new();
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private readonly List<EntityUid> _toClear = new();
    private readonly List<EntityUid> _awake = new();

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _toClear.Clear();
        _awake.Clear();

        foreach (var ent in _physics.AwakeBodies)
        {
            _awake.Add(ent.Owner);
        }

        foreach (var uid in _awake)
        {
            if (!_physicsQuery.TryGetComponent(uid, out var body)) continue;
            if (body.BodyType == BodyType.Static || !body.CanCollide) continue;
            if (HasComp<MapGridComponent>(uid) || HasComp<MapComponent>(uid) || HasComp<ShuttleComponent>(uid)) continue;
            if (IsPaused(uid)) continue;
            var hasStaticHardContact = false;
            var dirSum = Vector2.Zero;
            var contacts = _physics.GetContacts(uid);
            while (contacts.MoveNext(out var contact))
            {
                if (!contact.IsTouching || !contact.Hard) continue;
                var other = contact.OtherEnt(uid);
                var otherBody = contact.OtherBody(uid);
                if (otherBody.BodyType != BodyType.Static) continue;
                var selfTx = _physics.GetPhysicsTransform(uid);
                var otherTx = _physics.GetPhysicsTransform(other);
                var dir = selfTx.Position - otherTx.Position;
                if (dir != Vector2.Zero) dirSum += Vector2.Normalize(dir);
                hasStaticHardContact = true;
            }
            if (!hasStaticHardContact)
            {
                _toClear.Add(uid);
                continue;
            }
            if (_stuckTime.TryGetValue(uid, out var t)) _stuckTime[uid] = t + frameTime;
            else _stuckTime[uid] = frameTime;
            if (_stuckTime[uid] < 15f) continue;
            if (_xformQuery.TryGetComponent(uid, out var xform))
            {
                var offset = _random.Pick(StuckOffsets);
                _physics.SetCanCollide(uid, false, body: body);
                _xform.SetCoordinates(uid, xform, xform.Coordinates.Offset(offset));
                _physics.SetCanCollide(uid, true, body: body);
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
                _physics.WakeBody(uid, body: body);
            }
            _toClear.Add(uid);
        }
        foreach (var uid in _toClear)
        {
            _stuckTime.Remove(uid);
        }
    }
}

