using Content.Shared._Mono.Item.Components;
using Content.Shared._NF.Item;
using Content.Shared.Popups;

namespace Content.Shared._Mono.Item;

public sealed partial class DestroyItemOnPickupSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestroyItemOnPickupComponent, PickedUpEvent>(OnPickup);
    }

    private void OnPickup(Entity<DestroyItemOnPickupComponent> ent, ref PickedUpEvent args)
    {
        _popup.PopupPredicted(Loc.GetString(ent.Comp.Popup), ent, args.User);
        PredictedQueueDel(ent);
    }
}