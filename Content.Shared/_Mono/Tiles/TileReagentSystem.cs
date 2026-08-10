using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;

namespace Content.Shared._Mono.Tiles;

/// <summary>
/// If you make meth water I swear to god.
/// </summary>
public sealed partial class TileReagentSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RefillableSolutionComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<RefillableSolutionComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null)
            return;

        if (!TryFill(ent, args.ClickLocation, args.User))
            return;

        args.Handled = true;
    }

    /// <summary>
    /// Fills the container from the tile at these coordinates, if that tile is made of anything.
    /// </summary>
    public bool TryFill(Entity<RefillableSolutionComponent> ent, EntityCoordinates coordinates, EntityUid user)
    {
        if (!_turf.TryGetTileRef(coordinates, out var tileRef))
            return false;

        if (_turf.GetContentTileDefinition(tileRef.Value).Reagent is not { } reagent)
            return false;

        if (!_solution.TryGetRefillableSolution((ent.Owner, ent.Comp, null), out var soln, out var solution))
            return false;

        var amount = solution.AvailableVolume;

        if (ent.Comp.MaxRefill is { } maxRefill)
            amount = FixedPoint2.Min(amount, maxRefill);

        if (amount <= 0)
        {
            _popup.PopupClient(Loc.GetString("tile-reagent-fill-full", ("container", ent.Owner)), ent.Owner, user);
            return true;
        }

        if (!_solution.TryAddReagent(soln.Value, reagent, amount, out var accepted) || accepted <= 0)
            return false;

        _popup.PopupClient(
            Loc.GetString("tile-reagent-fill", ("container", ent.Owner), ("amount", accepted)),
            ent.Owner,
            user);

        return true;
    }
}
