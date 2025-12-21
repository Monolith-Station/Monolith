using Content.Server.Popups;
using Content.Server.Worldgen.Components.Debris;
using Content.Shared.Verbs;

namespace Content.Server._Mono.GridClaimer;

public sealed class GridClaimerSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridClaimerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<GridClaimerComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<GridClaimerComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GridClaimerComponent, ComponentRemove>(OnRemoved);
    }

    private void OnGetVerbs(Entity<GridClaimerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var xform = Transform(ent);

        if (ent.Comp.RequireAnchored && !xform.Anchored)
            return;

        if (ent.Comp.ClaimingGrid != null)
        {
            AlternativeVerb verb = new()
            {
                Act = () => UnclaimGrid(ent, true),
                Text = Loc.GetString("grid-claimer-verb-unclaim"),
                Priority = 1
            };
            args.Verbs.Add(verb);
        }
        else if (xform.GridUid != null && IsClaimable(xform.GridUid))
        {
            AlternativeVerb verb = new()
            {
                Act = () => ClaimGrid(xform.GridUid.Value, ent, true),
                Text = Loc.GetString("grid-claimer-verb-claim"),
                Priority = 1
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnAnchorChanged(Entity<GridClaimerComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (ent.Comp.RequireAnchored && ent.Comp.ClaimingGrid != null && args.Anchored == false)
            UnclaimGrid(ent);
    }

    private void OnParentChanged(Entity<GridClaimerComponent> ent, ref EntParentChangedMessage args)
    {
        // unclaim if we move offgrid
        if (ent.Comp.ClaimingGrid != null)
            UnclaimGrid(ent);
    }

    private void OnRemoved(Entity<GridClaimerComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.ClaimingGrid != null)
            UnclaimGrid(ent);
    }

    public void ClaimGrid(EntityUid gridUid, Entity<GridClaimerComponent> with, bool popup = false)
    {
        with.Comp.ClaimingGrid = gridUid;
        var claimable = EnsureComp<ClaimableGridComponent>(gridUid);

        if (popup)
            _popup.PopupEntity(Loc.GetString(claimable.Claimed ? "grid-claimer-claim-already" : "grid-claimer-claim"), with);

        claimable.ClaimedBy.Add(with);
        if (!claimable.Claimed)
            with.Comp.WasDebris = RemComp<OwnedDebrisComponent>(gridUid);
    }

    public void UnclaimGrid(Entity<GridClaimerComponent> from, bool popup = false)
    {
        if (from.Comp.ClaimingGrid == null)
            return;
        var gridUid = from.Comp.ClaimingGrid.Value;

        from.Comp.ClaimingGrid = null;
        if (TryComp<ClaimableGridComponent>(gridUid, out var claimable))
        {
            claimable.ClaimedBy.Remove(from);
            if (!claimable.Claimed && from.Comp.WasDebris)
                EnsureComp<OwnedDebrisComponent>(gridUid);

            if (popup)
                _popup.PopupEntity(Loc.GetString(claimable.Claimed ? "grid-claimer-unclaim-fail" : "grid-claimer-unclaim"), from);
        }
    }

    public bool IsClaimable(EntityUid? gridUid)
    {
        return HasComp<ClaimableGridComponent>(gridUid);
    }
}
