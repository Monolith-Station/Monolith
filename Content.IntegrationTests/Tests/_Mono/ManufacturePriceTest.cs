using Content.Server.Cargo.Systems;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;

namespace Content.IntegrationTests.Tests._Mono;

[TestFixture]
public sealed class ManufacturePriceTest
{
    private const float _pricingThreshold = 4f;

    [Test]
    public async Task CheckAllShuttleGrids()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entSysManager = server.ResolveDependency<IEntitySystemManager>();
        var entManager = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var pricing = entSysManager.GetEntitySystem<PricingSystem>();

        var count = 0;
        await server.WaitPost(() =>
        {
            var matPrices = new Dictionary<ProtoId<MaterialPrototype>, float>();
            foreach (var mat in proto.EnumeratePrototypes<MaterialPrototype>())
            {
                matPrices[mat.ID] = (float)mat.Price;
            }

            foreach (var latheProto in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (!latheProto.TryGetComponent<LatheComponent>(out var lathe, factory))
                    continue;

                var allPacks = new List<ProtoId<LatheRecipePackPrototype>>();
                allPacks.AddRange(lathe.DynamicPacks);
                allPacks.AddRange(lathe.StaticPacks);
                foreach (var pack in allPacks)
                {
                    var packProto = proto.Index(pack);
                    foreach (var recipe in packProto.Recipes)
                    {
                        var recipeProto = proto.Index(recipe);

                        var price = 0f;
                        foreach (var (id, count) in recipeProto.Materials)
                            price += matPrices[id] * count;

                        var worth = 0f;
                        if (proto.TryIndex(recipeProto.Result, out var resultProto))
                            worth += (float)pricing.GetEstimatedPrice(resultProto) * (lathe.ProductValueModifier ?? 1f);
                        foreach (var (id, count) in recipeProto.MaterialResult)
                            worth += matPrices[id] * count;

                        if (worth == 0f || price == 0f)
                            continue;

                        var ratio = worth / price;
                        if (ratio > _pricingThreshold)
                        {
                            count++;
                            var reasons = new List<string>();
                            foreach (var (id, count) in recipeProto.Materials)
                                reasons.Add($"material {id}: priced at {matPrices[id]}, we need {count}, total ${matPrices[id] * count}");
                            Logger.Warning($"Overpriced recipe prototype {recipe} in pack {pack} on lathe {latheProto.ID}: material sale price {price}, product sale price (1x) {worth}, ratio {ratio}, manufacture cost breakdown: [{string.Join("], [", reasons)}]");
                        }
                    }
                }
            }
        });
        if (count > 0)
            Assert.Fail($"Found {count} overpriced items");

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }
}
