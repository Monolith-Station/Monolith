using Content.Server.Power.Components;
using Content.Shared._Goobstation.ItemMiner;

namespace Content.Server._Goobstation.ItemMiner;

public sealed class PowerConsumerMinerSystem : EntitySystem
{
    private EntityQuery<PowerConsumerComponent> _consumerQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerConsumerMinerComponent, ItemMinerCheckEvent>(OnCheck);

        _consumerQuery = GetEntityQuery<PowerConsumerComponent>();
    }

    private void OnCheck(Entity<PowerConsumerMinerComponent> ent, ref ItemMinerCheckEvent args)
    {
        args.Cancelled |= !_consumerQuery.TryComp(ent, out var consumer) || consumer.ReceivedPower < consumer.DrawRate;
    }
}
