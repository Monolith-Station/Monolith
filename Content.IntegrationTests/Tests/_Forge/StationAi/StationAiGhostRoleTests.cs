using System.Linq;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Players;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Forge.StationAi;

[TestFixture]
public sealed class StationAiGhostRoleTests
{
    [Test]
    public async Task GhostCommandReopensTakenCoreRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var playerManager = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var console = client.ResolveDependency<IConsoleHost>();
        var mindSystem = entityManager.System<SharedMindSystem>();
        var ghostRoleSystem = entityManager.System<GhostRoleSystem>();
        var session = playerManager.Sessions.Single();
        var originalMind = session.ContentData()!.Mind!.Value;
        server.CfgMan.SetCVar(CCVars.GhostQuickLottery, true);

        EntityUid originalMob = default;
        await server.WaitPost(() =>
        {
            originalMob = entityManager.SpawnEntity(null, map.GridCoords);
            mindSystem.TransferTo(originalMind, originalMob, true);
        });
        await pair.RunTicksSync(10);

        console.ExecuteCommand("ghost");
        await pair.RunTicksSync(10);
        Assert.That(entityManager.HasComponent<GhostComponent>(session.AttachedEntity), Is.True);

        EntityUid brain = default;
        await server.WaitPost(() =>
        {
            brain = entityManager.SpawnEntity("StationAiBrain", map.GridCoords);
            var role = entityManager.GetComponent<GhostRoleComponent>(brain);
            ghostRoleSystem.Request(session, role.Identifier);
        });
        await pair.RunTicksSync(60);

        var brainRole = entityManager.GetComponent<GhostRoleComponent>(brain);
        Assert.Multiple(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(brain));
            Assert.That(brainRole.Taken, Is.True);
            Assert.That(ghostRoleSystem.GhostRoles.Select(role => role.Owner), Does.Not.Contain(brain));
        });

        console.ExecuteCommand("ghost");
        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(entityManager.HasComponent<GhostComponent>(session.AttachedEntity), Is.True);
            Assert.That(brainRole.Taken, Is.False);
            Assert.That(ghostRoleSystem.GhostRoles.Select(role => role.Owner), Does.Contain(brain));
        });

        await server.WaitPost(() =>
        {
            ghostRoleSystem.Request(session, brainRole.Identifier);
            Assert.DoesNotThrow(() => ghostRoleSystem.GetGhostRolesInfo(session));
            ghostRoleSystem.LeaveRaffle(session, brainRole.Identifier);
        });
        await pair.RunTicksSync(2);
        server.CfgMan.SetCVar(CCVars.GhostQuickLottery, false);

        await pair.CleanReturnAsync();
    }
}
