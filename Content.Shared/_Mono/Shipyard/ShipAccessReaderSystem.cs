using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Doors;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Lock;
using Content.Shared._NF.Whitelist.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Ghost;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;

namespace Content.Shared._Mono.Shipyard;

/// <summary>
/// System that handles ship deed-based access control.
/// </summary>
public sealed class ShipAccessReaderSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedIdCardSystem _idCardSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipAccessReaderComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt);
        SubscribeLocalEvent<ShipAccessReaderComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpened);
        SubscribeLocalEvent<ShipAccessReaderComponent, LockToggleAttemptEvent>(OnLockToggleAttempt);
    }

    private void OnStorageOpenAttempt(EntityUid uid, ShipAccessReaderComponent component, ref StorageOpenAttemptEvent args)
    {
        if (!component.Enabled)
            return;

        // If the locker is unlocked, allow anyone to open it
        if (TryComp<LockComponent>(uid, out var lockComp) && !lockComp.Locked)
            return;

        // If the locker is locked, require ship deed access
        if (!HasShipAccess(args.User, uid, component, args.Silent))
        {
            args.Cancelled = true;
        }
    }

    private void OnBeforeDoorOpened(EntityUid uid, ShipAccessReaderComponent component, ref BeforeDoorOpenedEvent args)
    {
        if (!component.Enabled)
            return;

        if (args.User == null)
            return;

        if (!HasShipAccess(args.User.Value, uid, component, false))
        {
            args.Cancel();
        }
    }

    private void OnLockToggleAttempt(EntityUid uid, ShipAccessReaderComponent component, ref LockToggleAttemptEvent args)
    {
        if (!component.Enabled)
            return;

        if (!HasShipAccess(args.User, uid, component, args.Silent))
        {
            args.Cancelled = true;
        }
    }

    /// <summary>
    /// Checks if a user has access to a ship entity by verifying they have the correct ship deed.
    /// </summary>
    /// <param name="user">The user trying to access the entity</param>
    /// <param name="target">The entity being accessed</param>
    /// <param name="component">The ship access reader component</param>
    /// <param name="silent">Whether to suppress popup messages</param>
    /// <returns>True if access is granted, false otherwise</returns>
    public bool HasShipAccess(EntityUid user, EntityUid target, ShipAccessReaderComponent component, bool silent = false)
    {
        // Allow admin ghosts to bypass ship access checks
        if (TryComp<GhostComponent>(user, out var ghost) && ghost.CanGhostInteract)
        {
            // Log.Debug("ShipAccess: Admin ghost {0} bypassing ship access check", user);
            return true; // Admin ghosts can access everything
        }

        // Allow AI cores to bypass ship access checks
        if (HasComp<StationAiHeldComponent>(user))
        {
            // Log.Debug("ShipAccess: AI core {0} bypassing ship access check", user);
            return true; // AI cores can access everything
        }

        // Get the grid the target entity is on
        var targetTransform = Transform(target);
        if (targetTransform.GridUid == null)
        {
            // Log.Debug("ShipAccess: Target {0} not on a grid, allowing access", target);
            return true; // Not on a grid, allow access
        }

        var gridUid = targetTransform.GridUid.Value;

        // Check if the grid has a ship deed (is a purchased ship)
        if (!TryComp<ShuttleDeedComponent>(gridUid, out var shipDeed))
        {
            // Log.Debug("ShipAccess: Grid {0} has no ShuttleDeedComponent, allowing normal access", gridUid);
            return true; // Not a ship with a deed, allow normal access
        }

        // Find all accessible ID cards for the user
        var accessibleCards = FindAccessibleIdCards(user);

        // Check for company-based access (USSP, Rogue, TSF) using ID card company
        if (TryComp<CompanyComponent>(gridUid, out var shipCompany))
        {
            // Check if ship has one of the special company designations
            if (shipCompany.CompanyName == "Rogue" || shipCompany.CompanyName == "TSF")
            {
                // Check each accessible ID card for matching company
                foreach (var cardUid in accessibleCards)
                {
                    if (TryComp<IdCardComponent>(cardUid, out var idCard) &&
                        idCard.CompanyName == shipCompany.CompanyName)
                    {
                        // Log.Debug("ShipAccess: User {0} has company access to {1} ship via ID card {2}", user, shipCompany.CompanyName, cardUid);
                        return true; // ID card company matches ship company
                    }
                }
            }
        }
        // Log.Debug("ShipAccess: User {0} has {1} accessible ID cards: {2}", user, accessibleCards.Count, string.Join(", ", accessibleCards));

        // Check if any of the user's ID cards have a deed for this specific ship
        foreach (var cardUid in accessibleCards)
        {
            if (TryComp<ShuttleDeedComponent>(cardUid, out var cardDeed))
            {
                // Log.Debug("ShipAccess: ID card {0} has deed for shuttle {1}, target ship is {2}", cardUid, cardDeed.ShuttleUid, shipDeed.ShuttleUid);
                // Check if this deed is for the same ship
                if (cardDeed.ShuttleUid == shipDeed.ShuttleUid)
                {
                    // Log.Debug("ShipAccess: User {0} has correct deed access via card {1}", user, cardUid);
                    return true; // User has the correct deed
                }
            }
        }

        // Find all accessible vouchers for the user
        var accessibleVouchers = FindAccessibleVouchers(user);
        // Log.Debug("ShipAccess: User {0} has {1} accessible vouchers: {2}", user, accessibleVouchers.Count, string.Join(", ", accessibleVouchers));

        // Check if any of the user's vouchers have a deed for this specific ship
        foreach (var voucherUid in accessibleVouchers)
        {
            if (TryComp<ShuttleDeedComponent>(voucherUid, out var voucherDeed))
            {
                // Log.Debug("ShipAccess: Voucher {0} has deed for shuttle {1}, target ship is {2}", voucherUid, voucherDeed.ShuttleUid, shipDeed.ShuttleUid);
                // Check if this deed is for the same ship
                if (voucherDeed.ShuttleUid == shipDeed.ShuttleUid)
                {
                    // Log.Debug("ShipAccess: User {0} has correct deed access via voucher {1}", user, voucherUid);
                    return true; // User has the correct deed
                }
            }
        }

        // Check if any of the user's ID cards have guest access to this ship
        if (TryComp<ShipGuestAccessComponent>(gridUid, out var guestAccess))
        {
            // Log.Debug("ShipAccess: Grid {0} has guest access component with {1} guest cards: {2}",
            //     gridUid, guestAccess.GuestIdCards.Count, string.Join(", ", guestAccess.GuestIdCards));

            // Check if the user is a cyborg with guest access
            if (TryComp<BorgChassisComponent>(user, out _) && guestAccess.GuestCyborgs.Contains(user))
            {
                // Log.Debug("ShipAccess: Cyborg {0} has guest access", user);
                return true; // Cyborg has guest access
            }

            foreach (var cardUid in accessibleCards)
            {
                if (guestAccess.GuestIdCards.Contains(cardUid))
                {
                    // Log.Debug("ShipAccess: User {0} has guest access via card {1}", user, cardUid);
                    return true; // User's ID card has guest access
                }
            }

            // Also check if any vouchers have guest access
            foreach (var voucherUid in accessibleVouchers)
            {
                if (guestAccess.GuestIdCards.Contains(voucherUid))
                {
                    // Log.Debug("ShipAccess: User {0} has guest access via voucher {1}", user, voucherUid);
                    return true; // User's voucher has guest access
                }
            }
        }
        // else
        // {
        //     Log.Debug("ShipAccess: Grid {0} has no ShipGuestAccessComponent", gridUid);
        // }

        // Log.Debug("ShipAccess: User {0} denied access to target {1} on grid {2}", user, target, gridUid);

        // Access denied - show popup if not silent
        if (!silent && component.ShowDeniedPopup)
        {
            _popup.PopupClient(Loc.GetString(component.DeniedMessage), target, user);
        }

        return false;
    }

    /// <summary>
    /// Finds all ID cards that the user can access (in hands, inventory, or inside PDAs).
    /// </summary>
    /// <param name="user">The user to check</param>
    /// <returns>Collection of accessible ID card entities</returns>
    private HashSet<EntityUid> FindAccessibleIdCards(EntityUid user)
    {
        var cards = new HashSet<EntityUid>();

        // Check items in hands for direct ID cards or PDAs with ID cards
        foreach (var item in _handsSystem.EnumerateHeld(user))
        {
            // Check if the item itself is an ID card (with or without deed)
            if (HasComp<IdCardComponent>(item))
                cards.Add(item);

            // Check if it's a PDA with an ID card
            if (_idCardSystem.TryGetIdCard(item, out var idCard))
                cards.Add(idCard.Owner);
        }

        // Check ID slot in inventory (could be direct ID or PDA)
        if (_inventorySystem.TryGetSlotEntity(user, "id", out var idUid))
        {
            // Check if the item itself is an ID card (with or without deed)
            if (HasComp<IdCardComponent>(idUid.Value))
                cards.Add(idUid.Value);

            // Check if it's a PDA with an ID card
            if (_idCardSystem.TryGetIdCard(idUid.Value, out var idCard))
                cards.Add(idCard.Owner);
        }

        return cards;
    }

    /// <summary>
    /// Finds all vouchers that the user can access (in hands or inventory).
    /// </summary>
    /// <param name="user">The user to check</param>
    /// <returns>Collection of accessible voucher entities</returns>
    private HashSet<EntityUid> FindAccessibleVouchers(EntityUid user)
    {
        var vouchers = new HashSet<EntityUid>();

        // Check items in hands for vouchers
        foreach (var item in _handsSystem.EnumerateHeld(user))
        {
            // Check if the item is a voucher
            if (HasComp<NFShipyardVoucherComponent>(item))
                vouchers.Add(item);
        }

        // Check ID slot in inventory for vouchers (in case someone puts a voucher in the ID slot)
        if (_inventorySystem.TryGetSlotEntity(user, "id", out var idUid))
        {
            // Check if the item is a voucher
            if (HasComp<NFShipyardVoucherComponent>(idUid.Value))
                vouchers.Add(idUid.Value);
        }

        return vouchers;
    }
}
