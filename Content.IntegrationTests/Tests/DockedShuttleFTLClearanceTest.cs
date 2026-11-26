using System.Collections.Generic;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Mono.Ships;
using Content.Shared.Maps;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class DockedShuttleFTLClearanceTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestShuttleDockDummy
  components:
  - type: Transform
    anchored: true
  - type: Docking
";

    private static Entity<MapGridComponent> CreateGrid(
        IEntityManager entMan,
        IMapManager mapMan,
        SharedMapSystem mapSys,
        ITileDefinitionManager tileDefMan,
        MapId mapId,
        Vector2 position,
        int width,
        int height,
        bool isShuttle = true)
    {
        var grid = mapMan.CreateGridEntity(mapId);
        entMan.System<SharedTransformSystem>().SetWorldPosition(grid.Owner, position);

        var tile = new Tile(tileDefMan["Plating"].TileId);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                mapSys.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), tile);
            }
        }

        if (isShuttle)
            entMan.EnsureComponent<ShuttleComponent>(grid.Owner);

        return grid;
    }

    private static Entity<DockingComponent> SpawnDock(
        IEntityManager entMan,
        EntityUid gridUid,
        Vector2 localPos,
        Angle localRot)
    {
        var dockUid = entMan.SpawnEntity("TestShuttleDockDummy", new EntityCoordinates(gridUid, localPos));
        entMan.System<SharedTransformSystem>().SetLocalRotation(dockUid, localRot);
        return (dockUid, entMan.GetComponent<DockingComponent>(dockUid));
    }

    [Test]
    public async Task FTLFree_ChecksDockedShuttlesAndRespectsFTLLock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapMan = server.MapMan;
        var tileDefMan = server.ResolveDependency<ITileDefinitionManager>();
        var mapSys = server.System<SharedMapSystem>();
        var dockSys = server.System<DockingSystem>();
        var shuttleSys = server.System<ShuttleSystem>();
        var consoleSys = server.System<ShuttleConsoleSystem>();

        var testMap = await pair.CreateTestMap();

        EntityUid mapUid = default;
        Entity<MapGridComponent> shuttleA = default;
        Entity<MapGridComponent> shuttleB = default;

        await server.WaitAssertion(() =>
        {
            entMan.DeleteEntity(testMap.Grid.Owner);
            mapUid = testMap.MapUid;

            shuttleA = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, Vector2.Zero, 4, 4);
            var drive = entMan.EnsureComponent<FTLDriveComponent>(shuttleA.Owner);
            drive.Range = 1000f;

            shuttleB = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, new Vector2(4f, 0f), 80, 4);

            var dockA = SpawnDock(entMan, shuttleA.Owner, new Vector2(3.5f, 1.5f), new Angle(MathF.PI / 2f));
            var dockB = SpawnDock(entMan, shuttleB.Owner, new Vector2(0.5f, 1.5f), new Angle(-MathF.PI / 2f));
            dockSys.Dock(dockA, dockB);

            // Obstacle positioned clear of shuttleA's arrival buffer at (200, 0),
            // but well inside shuttleB's arrival buffer when angle == 0.
            CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, new Vector2(280f, 0f), 4, 4);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var targetCoords = new EntityCoordinates(mapUid, new Vector2(200f, 0f));

            Assert.That(shuttleSys.FTLFree(shuttleA.Owner, targetCoords, Angle.Zero, null), Is.False,
                "FTLFree should reject target when an FTL-locked docked shuttle's arrival buffer hits another grid.");

            Assert.That(shuttleSys.FTLFree(shuttleA.Owner, targetCoords, new Angle(MathF.PI), null), Is.True,
                "FTLFree should accept target when rotating 180 degrees swings the docked shuttle clear of the obstacle.");

            consoleSys.SetFTLLock(shuttleB.Owner, false);
            Assert.That(shuttleSys.FTLFree(shuttleA.Owner, targetCoords, Angle.Zero, null), Is.True,
                "FTLFree should ignore a docked shuttle whose FTL lock is disabled.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TryFTLProximity_AvoidsObstacleOverlapForDockedGroup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapMan = server.MapMan;
        var tileDefMan = server.ResolveDependency<ITileDefinitionManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var dockSys = server.System<DockingSystem>();
        var shuttleSys = server.System<ShuttleSystem>();

        var testMap = await pair.CreateTestMap();

        Entity<MapGridComponent> shuttleA = default;
        Entity<MapGridComponent> shuttleB = default;
        Entity<MapGridComponent> obstacle = default;

        await server.WaitAssertion(() =>
        {
            entMan.DeleteEntity(testMap.Grid.Owner);

            shuttleA = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, Vector2.Zero, 4, 4);
            shuttleB = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, new Vector2(4f, 0f), 80, 4);

            var dockA = SpawnDock(entMan, shuttleA.Owner, new Vector2(3.5f, 1.5f), new Angle(MathF.PI / 2f));
            var dockB = SpawnDock(entMan, shuttleB.Owner, new Vector2(0.5f, 1.5f), new Angle(-MathF.PI / 2f));
            dockSys.Dock(dockA, dockB);

            obstacle = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, new Vector2(400f, 0f), 10, 10, isShuttle: false);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var (oldMainPos, oldMainRot) = xformSys.GetWorldPositionRotation(shuttleA.Owner);
            var (oldDockedPos, oldDockedRot) = xformSys.GetWorldPositionRotation(shuttleB.Owner);
            var relPos = (-oldMainRot).RotateVec(oldDockedPos - oldMainPos);
            var relRot = oldDockedRot - oldMainRot;

            Assert.That(shuttleSys.TryFTLProximity(shuttleA.Owner, obstacle.Owner), Is.True);

            var (newMainPos, newMainRot) = xformSys.GetWorldPositionRotation(shuttleA.Owner);
            xformSys.SetWorldRotationNoLerp(shuttleB.Owner, newMainRot + relRot);
            xformSys.SetWorldPosition(shuttleB.Owner, newMainPos + newMainRot.RotateVec(relPos));

            var obstacleBox = xformSys.GetWorldMatrix(obstacle.Owner).TransformBox(obstacle.Comp.LocalAABB);
            var boxA = xformSys.GetWorldMatrix(shuttleA.Owner).TransformBox(shuttleA.Comp.LocalAABB);
            var boxB = xformSys.GetWorldMatrix(shuttleB.Owner).TransformBox(shuttleB.Comp.LocalAABB);

            Assert.That(boxA.Intersects(obstacleBox), Is.False, "Lead shuttle intersected obstacle after TryFTLProximity.");
            Assert.That(boxB.Intersects(obstacleBox), Is.False, "Docked shuttle intersected obstacle after TryFTLProximity.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GetDockingConfig_RejectsDockWhenDockedShuttleCollides()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapMan = server.MapMan;
        var tileDefMan = server.ResolveDependency<ITileDefinitionManager>();
        var mapSys = server.System<SharedMapSystem>();
        var dockSys = server.System<DockingSystem>();
        var consoleSys = server.System<ShuttleConsoleSystem>();

        var testMap = await pair.CreateTestMap();

        Entity<MapGridComponent> station = default;
        Entity<MapGridComponent> shuttleA = default;
        Entity<MapGridComponent> shuttleB = default;

        await server.WaitAssertion(() =>
        {
            entMan.DeleteEntity(testMap.Grid.Owner);

            // Station occupies [0..20, 0..10] with a North-facing dock at (5.5, 9.5).
            station = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, Vector2.Zero, 20, 10, isShuttle: false);
            SpawnDock(entMan, station.Owner, new Vector2(5.5f, 9.5f), new Angle(MathF.PI));

            // ShuttleA (4x4) docks at (4, 10)..(8, 14) with no station overlap on its own.
            shuttleA = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, new Vector2(0f, 30f), 4, 4);
            SpawnDock(entMan, shuttleA.Owner, new Vector2(1.5f, 0.5f), Angle.Zero);
            var aEastDock = SpawnDock(entMan, shuttleA.Owner, new Vector2(3.5f, 1.5f), new Angle(MathF.PI / 2f));

            // ShuttleB (6x8) is docked to ShuttleA's East side and protrudes 4m South past ShuttleA,
            // so docking ShuttleA onto the station would place ShuttleB inside [8..14, 6..14] overlapping the station.
            shuttleB = CreateGrid(entMan, mapMan, mapSys, tileDefMan, testMap.MapId, new Vector2(4f, 26f), 6, 8);
            var bWestDock = SpawnDock(entMan, shuttleB.Owner, new Vector2(0.5f, 5.5f), new Angle(-MathF.PI / 2f));
            dockSys.Dock(aEastDock, bWestDock);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var config = dockSys.GetDockingConfig(shuttleA.Owner, station.Owner);
            Assert.That(config, Is.Null,
                "GetDockingConfig should reject docking positions where an FTL-locked docked shuttle would collide with the target grid.");

            consoleSys.SetFTLLock(shuttleB.Owner, false);
            var unlockedConfig = dockSys.GetDockingConfig(shuttleA.Owner, station.Owner);
            Assert.That(unlockedConfig, Is.Not.Null,
                "GetDockingConfig should allow docking once the colliding docked shuttle's FTL lock is disabled.");
        });

        await pair.CleanReturnAsync();
    }
}
