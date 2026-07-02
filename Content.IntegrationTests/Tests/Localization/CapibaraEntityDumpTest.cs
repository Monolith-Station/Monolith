using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Localization;

/// <summary>
/// Capibara ESP tooling — NOT a real test. Run explicitly to dump every non-abstract
/// entity prototype's resolved English name/description (via the engine, so inheritance
/// is handled) to _Capibara/entities/entity-source.json for the es-ES entity translation
/// pass. Run with:
///   dotnet test Content.IntegrationTests --filter FullyQualifiedName~CapibaraEntityDumpTest -c Release
/// </summary>
[TestFixture]
public sealed class CapibaraEntityDumpTest
{
    private sealed record EntityLoc(string id, string name, string desc);

    [Test]
    public async Task DumpEntityNamesAndDescriptions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        var rows = new List<EntityLoc>();
        await server.WaitPost(() =>
        {
            foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID))
            {
                if (proto.Abstract)
                    continue;

                var data = loc.GetEntityData(proto.ID);
                var name = data.Name ?? string.Empty;
                var desc = data.Desc ?? string.Empty;

                // Skip entities with no display name at all.
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(desc))
                    continue;

                rows.Add(new EntityLoc(proto.ID, name, desc));
            }
        });

        // Repo root = current working directory during integration tests.
        var outDir = Path.Combine(Directory.GetCurrentDirectory(), "_Capibara", "entities");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "entity-source.json");
        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(outPath, json);

        TestContext.Out.WriteLine($"[CapibaraEntityDump] wrote {rows.Count} entities to {outPath}");
        Assert.That(rows.Count, Is.GreaterThan(1000), "Expected thousands of named entities.");

        await pair.CleanReturnAsync();
    }
}
