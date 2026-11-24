using System.Collections.Generic;
using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class ZLevelMapNetworkTest
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task DeletingTransitAnchorReanchorsOrLands(bool lostLower, bool hasReplacement)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var levels = server.System<CEZLevelsSystem>();
        var grids = server.ResolveDependency<IMapManager>();
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var network = levels.CreateMapNetwork();
            var lower = maps.CreateMap(out _);
            var upper = maps.CreateMap(out _);
            var gap = maps.CreateMap(out _);
            var layers = new Dictionary<EntityUid, int> { [lower] = 0, [upper] = 1 };
            EntityUid? replacement = null;
            if (hasReplacement)
            {
                replacement = maps.CreateMap(out _);
                layers.Add(replacement.Value, lostLower ? -2 : 3);
            }
            Assert.That(levels.TryAddMapsIntoNetwork(network, layers), Is.True);

            var transit = entities.AddComponent<CEZTransitMapComponent>(gap);
            transit.LowerMap = lower;
            transit.UpperMap = upper;
            var ship = grids.CreateGridEntity(gap);
            var grid = ship.Owner;
            maps.SetTile(grid, ship.Comp, Vector2i.Zero, new Tile(1));
            transit.PrimaryGrid = grid;
            var physics = entities.GetComponent<CEZPhysicsComponent>(grid);
            levels.SetZPosition((grid, physics), 0.5f);
            levels.SetZVelocity((grid, physics), -0.4f);
            var passenger = entities.SpawnEntity(null, new EntityCoordinates(grid, 0.5f, 0.5f));
            Assert.That(entities.GetComponent<TransformComponent>(passenger).ParentUid, Is.EqualTo(grid));

            entities.DeleteEntity(lostLower ? lower : upper);

            Assert.That(entities.GetComponent<TransformComponent>(passenger).ParentUid, Is.EqualTo(grid));
            var expected = hasReplacement ? gap : lostLower ? upper : lower;
            Assert.That(entities.GetComponent<TransformComponent>(grid).MapUid, Is.EqualTo(expected));
            Assert.That(entities.GetComponent<TransformComponent>(passenger).MapUid, Is.EqualTo(expected));
            Assert.That(entities.IsQueuedForDeletion(gap), Is.EqualTo(!hasReplacement));

            if (hasReplacement)
            {
                Assert.That(transit.LowerMap, Is.EqualTo(lostLower ? replacement : lower));
                Assert.That(transit.UpperMap, Is.EqualTo(lostLower ? upper : replacement));
                Assert.That(levels.GetAbsoluteAltitude(grid), Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(physics.Velocity, Is.EqualTo(-0.4f));
            }
            else
            {
                Assert.That(physics.LocalPosition, Is.Zero);
                Assert.That(physics.Velocity, Is.Zero);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlanetArrivalKeepsAutoLandThroughOpenSky()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var levels = server.System<CEZLevelsSystem>();
        var grids = server.ResolveDependency<IMapManager>();
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var network = levels.CreateMapNetwork();
            var surface = maps.CreateMap(out _);
            var sky = maps.CreateMap(out _);
            var orbit = maps.CreateMap(out _);
            Assert.That(levels.TryAddMapsIntoNetwork(network, new() { [surface] = 0, [sky] = 1 }), Is.True);

            var grid = grids.CreateGridEntity(orbit);
            var xform = entities.GetComponent<TransformComponent>(grid);
            Assert.That(levels.TryInsertIntoOpenSky(grid, network, Vector2.Zero), Is.True);
            var entry = entities.GetComponent<CEZTransitMapComponent>(xform.MapUid!.Value);
            Assert.That(entry.AutoLand, Is.True);
            Assert.That(entry.UpperMap, Is.Null);

            Assert.That(levels.SetTransitAltitude(grid, 0.75f), Is.True);
            var gap = entities.GetComponent<CEZTransitMapComponent>(xform.MapUid!.Value);
            Assert.That(gap.AutoLand, Is.True);
            Assert.That(gap.LowerMap, Is.EqualTo(surface));
            Assert.That(levels.TryExitTransit(grid), Is.True);
            Assert.That(xform.MapUid, Is.EqualTo(surface));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NestedBodyStaysWithParentButCanStepOffTransitGrid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var levels = server.System<CEZLevelsSystem>();
        var transform = server.System<SharedTransformSystem>();
        var grids = server.ResolveDependency<IMapManager>();
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var network = levels.CreateMapNetwork();
            var lower = maps.CreateMap(out _);
            var gap = maps.CreateMap(out _);
            Assert.That(levels.TryAddMapsIntoNetwork(network, new() { [lower] = 0 }), Is.True);

            var transit = entities.AddComponent<CEZTransitMapComponent>(gap);
            transit.LowerMap = lower;
            var grid = grids.CreateGridEntity(gap).Owner;
            transit.PrimaryGrid = grid;
            var gridPhysics = entities.GetComponent<CEZPhysicsComponent>(grid);
            levels.SetZPosition((grid, gridPhysics), 0.6f);

            var fixture = entities.SpawnEntity(null, new EntityCoordinates(grid, 0, 0));
            var rider = entities.SpawnEntity(null, new EntityCoordinates(grid, 0, 0));
            var riderPhysics = entities.AddComponent<CEZPhysicsComponent>(rider);
            levels.SetZPosition((rider, riderPhysics), 0.2f);
            levels.SetZVelocity((rider, riderPhysics), -0.3f);
            transform.SetCoordinates(rider, new EntityCoordinates(fixture, 0, 0));

            Assert.That(riderPhysics.LocalPosition, Is.Zero);
            Assert.That(riderPhysics.Velocity, Is.Zero);
            levels.WakeBody((rider, riderPhysics));
            Assert.That(levels.ActiveBodies, Does.Not.Contain(rider));
            Assert.That(levels.TryMoveDown(rider), Is.False);

            transform.SetCoordinates(rider, new EntityCoordinates(grid, 0, 0));
            transform.SetCoordinates(rider, new EntityCoordinates(gap, 1, 0));
            Assert.That(entities.GetComponent<TransformComponent>(rider).MapUid, Is.EqualTo(lower));
            Assert.That(riderPhysics.LocalPosition, Is.EqualTo(0.6f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MapEditsPreserveTransitAnchors()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var levels = server.System<CEZLevelsSystem>();
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var network = levels.CreateMapNetwork();
            var lower = maps.CreateMap(out _);
            var upper = maps.CreateMap(out _);
            var gap = maps.CreateMap(out _);
            entities.AddComponent<CEZMapComponent>(lower);
            Assert.That(levels.TryAddMapsIntoNetwork(network, new() { [lower] = 0 }), Is.True);

            var transit = entities.AddComponent<CEZTransitMapComponent>(gap);
            transit.LowerMap = lower;
            Assert.That(levels.TryAddMapsIntoNetwork(network, new() { [upper] = 1 }), Is.True);
            Assert.That(entities.GetComponent<CEZMapComponent>(lower).MapAbove, Is.EqualTo(upper));
            Assert.That(entities.GetComponent<CEZMapComponent>(upper).MapBelow, Is.EqualTo(lower));
            Assert.That(transit.UpperMap, Is.EqualTo(upper));
            Assert.That(levels.TryRemoveMapFromNetwork(network, upper), Is.False);

            entities.DeleteEntity(gap);
            Assert.That(levels.TryRemoveMapFromNetwork(network, upper), Is.True);
            Assert.That(entities.HasComponent<MapComponent>(upper), Is.True);
            Assert.That(entities.HasComponent<CEZMapComponent>(upper), Is.False);
            Assert.That(levels.TryMapUp(lower, out _), Is.False);

            Assert.That(levels.TryAddMapsIntoNetwork(network, new() { [upper] = 1 }), Is.True);
            entities.DeleteEntity(upper);
            Assert.That(levels.TryMapUp(lower, out _), Is.False);
            Assert.That(levels.TryGetMapAtDepth((network.Owner, network.Comp), 1, out _), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
