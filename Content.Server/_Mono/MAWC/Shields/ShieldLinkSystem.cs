using Content.Shared._Mono.MAWC.Shields;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._Mono.MAWC.Shields;

public sealed partial class ShieldLinkSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var receivers = EntityQueryEnumerator<ShieldLinkReceiverComponent>();
        while (receivers.MoveNext(out _, out var receiver))
        {
            receiver.LinkedSources.Clear();
        }

        var sources = EntityQueryEnumerator<ShieldLinkSourceComponent, ItemToggleComponent, TransformComponent>();
        while (sources.MoveNext(out var sourceUid, out var source, out var toggle, out var sourceXform))
        {
            source.LinkedShields.Clear();
            if (!toggle.Activated || source.MaxLinks <= 0 || source.MaxRange < 0f ||
                !TryComp<PersonalShieldComponent>(sourceUid, out var sourceShield) ||
                !_inventory.InSlotWithFlags(sourceUid, sourceShield.Shield.RequiredSlot))
            {
                Dirty(sourceUid, source);
                continue;
            }

            var nearby = EntityQueryEnumerator<ShieldLinkReceiverComponent, TransformComponent>();
            while (nearby.MoveNext(out var receiverUid, out var receiver, out var receiverXform))
            {
                if (source.LinkedShields.Count >= source.MaxLinks)
                    break;

                if (receiverUid == sourceUid || source.LinkId != receiver.LinkId ||
                    !TryComp<PersonalShieldComponent>(receiverUid, out var shield) ||
                    !_inventory.InSlotWithFlags(receiverUid, shield.Shield.RequiredSlot) ||
                    !_transform.InRange(sourceXform.Coordinates, receiverXform.Coordinates, source.MaxRange))
                {
                    continue;
                }

                source.LinkedShields.Add(receiverUid);
                receiver.LinkedSources.Add(sourceUid);
            }

            Dirty(sourceUid, source);
        }

        receivers = EntityQueryEnumerator<ShieldLinkReceiverComponent>();
        while (receivers.MoveNext(out var receiverUid, out var receiver))
        {
            Dirty(receiverUid, receiver);
            _toggle.TrySetActive(receiverUid, receiver.LinkedSources.Count > 0, predicted: false);
        }
    }
}
