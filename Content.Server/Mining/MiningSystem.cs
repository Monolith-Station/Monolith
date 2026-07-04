using Content.Server._Mono.Spawning;
using Content.Server.Gatherable;
using Content.Shared.Destructible;
using Content.Shared.Mining;
using Content.Shared.Mining.Components;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Mining;

/// <summary>
/// This handles creating ores when the entity is destroyed.
/// </summary>
public sealed partial class MiningSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpawnCountSystem _spawnCount = default!; // Mono edit - ore consolidation

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreVeinComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OreVeinComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<OreVeinComponent, GatheredEvent>(OnGather); // Mono edit
    }

    private void OnDestruction(EntityUid uid, OreVeinComponent component, DestructionEventArgs args)
    {
        Mine(uid, component); // mono
    }

    /// <summary>
    /// Monolith - Mining now also uses gathered event.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnGather(EntityUid uid, OreVeinComponent component, GatheredEvent args)
    {
        Mine(uid, component, args.Gatherer, args.TeleportLootToGatherer);
    }

    /// <summary>
    /// Monolith - Moved out method
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="gatherer"></param>
    /// <param name="spawnOnGatherer"></param>
    public void Mine(EntityUid uid, OreVeinComponent component, EntityUid? gatherer = null, bool spawnOnGatherer = false)
    {
        if (component.CurrentOre == null)
            return;

        if (component.PreventSpawning)
            return;

        var proto = _proto.Index(component.CurrentOre);

        if (proto.OreEntity == null)
            return;

        var coords = gatherer != null && spawnOnGatherer
            ? Transform(gatherer.Value).Coordinates
            : Transform(uid).Coordinates;

        var yield = _random.Next(proto.MinOreYield, proto.MaxOreYield+1);
        _spawnCount.SpawnCount(proto.OreEntity.Value, coords.Offset(_random.NextVector2(0.2f)), yield);
        component.PreventSpawning = true;
    }

    private void OnMapInit(EntityUid uid, OreVeinComponent component, MapInitEvent args)
    {
        if (component.CurrentOre != null || component.OreRarityPrototypeId == null || !_random.Prob(component.OreChance))
            return;

        component.CurrentOre = _proto.Index<WeightedRandomOrePrototype>(component.OreRarityPrototypeId).Pick(_random);
    }
}
