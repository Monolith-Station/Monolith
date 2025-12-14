using System.Linq;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Access.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Hands.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Server._NF.Shipyard.Components;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Shipyard;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Interaction;
using Content.Shared.PDA;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// Server-side implementation of the shuttle console lock system.
/// </summary>
public sealed class ShuttleConsoleLockSystem : SharedShuttleConsoleLockSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _consoleSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleConsoleLockComponent, ComponentInit>(OnShuttleConsoleLockInit);
        SubscribeLocalEvent<ShuttleConsoleLockComponent, GetVerbsEvent<AlternativeVerb>>(AddUnlockVerb);
        SubscribeLocalEvent<ShuttleConsoleLockComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<PdaComponent, AfterInteractEvent>(OnPdaAfterInteract);
        SubscribeLocalEvent<ShuttleConsoleLockComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<ShuttleDeedComponent, ComponentInit>(OnShuttleDeedInit);
    }

    /// <summary>
    /// Initializes the lock component, ensuring all grids with shuttle consoles have grid lock components
    /// </summary>
    private void OnShuttleConsoleLockInit(EntityUid uid, ShuttleConsoleLockComponent component, ComponentInit args)
    {
        // Get the grid this console is on
        var transform = Transform(uid);
        if (transform.GridUid == null)
        {
            // Not on a grid, use individual console logic
            if (string.IsNullOrEmpty(component.ShuttleId))
                component.Locked = false;
            return;
        }

        var gridUid = transform.GridUid.Value;

        // Ensure this grid has a grid lock component
        if (!TryComp<ShipGridLockComponent>(gridUid, out var gridLock))
        {
            // Create grid lock component for this grid
            gridLock = AddComp<ShipGridLockComponent>(gridUid);
            gridLock.Locked = !string.IsNullOrEmpty(component.ShuttleId); // Lock if it has a shuttle ID
            gridLock.ShuttleId = component.ShuttleId;
            Dirty(gridUid, gridLock);
            Log.Debug("Created grid lock component for grid {0} with shuttle ID {1}", gridUid, component.ShuttleId);
        }

        // If the grid has a deed, ensure the shuttle ID is set correctly
        if (TryComp<ShuttleDeedComponent>(gridUid, out var deed))
        {
            // Console is on a ship grid - ensure it has the correct shuttle ID
            if (string.IsNullOrEmpty(component.ShuttleId) && deed.ShuttleUid != null)
            {
                component.ShuttleId = deed.ShuttleUid.Value.ToString();
                gridLock.ShuttleId = component.ShuttleId;
                Dirty(gridUid, gridLock);
                Log.Debug("Assigned shuttle ID {0} to console {1} on ship grid {2}", component.ShuttleId, uid, gridUid);
            }
        }
        else
        {
            // Not on a ship grid with deed, but still use grid-based locking
            if (string.IsNullOrEmpty(component.ShuttleId))
            {
                gridLock.Locked = false;
                Dirty(gridUid, gridLock);
            }
        }
    }

    /// <summary>
    /// Handles when a ShuttleDeedComponent is added to ensure grid lock component exists
    /// </summary>
    private void OnShuttleDeedInit(EntityUid uid, ShuttleDeedComponent component, ComponentInit args)
    {
        // Only create grid lock component for grids (not ID cards)
        if (!HasComp<MapGridComponent>(uid))
            return;

        EnsureGridLockComponent(uid, component.ShuttleUid?.ToString());
    }

    /// <summary>
    /// Adds verbs for console interaction (unlock/lock, guest access, reset guest access)
    /// </summary>
    private void AddUnlockVerb(EntityUid uid,
        ShuttleConsoleLockComponent component,
        GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Check if player has an ID card or voucher in hand
        var idCards = FindAccessibleIdCards(args.User);
        var vouchers = FindAccessibleVouchers(args.User);
        var isCyborg = TryComp<BorgChassisComponent>(args.User, out _);

        // Show unlock/lock verb only for users with ID cards or vouchers
        var hasIdOrVoucher = idCards.Count > 0 || vouchers.Count > 0;

        // Get the effective lock state for use throughout this method
        var effectiveLocked = GetEffectiveLockState(uid, component);

        if (hasIdOrVoucher)
        {
            AlternativeVerb verb = new()
            {
                Act = () => TryToggleLock(uid, args.User, component),
                Text = effectiveLocked
                    ? Loc.GetString("shuttle-console-verb-unlock")
                    : Loc.GetString("shuttle-console-verb-lock"),
                Icon = effectiveLocked
                    ? new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png"))
                    : new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/lock.svg.192dpi.png")),
                Priority = 10,
            };

            args.Verbs.Add(verb);
        }

        // Add reset guest access verb for deed holders when console is unlocked
        if (!effectiveLocked && hasIdOrVoucher && HasDeedAccess(uid, args.User, component))
        {
            AlternativeVerb resetVerb = new()
            {
                Act = () => TryResetGuestAccess(uid, args.User, component),
                Text = Loc.GetString("shuttle-console-verb-reset-guest-access"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Priority = 5,
            };

            args.Verbs.Add(resetVerb);
        }

        // Add guest access verb for users without deed access when console is unlocked
        // This includes cyborgs (who don't have ID cards) and users with ID cards that don't have the correct deed
        if (!effectiveLocked && (isCyborg || (hasIdOrVoucher && !HasDeedAccess(uid, args.User, component))))
        {
            AlternativeVerb guestVerb = new()
            {
                Act = () => TryGrantGuestAccess(uid, args.User, component),
                Text = Loc.GetString("shuttle-console-verb-guest-access"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/group.svg.192dpi.png")),
                Priority = 10,
            };

            args.Verbs.Add(guestVerb);
        }

        // Add ship access toggle verbs for deed holders when console is unlocked
        if (!effectiveLocked && hasIdOrVoucher && HasDeedAccess(uid, args.User, component))
        {
            var shipAccessEnabled = IsShipAccessEnabled(uid);

            AlternativeVerb shipAccessVerb = new()
            {
                Act = () => TryToggleShipAccess(uid, args.User, component, !shipAccessEnabled),
                Text = shipAccessEnabled
                    ? Loc.GetString("shuttle-console-verb-unlock-ship")
                    : Loc.GetString("shuttle-console-verb-lock-ship"),
                Icon = shipAccessEnabled
                    ? new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png"))
                    : new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/lock.svg.192dpi.png")),
                Priority = 8,
            };

            args.Verbs.Add(shipAccessVerb);
        }
    }

    /// <summary>
    /// Handles PDA interaction with shuttle consoles for lock/unlock functionality
    /// </summary>
    private void OnPdaAfterInteract(EntityUid uid, PdaComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        // Check if the target has a ShuttleConsoleLockComponent
        if (!TryComp<ShuttleConsoleLockComponent>(args.Target, out var lockComponent))
            return;

        // Check if the PDA has an ID card
        if (component.ContainedId == null)
        {
            Popup.PopupEntity(Loc.GetString("shuttle-console-no-id-card"), uid, args.User);
            return;
        }

        // Try to toggle the lock using the PDA's ID card
        TryToggleLock(args.Target.Value, args.User, lockComponent);
        args.Handled = true;
    }

    /// <summary>
    /// Handles ID card, PDA, and voucher interaction with shuttle consoles for lock/unlock functionality
    /// </summary>
    private void OnAfterInteractUsing(EntityUid uid, ShuttleConsoleLockComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // Check if the used item is an ID card, PDA with ID card, or voucher
        if (!TryComp<IdCardComponent>(args.Used, out _) &&
            (!TryComp<PdaComponent>(args.Used, out var pda) || pda.ContainedId == null) &&
            !TryComp<ShipyardVoucherComponent>(args.Used, out _))
            return;

        // Try to toggle the lock using the ID card or voucher
        TryToggleLock(uid, args.User, component);
        args.Handled = true;
    }



    /// <summary>
    /// Tries to toggle the lock state of the console using an ID card or voucher from the player
    /// </summary>
    private void TryToggleLock(EntityUid uid, EntityUid user, ShuttleConsoleLockComponent component)
    {
        var effectiveLocked = GetEffectiveLockState(uid, component);

        // If locked, try to unlock
        if (effectiveLocked)
        {

            // Normal unlock procedure for non-emergency locks
            // Try each ID card the user has
            var idCards = FindAccessibleIdCards(user);
            var unlocked = idCards.Any(idCard => TryUnlock(uid, idCard, component, user: user));

            // If ID cards didn't work, try each voucher
            if (!unlocked && FindAccessibleVouchers(user).Any(voucher => TryUnlockWithVoucher(uid, voucher, component)))
                unlocked = true;

            // If we reach here and nothing worked, show error
            if (!unlocked)
                Popup.PopupEntity(Loc.GetString("shuttle-console-wrong-deed"), uid, user);
        }
        // If unlocked, try to lock it again (only works if it's your ship) or grant guest access
        else
        {
            // Don't allow locking if there's no shuttle ID
            if (string.IsNullOrEmpty(component.ShuttleId))
            {
                Popup.PopupEntity(Loc.GetString("shuttle-console-no-ship-id"), uid, user);
                return;
            }

            // Try ID cards first
            var idCards = FindAccessibleIdCards(user);
            var validLock = idCards.Any(idCard => TryLock(uid, idCard, component));

            // If ID cards didn't work, try vouchers
            if (!validLock && FindAccessibleVouchers(user).Any(voucher => TryLockWithVoucher(uid, voucher, component)))
                validLock = true;

            // If user doesn't have deed access but console is unlocked, grant guest access
            if (!validLock)
            {
                TryGrantGuestAccess(uid, user, component);
            }
        }
    }

    /// <summary>
    /// Tries to lock the console with the given ID card
    /// </summary>
    private bool TryLock(EntityUid console,
        EntityUid idCard,
        ShuttleConsoleLockComponent? lockComp = null,
        IdCardComponent? idComp = null)
    {
        if (!Resolve(console, ref lockComp) || !Resolve(idCard, ref idComp))
            return false;

        // If the console is already locked, do nothing
        if (GetEffectiveLockState(console, lockComp))
            return false;

        // Get grid information for grid-based locking
        var transform = Transform(console);
        var gridUid = transform.GridUid;
        ShipGridLockComponent? gridLock = null;

        if (gridUid != null && TryComp<ShipGridLockComponent>(gridUid.Value, out gridLock))
        {
            // Use grid lock state for ships with deeds
        }

        // Can't lock a console without a shuttle ID
        var shuttleId = gridLock?.ShuttleId ?? lockComp.ShuttleId;
        if (string.IsNullOrEmpty(shuttleId))
            return false;

        // Only allow locking if this ID card has a matching deed
        var hasMatchingDeed = false;

        // Find all deed components (either on the ID or pointing to this ID)
        var query = EntityQueryEnumerator<ShuttleDeedComponent>();
        while (query.MoveNext(out var entity, out var deed))
        {
            // Check if this is for the same shuttle
            if ((entity != idCard && deed.DeedHolder != idCard)
                || deed.ShuttleUid == null
                || deed.ShuttleUid.Value.ToString() != shuttleId)
                continue;

            hasMatchingDeed = true;
            break;
        }

        if (!hasMatchingDeed)
            return false;

        // Success! Lock the console
        if (gridLock != null)
        {
            // Lock at grid level
            SetGridLockState(gridUid!.Value, true, shuttleId);
        }
        else
        {
            // Lock individual console
            lockComp.Locked = true;
        }

        _audio.PlayPvs(idComp.SwipeSound, console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-locked-success"), console);

        // Remove any pilots
        if (!TryComp<ShuttleConsoleComponent>(console, out var shuttleComp))
            return true;

        // Clone the list to avoid modification during enumeration
        var pilots = shuttleComp.SubscribedPilots.ToList();
        foreach (var pilot in pilots)
            _consoleSystem.RemovePilot(pilot);

        return true;
    }

    /// <summary>
    /// Tries to unlock the console with the given voucher
    /// </summary>
    private bool TryUnlockWithVoucher(EntityUid console,
        EntityUid voucher,
        ShuttleConsoleLockComponent? lockComp = null)
    {
        if (!Resolve(console, ref lockComp))
            return false;

        // If the console is already unlocked, do nothing
        if (!GetEffectiveLockState(console, lockComp))
            return false;

        // Get grid information for grid-based locking
        var transform = Transform(console);
        var gridUid = transform.GridUid;
        ShipGridLockComponent? gridLock = null;

        if (gridUid != null && TryComp<ShipGridLockComponent>(gridUid.Value, out gridLock))
        {
            // Use grid lock state for ships with deeds
        }

        // If there's no shuttle ID, there's nothing to unlock against
        var shuttleId = gridLock?.ShuttleId ?? lockComp.ShuttleId;
        if (string.IsNullOrEmpty(shuttleId))
        {
            if (gridLock != null)
            {
                SetGridLockState(gridUid!.Value, false);
            }
            else
            {
                lockComp.Locked = false;
            }
            return true;
        }

        // Get the voucher's UID
        var voucherUid = voucher.ToString();
        var deedFound = false;
        var query = EntityQueryEnumerator<ShuttleDeedComponent>();

        while (query.MoveNext(out var entity, out var deed))
        {
            var deedShuttleId = deed.ShuttleUid?.ToString();

            // Check if this deed was purchased with this specific voucher and matches the shuttle ID
            if (!deed.PurchasedWithVoucher ||
                deed.ShuttleUid == null ||
                shuttleId == null ||
                deedShuttleId != shuttleId ||
                deed.PurchaseVoucherUid != voucherUid)
                continue;
            deedFound = true;
            Log.Debug("Found matching voucher-purchased deed for shuttle console {0}", console);
            break;
        }

        if (!deedFound)
        {
            Log.Debug("No matching voucher-purchased deed found for shuttle console {0}", console);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg"), voucher);
            return false;
        }

        // Success! Unlock the console
        Log.Debug("Successfully unlocked shuttle console {0} with voucher {1}", console, voucher);

        if (gridLock != null)
        {
            // Unlock at grid level
            SetGridLockState(gridUid!.Value, false);
        }
        else
        {
            // Unlock individual console
            lockComp.Locked = false;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg"), console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-unlocked"), console);
        return true;
    }

    /// <summary>
    /// Tries to lock the console with the given voucher
    /// </summary>
    private bool TryLockWithVoucher(EntityUid console, EntityUid voucher, ShuttleConsoleLockComponent? lockComp = null)
    {
        if (!Resolve(console, ref lockComp))
            return false;

        // If the console is already locked, do nothing
        if (GetEffectiveLockState(console, lockComp))
            return false;

        // Get grid information for grid-based locking
        var transform = Transform(console);
        var gridUid = transform.GridUid;
        ShipGridLockComponent? gridLock = null;

        if (gridUid != null && TryComp<ShipGridLockComponent>(gridUid.Value, out gridLock))
        {
            // Use grid lock state for ships with deeds
        }

        // If there's no shuttle ID, there's nothing to lock against
        var shuttleId = gridLock?.ShuttleId ?? lockComp.ShuttleId;
        if (string.IsNullOrEmpty(shuttleId))
            return false;

        // Get the voucher's UID
        var voucherUid = voucher.ToString();
        var deedFound = false;
        var query = EntityQueryEnumerator<ShuttleDeedComponent>();

        while (query.MoveNext(out var entity, out var deed))
        {
            var deedShuttleId = deed.ShuttleUid?.ToString();

            // Check if this deed was purchased with this specific voucher and matches the shuttle ID
            if (!deed.PurchasedWithVoucher ||
                deed.ShuttleUid == null ||
                shuttleId == null ||
                deedShuttleId != shuttleId ||
                deed.PurchaseVoucherUid != voucherUid)
                continue;

            deedFound = true;
            Log.Debug("Found matching voucher-purchased deed for shuttle console {0}", console);
            break;
        }

        if (!deedFound)
        {
            Log.Debug("No matching voucher-purchased deed found for shuttle console {0}", console);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg"), voucher);
            return false;
        }

        // Success! Lock the console
        Log.Debug("Successfully locked shuttle console {0} with voucher {1}", console, voucher);

        if (gridLock != null)
        {
            // Lock at grid level
            SetGridLockState(gridUid!.Value, true);
        }
        else
        {
            // Lock individual console
            lockComp.Locked = true;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg"), console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-locked-success"), console);

        // Remove any pilots
        if (!TryComp<ShuttleConsoleComponent>(console, out var shuttleComp))
            return true;
        // Clone the list to avoid modification during enumeration
        var pilots = shuttleComp.SubscribedPilots.ToList();
        foreach (var pilot in pilots)
            _consoleSystem.RemovePilot(pilot);

        return true;
    }

    /// <summary>
    /// Prevents using the console UI if it's locked
    /// </summary>
    private void OnUIOpenAttempt(EntityUid uid,
        ShuttleConsoleLockComponent component,
        ActivatableUIOpenAttemptEvent args)
    {
        if (GetEffectiveLockState(uid, component))
        {
            Popup.PopupEntity(Loc.GetString("shuttle-console-locked"), uid, args.User);
            args.Cancel();
        }
    }

    /// <summary>
    /// Finds all ID cards accessible to a user (in hands or worn)
    /// </summary>
    private List<EntityUid> FindAccessibleIdCards(EntityUid user)
    {
        var results = new List<EntityUid>();

        // Check hands
        var hands = _handsSystem.EnumerateHands(user);
        foreach (var hand in hands)
        {
            if (hand.HeldEntity == null)
                continue;

            if (TryComp<IdCardComponent>(hand.HeldEntity, out _))
                results.Add(hand.HeldEntity.Value);

            if (TryComp<PdaComponent>(hand.HeldEntity, out var pdaComponent) && pdaComponent.ContainedId is not null)
                results.Add(pdaComponent.ContainedId.Value);
        }

        return results;
    }

    /// <summary>
    /// Finds all vouchers accessible to a user (in hands)
    /// </summary>
    private List<EntityUid> FindAccessibleVouchers(EntityUid user)
    {
        var results = new List<EntityUid>();

        // Check hands
        var hands = _handsSystem.EnumerateHands(user);
        foreach (var hand in hands)
        {
            if (hand.HeldEntity == null)
                continue;

            if (TryComp<ShipyardVoucherComponent>(hand.HeldEntity, out _))
                results.Add(hand.HeldEntity.Value);
        }

        return results;
    }

    /// <summary>
    /// Server-side implementation of TryUnlock
    /// </summary>
    public override bool TryUnlock(EntityUid console,
        EntityUid idCard,
        ShuttleConsoleLockComponent? lockComp = null,
        IdCardComponent? idComp = null,
        EntityUid? user = null)
    {
        if (!Resolve(console, ref lockComp) || !Resolve(idCard, ref idComp))
            return false;

        // If the console is already unlocked, do nothing
        if (!GetEffectiveLockState(console, lockComp))
            return false;

        // Get grid information for grid-based locking
        var transform = Transform(console);
        var gridUid = transform.GridUid;
        ShipGridLockComponent? gridLock = null;

        if (gridUid != null && TryComp<ShipGridLockComponent>(gridUid.Value, out gridLock))
        {
            // Use grid lock state for ships with deeds
        }



        // If there's no shuttle ID, there's nothing to unlock against
        var shuttleId = gridLock?.ShuttleId ?? lockComp.ShuttleId;
        if (string.IsNullOrEmpty(shuttleId))
        {
            if (gridLock != null)
            {
                SetGridLockState(gridUid!.Value, false);
            }
            else
            {
                lockComp.Locked = false;
            }
            return true;
        }

        // Get the ID's uid string to compare with the lock
        Log.Debug("Attempting to unlock shuttle console {0} with card {1}. Lock ID: {2}",
            console,
            idCard,
            lockComp.ShuttleId);

        // First approach: Check if this ID card IS the deed holder
        var deedFound = false;
        var deeds = new List<(EntityUid Entity, ShuttleDeedComponent Component)>();

        // Find all deed components (either on the ID or pointing to this ID)
        var query = EntityQueryEnumerator<ShuttleDeedComponent>();
        while (query.MoveNext(out var entity, out var deed))
        {
            // Case 1: The deed is on this ID card
            if (entity == idCard)
            {
                deeds.Add((entity, deed));
                Log.Debug("Found deed on ID card {0}", idCard);
            }
            // Case 2: The deed points to this ID card as its holder
            else if (deed.DeedHolder == idCard)
            {
                deeds.Add((entity, deed));
                Log.Debug("Found deed with DeedHolder {0} matching ID {1}", deed.DeedHolder, idCard);
            }
        }

        // No deeds on this ID card
        if (deeds.Count == 0)
        {
            Log.Debug("No deeds found for ID card {0}", idCard);
            _audio.PlayPvs(idComp.ErrorSound, idCard);
            return false;
        }

        // Check if any deed matches the shuttle ID
        foreach (var (_, deed) in deeds)
        {
            var deedShuttleId = deed.ShuttleUid?.ToString();

            Log.Debug("Checking deed shuttle ID {0} against lock shuttle ID {1}", deedShuttleId, shuttleId);

            if (deed.ShuttleUid == null ||
                shuttleId == null ||
                deedShuttleId != shuttleId)
                continue;

            deedFound = true;
            Log.Debug("Found matching deed for shuttle console {0}", console);
            break;
        }

        if (!deedFound)
        {
            Log.Debug("No matching deed found for shuttle console {0}", console);
            _audio.PlayPvs(idComp.ErrorSound, idCard);
            return false;
        }

        // Success! Unlock the console
        Log.Debug("Successfully unlocked shuttle console {0}", console);

        if (gridLock != null)
        {
            // Unlock at grid level
            SetGridLockState(gridUid!.Value, false);
        }
        else
        {
            // Unlock individual console
            lockComp.Locked = false;
        }

        _audio.PlayPvs(idComp.SwipeSound, console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-unlocked"), console);
        return true;
    }

    /// <summary>
    /// Sets the shuttle ID for a console lock component.
    /// This should be called when a ship is purchased.
    /// </summary>
    public void SetShuttleId(EntityUid console, string shuttleId, ShuttleConsoleLockComponent? lockComp = null)
    {
        if (!Resolve(console, ref lockComp))
            return;

        lockComp.ShuttleId = shuttleId;

        // Get grid information for grid-based locking
        var transform = Transform(console);
        var gridUid = transform.GridUid;
        var shouldLock = !string.IsNullOrEmpty(shuttleId);

        if (gridUid != null && TryComp<ShipGridLockComponent>(gridUid.Value, out var gridLock))
        {
            // Update grid lock state
            gridLock.ShuttleId = shuttleId;
            gridLock.Locked = shouldLock;
            Dirty(gridUid.Value, gridLock);
        }
        else
        {
            // Fallback to individual console lock
            lockComp.Locked = shouldLock;
        }

        // Remove any pilots when locking the console
        if (!shouldLock || !TryComp<ShuttleConsoleComponent>(console, out var shuttleComp))
            return;

        // Clone the list to avoid modification during enumeration
        var pilots = shuttleComp.SubscribedPilots.ToList();
        foreach (var pilot in pilots)
            _consoleSystem.RemovePilot(pilot);

    }

    /// <summary>
    /// Grants guest access to a ship when someone without deed access swipes their ID on an unlocked shuttle console.
    /// </summary>
    public void TryGrantGuestAccess(EntityUid console, EntityUid user, ShuttleConsoleLockComponent lockComp)
    {
        // Log.Debug("TryGrantGuestAccess: User {0} attempting to get guest access via console {1}", user, console);

        // Get the grid the console is on
        var consoleTransform = Transform(console);
        if (consoleTransform.GridUid == null)
        {
            // Log.Debug("TryGrantGuestAccess: Console {0} not on a grid", console);
            return;
        }

        var gridUid = consoleTransform.GridUid.Value;
        // Log.Debug("TryGrantGuestAccess: Console {0} is on grid {1}", console, gridUid);

        // Check if this is a ship with a deed
        if (!TryComp<ShuttleDeedComponent>(gridUid, out var shipDeed))
        {
            // Log.Debug("TryGrantGuestAccess: Grid {0} has no ShuttleDeedComponent", gridUid);
            return;
        }

        // Log.Debug("TryGrantGuestAccess: Grid {0} has ShuttleDeedComponent for shuttle {1}", gridUid, shipDeed.ShuttleUid);

        // Check if the user is a cyborg
        if (TryComp<BorgChassisComponent>(user, out _))
        {
            // Handle cyborg guest access
            TryGrantCyborgGuestAccess(console, user, gridUid);
            return;
        }

        // Find all accessible ID cards for the user
        var idCards = FindAccessibleIdCards(user);
        // Log.Debug("TryGrantGuestAccess: User {0} has {1} accessible ID cards: {2}", user, idCards.Count, string.Join(", ", idCards));

        if (idCards.Count == 0)
        {
            // Log.Debug("TryGrantGuestAccess: User {0} has no accessible ID cards", user);
            Popup.PopupEntity(Loc.GetString("shuttle-console-no-id-card"), console, user);
            return;
        }

        // Check if any ID card already has deed access (shouldn't happen, but safety check)
        foreach (var cardUid in idCards)
        {
            if (TryComp<ShuttleDeedComponent>(cardUid, out var cardDeed) &&
                cardDeed.ShuttleUid == shipDeed.ShuttleUid)
            {
                // Log.Debug("TryGrantGuestAccess: User {0} already has deed access via card {1}", user, cardUid);
                return; // User already has deed access
            }
        }

        // Ensure the ship has a guest access component
        var guestAccess = EnsureComp<ShipGuestAccessComponent>(gridUid);
        // Log.Debug("TryGrantGuestAccess: Ensured ShipGuestAccessComponent on grid {0}", gridUid);

        // Check if any of the user's ID cards already have guest access
        var alreadyHasAccess = idCards.Any(cardUid => guestAccess.GuestIdCards.Contains(cardUid));
        if (alreadyHasAccess)
        {
            // Log.Debug("TryGrantGuestAccess: User {0} already has guest access", user);
            Popup.PopupEntity(Loc.GetString("shuttle-console-guest-access-already-granted"), console, user);
            return;
        }

        // Grant guest access to all of the user's ID cards
        foreach (var cardUid in idCards)
        {
            guestAccess.GuestIdCards.Add(cardUid);
            // Log.Debug("TryGrantGuestAccess: Granted guest access to ID card {0}", cardUid);
        }
        Dirty(gridUid, guestAccess);

        // Log.Debug("TryGrantGuestAccess: Successfully granted guest access to user {0} on grid {1}", user, gridUid);

        // Play sound and show popup
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg"), console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-guest-access-granted"), console, user);
    }

    /// <summary>
    /// Grants guest access to a cyborg.
    /// </summary>
    private void TryGrantCyborgGuestAccess(EntityUid console, EntityUid cyborg, EntityUid gridUid)
    {
        // Log.Debug("TryGrantCyborgGuestAccess: Cyborg {0} attempting to get guest access via console {1} on grid {2}", cyborg, console, gridUid);

        // Ensure the ship has a guest access component
        var guestAccess = EnsureComp<ShipGuestAccessComponent>(gridUid);
        // Log.Debug("TryGrantCyborgGuestAccess: Ensured ShipGuestAccessComponent on grid {0}", gridUid);

        // Check if the cyborg already has guest access
        if (guestAccess.GuestCyborgs.Contains(cyborg))
        {
            // Log.Debug("TryGrantCyborgGuestAccess: Cyborg {0} already has guest access", cyborg);
            Popup.PopupEntity(Loc.GetString("shuttle-console-guest-access-already-granted"), console, cyborg);
            return;
        }

        // Grant guest access to the cyborg
        guestAccess.GuestCyborgs.Add(cyborg);
        Dirty(gridUid, guestAccess);

        // Log.Debug("TryGrantCyborgGuestAccess: Successfully granted guest access to cyborg {0} on grid {1}", cyborg, gridUid);

        // Play sound and show popup
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg"), console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-guest-access-granted"), console, cyborg);
    }

    /// <summary>
    /// Checks if a user has deed access to the ship this console is on.
    /// </summary>
    private bool HasDeedAccess(EntityUid console, EntityUid user, ShuttleConsoleLockComponent lockComp)
    {
        // Get the grid the console is on
        var consoleTransform = Transform(console);
        if (consoleTransform.GridUid == null)
            return false;

        var gridUid = consoleTransform.GridUid.Value;

        // Check if this is a ship with a deed
        if (!TryComp<ShuttleDeedComponent>(gridUid, out var shipDeed))
            return false;

        // Find all accessible ID cards for the user
        var idCards = FindAccessibleIdCards(user);

        // Check if any ID card has deed access for this ship
        foreach (var cardUid in idCards)
        {
            if (TryComp<ShuttleDeedComponent>(cardUid, out var cardDeed) &&
                cardDeed.ShuttleUid == shipDeed.ShuttleUid)
            {
                return true; // User has deed access
            }
        }

        return false;
    }

    /// <summary>
    /// Resets guest access for the ship.
    /// </summary>
    private void TryResetGuestAccess(EntityUid console, EntityUid user, ShuttleConsoleLockComponent lockComp)
    {
        // Get the grid the console is on
        var consoleTransform = Transform(console);
        if (consoleTransform.GridUid == null)
            return;

        var gridUid = consoleTransform.GridUid.Value;

        // Check if this is a ship with a deed
        if (!TryComp<ShuttleDeedComponent>(gridUid, out var shipDeed))
            return;

        // Verify user has deed access
        if (!HasDeedAccess(console, user, lockComp))
        {
            Popup.PopupEntity(Loc.GetString("shuttle-console-reset-guest-access-denied"), console, user);
            return;
        }

        // Check if there's a guest access component
        if (!TryComp<ShipGuestAccessComponent>(gridUid, out var guestAccess))
        {
            Popup.PopupEntity(Loc.GetString("shuttle-console-no-guest-access"), console, user);
            return;
        }

        // Check if there are any guest cards or cyborgs to reset
        var totalGuests = guestAccess.GuestIdCards.Count + guestAccess.GuestCyborgs.Count;
        if (totalGuests == 0)
        {
            Popup.PopupEntity(Loc.GetString("shuttle-console-no-guest-access"), console, user);
            return;
        }

        // Reset guest access
        guestAccess.GuestIdCards.Clear();
        guestAccess.GuestCyborgs.Clear();
        Dirty(gridUid, guestAccess);

        // Play sound and show popup
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg"), console);
        Popup.PopupEntity(Loc.GetString("shuttle-console-guest-access-reset", ("count", totalGuests)), console, user);
    }

    /// <summary>
    /// Checks if ship access is currently enabled on the grid
    /// </summary>
    private bool IsShipAccessEnabled(EntityUid consoleUid)
    {
        // Get the grid the console is on
        var consoleTransform = Transform(consoleUid);
        if (consoleTransform.GridUid == null)
            return false;

        var gridUid = consoleTransform.GridUid.Value;

        // Get all entities on the grid using transform children
        var gridTransform = Transform(gridUid);
        var childEnumerator = gridTransform.ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            if (TryComp<ShipAccessReaderComponent>(child, out var accessReader) && accessReader.Enabled)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Toggles ship access for all ship access readers on the grid
    /// </summary>
    private void ToggleShipAccess(EntityUid consoleUid, bool enabled)
    {
        // Get the grid the console is on
        var consoleTransform = Transform(consoleUid);
        if (consoleTransform.GridUid == null)
            return;

        var gridUid = consoleTransform.GridUid.Value;

        // Get all entities on the grid using transform children
        var gridTransform = Transform(gridUid);
        var childEnumerator = gridTransform.ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            if (TryComp<ShipAccessReaderComponent>(child, out var accessReader))
            {
                accessReader.Enabled = enabled;
                Dirty(child, accessReader);
            }
        }
    }

    /// <summary>
    /// Attempts to toggle ship access for the grid
    /// </summary>
    private void TryToggleShipAccess(EntityUid consoleUid, EntityUid user, ShuttleConsoleLockComponent component, bool enable)
    {
        // Only allow deed holders to toggle ship access
        if (!HasDeedAccess(consoleUid, user, component))
        {
            Popup.PopupEntity(Loc.GetString("shuttle-console-access-denied"), consoleUid, user);
            return;
        }

        // Toggle ship access
        ToggleShipAccess(consoleUid, enable);

        // Play sound and show popup
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg"), consoleUid);
        var message = enable
            ? Loc.GetString("shuttle-console-ship-access-enabled")
            : Loc.GetString("shuttle-console-ship-access-disabled");
        Popup.PopupEntity(message, consoleUid, user);
    }
}
