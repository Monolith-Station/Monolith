using Content.Server.Stack;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mono.Spawning;

/// <summary>
/// This system handles spawning both stacked entities by consolidating them and non-stacked entities.
/// </summary>
public sealed class SpawnCountSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CountSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    public void OnMapInit(Entity<CountSpawnerComponent> ent, ref MapInitEvent args)
    {
        var count = _random.Next(ent.Comp.MinimumCount, ent.Comp.MaximumCount);

        SpawnCount(ent.Comp.Prototype, Transform(ent).Coordinates, count);
        if (ent.Comp.DespawnAfterSpawn)
            QueueDel(ent);
    }

    public void SpawnCount(EntProtoId prototype, EntityCoordinates coordinates, int count)
    {
        if (!_proto.TryIndex<EntityPrototype>(prototype, out var entProto))
            return;

        var bound = 1;
        var stackCount = count;

        if (entProto.TryGetComponent<StackComponent>(out var stack))
        {
            stackCount = stack.Count * count;
            var stackPrototype = _proto.Index<StackPrototype>(stack.StackTypeId);
            bound = stackPrototype.MaxCount ?? Int32.MaxValue;
        }

        for (var i = 0; i < stackCount; i += bound)
        {
            // spawn the remainder, potentially over the stack limit, this will be clamped in SetCount()
            SpawnEntity(prototype, coordinates, stackCount - i);
        }
    }


    private void SpawnEntity(string? prototype, EntityCoordinates coordinates, int stackCount)
    {
        var ent = Spawn(prototype, coordinates);

        if (TryComp<StackComponent>(ent, out var stack))
            _stack.SetCount(ent, stackCount, stack);
    }
}
