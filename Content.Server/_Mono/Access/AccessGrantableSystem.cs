using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Interaction;

namespace Content.Server._Mono.Access;

public sealed class AccessGrantableSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessGrantableComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    private void OnAfterInteractUsing(Entity<AccessGrantableComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Target != ent || !args.CanReach || !HasComp<IdCardComponent>(args.Used))
            return;

        if (!TryComp<AccessComponent>(ent, out var ourAccess) || !TryComp<AccessComponent>(args.Used, out var cardAccess))
            return;

        var beforeLength = ourAccess.Tags.Count;
        ourAccess.Tags.UnionWith(cardAccess.Tags);
        var addedLength = ourAccess.Tags.Count - beforeLength;

        if (addedLength == 0)
        {
            _popupSystem.PopupEntity(Loc.GetString("agent-id-no-new", ("card", args.Used)), ent);
            return;
        }

        Dirty(ent, ourAccess);

        if (addedLength == 1)
        {
            _popupSystem.PopupEntity(Loc.GetString("agent-id-new-1", ("card", args.Used)), ent);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("agent-id-new", ("number", addedLength), ("card", args.Used)), ent);
    }
}
