using System.Numerics;
using Content.Shared._Mono.Fixtures;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Mono.Fixtures;

public sealed partial class DirectionalHullSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private OccluderSystem _occluder = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DirectionalHullComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DirectionalHullComponent, MoveEvent>(OnMove);
    }

    private void OnStartup(Entity<DirectionalHullComponent> ent, ref ComponentStartup args) => UpdateHull(ent);

    private void OnMove(Entity<DirectionalHullComponent> ent, ref MoveEvent args)
    {
        if (args.OldRotation != args.NewRotation)
            UpdateHull(ent);
    }

    private void UpdateHull(Entity<DirectionalHullComponent> ent)
    {
        var dir = Transform(ent).LocalRotation.GetCardinalDir();
        if ((ent.Comp.Hulls.GetValueOrDefault(dir) ?? PrototypeHull(ent)) is not { } hull)
            return;

        if (_fixtures.GetFixtureOrNull(ent, ent.Comp.Fixture) is { Shape: PolygonShape poly } fixture)
            _physics.SetVertices(ent, ent.Comp.Fixture, fixture, poly, hull);

        if (ent.Comp.Occluder && TryComp<OccluderComponent>(ent, out var occluder))
            _occluder.SetPolygon(ent, [.. hull], occluder);
    }

    private Vector2[]? PrototypeHull(Entity<DirectionalHullComponent> ent)
    {
        if (MetaData(ent).EntityPrototype is not { } proto
            || !proto.TryGetComponent<FixturesComponent>(out var fixtures, EntityManager.ComponentFactory)
            || !fixtures.Fixtures.TryGetValue(ent.Comp.Fixture, out var fixture)
            || fixture.Shape is not PolygonShape poly)
            return null;

        return poly.Vertices;
    }
}
