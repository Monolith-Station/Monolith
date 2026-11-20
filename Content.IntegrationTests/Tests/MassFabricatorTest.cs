using System.Collections.Generic;
using System.Numerics;
using Content.Server._CE.ZCollapse;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Events;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using MassFabricatorSystem = Content.Server._Mono.MassFabricator.MassFabricatorSystem;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class MassFabricatorTest
{
    [Test]
    public async Task OffGridBatchPrintsOutwardInWaves()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var fab = entities.System<MassFabricatorSystem>();
        var maps = entities.System<SharedMapSystem>();
        var blueprint = server.ResolveDependency<IPrototypeManager>().Index<MassFabricatorBlueprintPrototype>("HZBlueprintPlating");
        Entity<MapGridComponent> ship = default;
        var plans = new EntityUid[3];

        await server.WaitAssertion(() =>
        {
            ship = server.MapMan.CreateGridEntity(map.MapId);
            entities.System<SharedTransformSystem>().SetWorldPosition(ship, new Vector2(30f, 40f));
            var edge = new Vector2i(15, 0);
            maps.SetTile(ship, ship, edge, new Tile(1));
            var position = maps.GridTileToLocal(ship, ship, edge);
            var user = entities.SpawnAttachedTo("MobHuman", position);
            var gun = entities.SpawnAttachedTo("HZMassFabricator", position);
            Assert.That(entities.System<SharedHandsSystem>().TryPickup(user, gun), Is.True);
            Assert.That(entities.System<SharedMaterialStorageSystem>().TryChangeMaterialAmount(gun, "Steel", 300), Is.True);
            var tool = entities.GetComponent<MassFabricatorComponent>(gun);
            entities.AddComponent<MassFabricatorViewComponent>(user).Fabricator = gun;
            for (var i = 0; i < plans.Length; i++)
            {
                var cell = maps.GridTileToLocal(ship, ship, new Vector2i(16 + i, 0));
                var plan = fab.CreatePlan(user, (gun, tool), blueprint, "Plating", cell, Angle.Zero);
                Assert.That(plan, Is.Not.Null);
                plans[i] = plan!.Value;
            }

            var click = new InteractUsingEvent(user, gun, plans[0], maps.GridTileToLocal(ship, ship, new Vector2i(16, 0)));
            entities.EventBus.RaiseLocalEvent(plans[0], click);
            Assert.That(click.Handled, Is.True);
            for (var i = 0; i < plans.Length; i++)
            {
                var plan = entities.GetComponent<MassFabricatorHologramComponent>(plans[i]);
                Assert.That(plan.PrintState, Is.EqualTo(i == 0 ? MassFabricatorPrintState.Printing : MassFabricatorPrintState.Idle));
                Assert.That(maps.GetTileRef(ship, plan.Cell).Tile.IsEmpty, Is.True);
            }
        });

        await server.WaitRunTicks(150);
        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < plans.Length; i++)
                Assert.That(maps.GetTileRef(ship, new Vector2i(16 + i, 0)).Tile.IsEmpty, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipPlansFollowTheirSupportAcrossChunksAndSplits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var fab = entities.System<MassFabricatorSystem>();
        var maps = entities.System<SharedMapSystem>();
        var transform = entities.System<SharedTransformSystem>();
        var blueprint = server.ResolveDependency<IPrototypeManager>().Index<MassFabricatorBlueprintPrototype>("HZBlueprintPlating");
        Entity<MapGridComponent> ship = default;
        EntityUid plan = default;
        EntityUid tail = default;
        EntityUid beyondTail = default;
        var support = new Vector2i(15, 0);
        MassFabricatorComponent tool = default!;

        await server.WaitAssertion(() =>
        {
            ship = server.MapMan.CreateGridEntity(map.MapId);
            transform.SetWorldPositionRotation(ship, new Vector2(30f, 40f), Angle.FromDegrees(30));
            for (var x = 12; x <= 15; x++)
                maps.SetTile(ship, ship, new Vector2i(x, 0), new Tile(1));

            var user = entities.SpawnAttachedTo(null, maps.GridTileToLocal(ship, ship, support));
            entities.AddComponent<EyeComponent>(user);
            var gun = entities.SpawnAttachedTo("HZMassFabricator", maps.GridTileToLocal(ship, ship, support));
            tool = entities.GetComponent<MassFabricatorComponent>(gun);
            entities.AddComponent<MassFabricatorViewComponent>(user).Fabricator = gun;

            plan = PlanAt(16);
            tail = PlanAt(17);
            beyondTail = PlanAt(18);
            transform.SetWorldPositionRotation(ship, new Vector2(45f, 35f), Angle.FromDegrees(90));
            Assert.That(entities.GetComponent<TransformComponent>(plan).ParentUid, Is.EqualTo(ship.Owner));
            Assert.That(transform.GetWorldPosition(tail), Is.EqualTo(transform.ToMapCoordinates(maps.GridTileToLocal(ship, ship,
                new Vector2i(17, 0))).Position));

            // Overlap only the tail, and share an edge with the first plan.
            var obstacle = server.MapMan.CreateGridEntity(map.MapId);
            var origin = transform.ToMapCoordinates(new EntityCoordinates(ship, new Vector2(17, 0))).Position;
            transform.SetWorldPositionRotation(obstacle, origin, Angle.FromDegrees(90));
            maps.SetTile(obstacle, obstacle, Vector2i.Zero, new Tile(1));

            EntityUid PlanAt(int x)
            {
                var point = transform.ToMapCoordinates(maps.GridTileToLocal(ship, ship, new Vector2i(x, 0)));
                var result = fab.CreatePlan(user, (gun, tool), blueprint, "Plating", transform.ToCoordinates(point), Angle.Zero);
                Assert.That(result, Is.Not.Null);
                return result!.Value;
            }
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(tail), Is.False);
            Assert.That(entities.EntityExists(beyondTail), Is.False);
            Assert.That(entities.EntityExists(plan), Is.True);
            maps.SetTile(ship, ship, new Vector2i(14, 0), Tile.Empty);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var pending = entities.GetComponent<MassFabricatorHologramComponent>(plan);
            Assert.That(pending.BuildGrid, Is.Not.EqualTo(ship.Owner));
            Assert.That(pending.Cell, Is.EqualTo(new Vector2i(16, 0)));
            var fragment = pending.BuildGrid!.Value;
            Assert.That(entities.GetComponent<TransformComponent>(plan).ParentUid, Is.EqualTo(fragment));
            maps.SetTile(fragment, entities.GetComponent<MapGridComponent>(fragment), support, Tile.Empty);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(plan), Is.False);
            Assert.That(tool.BlueprintLoad, Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmptyUpperMapGridPlanTracksCollapseSupport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var maps = entities.System<SharedMapSystem>();
        var fab = entities.System<MassFabricatorSystem>();
        var levels = entities.System<CEZLevelsSystem>();
        var blueprint = server.ResolveDependency<IPrototypeManager>().Index<MassFabricatorBlueprintPrototype>("HZBlueprintPlating");
        EntityUid plan = default;
        var floor = new EntityUid[8];
        EntityUid upper = default;
        EntityUid lower = default;
        EntityUid pillar = default;
        var cell = new Vector2i(10, 10);

        await server.WaitAssertion(() =>
        {
            lower = maps.CreateMap(out _);
            upper = maps.CreateMap(out _);
            Assert.That(levels.TryAddMapsIntoNetwork(levels.CreateMapNetwork(),
                new Dictionary<EntityUid, int> { [lower] = 0, [upper] = 1 }), Is.True);
            var lowerGrid = entities.AddComponent<MapGridComponent>(lower);
            var upperGrid = entities.AddComponent<MapGridComponent>(upper);
            entities.EnsureComponent<CEGridStabilityComponent>(lower).SupportLowestLevel = true;
            entities.EnsureComponent<CEGridStabilityComponent>(upper);
            maps.SetTile(lower, lowerGrid, cell, new Tile(1));
            var lowerTarget = maps.GridTileToLocal(lower, lowerGrid, cell);
            var user = entities.SpawnAttachedTo(null, lowerTarget);
            var gun = entities.SpawnAttachedTo("HZMassFabricator", lowerTarget);
            entities.AddComponent<CEZLevelViewerComponent>(user);
            entities.EventBus.RaiseLocalEvent(user, new CEToggleZLevelLookUpAction());
            for (var i = 0; i < floor.Length; i++)
            {
                var coords = maps.GridTileToLocal(upper, upperGrid, cell + new Vector2i(i, 0));
                var pending = fab.CreatePlan(user, (gun, entities.GetComponent<MassFabricatorComponent>(gun)),
                    blueprint, "Plating", coords, Angle.Zero);
                Assert.That(pending, Is.Not.Null);
                floor[i] = pending!.Value;
                Assert.That(entities.GetComponent<TransformComponent>(floor[i]).ParentUid, Is.EqualTo(upper));
            }

            plan = floor[0];
            var wall = server.ResolveDependency<IPrototypeManager>().Index<MassFabricatorBlueprintPrototype>("HZBlueprintWall");
            Assert.That(fab.CreatePlan(user, (gun, entities.GetComponent<MassFabricatorComponent>(gun)),
                wall, "WallSolid", lowerTarget, Angle.Zero), Is.Not.Null);
        });

        await server.WaitRunTicks(8);
        await server.WaitAssertion(() =>
        {
            foreach (var uid in floor)
                Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(uid).Unsupported, Is.True);
            var grid = entities.GetComponent<MapGridComponent>(lower);
            pillar = entities.SpawnAttachedTo("WallSolid", maps.GridTileToLocal(lower, grid, cell));
        });

        await server.WaitRunTicks(8);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(plan).Unsupported, Is.False);
            Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(plan).PrintBlocked, Is.False);
            Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(floor[6]).Unsupported, Is.False);
            Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(floor[6]).PrintBlocked, Is.True);
            Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(floor[7]).Unsupported, Is.True);
            entities.DeleteEntity(pillar);
        });

        await server.WaitRunTicks(8);
        await server.WaitAssertion(() =>
            Assert.That(entities.GetComponent<MassFabricatorHologramComponent>(plan).Unsupported, Is.True));
        await pair.CleanReturnAsync();
    }
}
