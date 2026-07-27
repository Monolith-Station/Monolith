using Content.Shared._Goobstation.Clothing.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.Timing; /// Forge-Change

namespace Content.Shared._Goobstation.Clothing.Systems;

/// <summary>
///     System used for sealable clothing (like modsuits)
/// </summary>
public abstract partial class SharedSealableClothingSystem : EntitySystem
{
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private ActionContainerSystem _actionContainerSystem = default!;
    [Dependency] private ComponentTogglerSystem _componentTogglerSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedPowerCellSystem _powerCellSystem = default!;
    [Dependency] private ToggleableClothingSystem _toggleableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SealableClothingComponent, ClothingPartSealCompleteEvent>(OnPartSealingComplete);

        SubscribeLocalEvent<SealableClothingControlComponent, ClothingControlSealCompleteEvent>(OnControlSealingComplete);
        SubscribeLocalEvent<SealableClothingControlComponent, ClothingGotEquippedEvent>(OnControlEquip);
        SubscribeLocalEvent<SealableClothingControlComponent, ClothingGotUnequippedEvent>(OnControlUnequip);
        SubscribeLocalEvent<SealableClothingControlComponent, ComponentRemove>(OnControlRemove);
        SubscribeLocalEvent<SealableClothingControlComponent, GetItemActionsEvent>(OnControlGetItemActions);
        SubscribeLocalEvent<SealableClothingControlComponent, GetVerbsEvent<Verb>>(OnEquipmentVerb);
        SubscribeLocalEvent<SealableClothingControlComponent, MapInitEvent>(OnControlMapInit);
        SubscribeLocalEvent<SealableClothingControlComponent, SealClothingDoAfterEvent>(OnSealClothingDoAfter);
        SubscribeLocalEvent<SealableClothingControlComponent, SealClothingEvent>(OnControlSealEvent);
        SubscribeLocalEvent<SealableClothingControlComponent, StartSealingProcessDoAfterEvent>(OnStartSealingDoAfter); /// Forge-Change
        SubscribeLocalEvent<SealableClothingControlComponent, ToggleClothingAttemptEvent>(OnToggleClothingAttempt);
    }

    #region Events

    /// <summary>
    /// Toggles components on part when suit complete sealing process
    /// </summary>
    /// <param name="part"></param>
    /// <param name="args"></param>
    private void OnPartSealingComplete(Entity<SealableClothingComponent> part, ref ClothingPartSealCompleteEvent args)
    {
        _componentTogglerSystem.ToggleComponent(part, args.IsSealed);
    }

    /// <summary>
    ///     Toggles components on control when suit complete sealing process
    /// </summary>
    private void OnControlSealingComplete(Entity<SealableClothingControlComponent> control, ref ClothingControlSealCompleteEvent args)
    {
        _componentTogglerSystem.ToggleComponent(control, args.IsSealed);
    }

    /// <summary>
    /// Add/Remove wearer on clothing equip/unequip
    /// </summary>
    private void OnControlEquip(Entity<SealableClothingControlComponent> control, ref ClothingGotEquippedEvent args)
    {
        control.Comp.WearerEntity = args.Wearer;
        Dirty(control);
    }

    private void OnControlUnequip(Entity<SealableClothingControlComponent> control, ref ClothingGotUnequippedEvent args)
    {
        var comp = control.Comp;
        comp.WearerEntity = null;

        // Forge-Change-start: dropped control units must not stay sealed
        if (comp.IsCurrentlySealed || comp.IsInProcess)
        {
            comp.IsCurrentlySealed = false;
            comp.IsInProcess = false;
            comp.ProcessQueue.Clear();
            _appearanceSystem.SetData(control, SealableClothingVisuals.Sealed, false);
        }
        // Forge-Change-end

        Dirty(control);
    }

    /// <summary>
    /// Removes seal action on component remove
    /// </summary>
    private void OnControlRemove(Entity<SealableClothingControlComponent> control, ref ComponentRemove args)
    {
        var comp = control.Comp;

        _actionsSystem.RemoveAction(comp.SealActionEntity);
    }

    /// <summary>
    /// Ensures seal action to wearer when it equip the seal control
    /// </summary>
    private void OnControlGetItemActions(Entity<SealableClothingControlComponent> control, ref GetItemActionsEvent args)
    {
        var (uid, comp) = control;

        if (comp.SealActionEntity == null || args.SlotFlags != comp.RequiredControlSlot)
            return;

        args.AddAction(comp.SealActionEntity.Value);
    }

    /// <summary>
    /// Adds unsealing verbs to sealing control allowing other users to unseal/seal clothing via stripping
    /// </summary>
    private void OnEquipmentVerb(Entity<SealableClothingControlComponent> control, ref GetVerbsEvent<Verb> args)
    {
        var (uid, comp) = control;
        var user = args.User;

        if (!args.CanComplexInteract)
            return;

        // Since sealing control in wearer's container system just won't show verb on args.CanAccess
        if (!_interactionSystem.InRangeUnobstructed(user, uid))
            return;

        if (comp.WearerEntity == null) /// Forge-Change
            return;

        var verbIcon = comp.IsCurrentlySealed ?
            new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png")) :
            new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/lock.svg.192dpi.png"));

        var verb = new Verb()
        {
            Icon = verbIcon,
            Priority = 5,
            Text = Loc.GetString(comp.VerbText),
            // Act = () => TryStartSealToggleProcess(control, user) /// Forge-Change-Del
        };
        /// Forge-Change-Start
        if (args.User == comp.WearerEntity)
        {
            verb.Act = () => TryStartSealToggleProcess(control);
        }
        else
        {
            verb.Act = () => StartSealDoAfter(user, control, comp.WearerEntity.Value);
        }
        /// Forge-Change-End

        args.Verbs.Add(verb);
    }
    /// Forge-Change-Start
    /// Take from Goobstation codebase

        private void StartSealDoAfter(EntityUid user, Entity<SealableClothingControlComponent> control, EntityUid wearer)
    {
        _popupSystem.PopupClient("You start the suits' sealing process", wearer, user);
        var args = new DoAfterArgs(EntityManager, user, control.Comp.NonWearerSealingTime, new StartSealingProcessDoAfterEvent(), control, wearer, control)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 2,
        };

        if (!_doAfterSystem.TryStartDoAfter(args))
        {
            return;
        }

        var popup = Loc.GetString("strippable-component-alert-owner-interact", ("user", Identity.Entity(user, EntityManager)), ("item", control));
        _popupSystem.PopupEntity(popup, wearer, wearer, PopupType.Large);

    }
    /// Forge-Change-End

    /// <summary>
    /// Ensure actionEntity on map init
    /// </summary>
    private void OnControlMapInit(Entity<SealableClothingControlComponent> control, ref MapInitEvent args)
    {
        var (uid, comp) = control;
        _actionContainerSystem.EnsureAction(uid, ref comp.SealActionEntity, comp.SealAction);
    }

    /// Forge-Change-Start
    /// Take from Goobstation codebase
    private void OnStartSealingDoAfter(Entity<SealableClothingControlComponent> control, ref StartSealingProcessDoAfterEvent args)
    {
        if (args.Cancelled)
            return;
        var user = args.User;
        // unless you have another way to do doafters inside doafters then yeah
        Timer.Spawn(0, () => TryStartSealToggleProcess(control, user));
    }
    /// Forge-Change-End

    /// <summary>
    /// Trying to start sealing on action. It'll notify wearer if process already started
    /// </summary>
    private void OnControlSealEvent(Entity<SealableClothingControlComponent> control, ref SealClothingEvent args)
    {
        var (uid, comp) = control;

        if (!_actionBlockerSystem.CanInteract(args.Performer, null))
            return;

        if (comp.IsInProcess)
        {
            if (comp.IsCurrentlySealed)
            {
                _popupSystem.PopupClient(Loc.GetString(comp.SealedInProcessToggleFailPopup), uid, args.Performer);
                _audioSystem.PlayPredicted(comp.FailSound, uid, args.Performer);
            }
            else
            {
                _popupSystem.PopupClient(Loc.GetString(comp.UnsealedInProcessToggleFailPopup), uid, args.Performer);
                _audioSystem.PlayPredicted(comp.FailSound, uid, args.Performer);
            }

            return;
        }

        TryStartSealToggleProcess(control, args.Performer);
    }

    /// <summary>
    /// Toggle seal on one part and starts same process on next part
    /// </summary>
    private void OnSealClothingDoAfter(Entity<SealableClothingControlComponent> control, ref SealClothingDoAfterEvent args)
    {
        var (uid, comp) = control;

        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        var part = args.Target;

        if (!TryComp<SealableClothingComponent>(part, out var sealableComponet))
            return;

        sealableComponet.IsSealed = !comp.IsCurrentlySealed;

        Dirty(part.Value, sealableComponet);

        if (sealableComponet.IsSealed)
            _audioSystem.PlayPvs(sealableComponet.SealUpSound, uid);
        else
            _audioSystem.PlayPvs(sealableComponet.SealUpSound, uid);

        _appearanceSystem.SetData(part.Value, SealableClothingVisuals.Sealed, sealableComponet.IsSealed);

        var ev = new ClothingPartSealCompleteEvent(sealableComponet.IsSealed);
        RaiseLocalEvent(part.Value, ref ev);

        NextSealProcess(control);
    }

    /// <summary>
    /// Prevents clothing from toggling if it's sealed or in sealing process
    /// </summary>
    private void OnToggleClothingAttempt(Entity<SealableClothingControlComponent> control, ref ToggleClothingAttemptEvent args)
    {
        var (uid, comp) = control;

        // Popup if currently sealing
        if (comp.IsInProcess)
        {
            _popupSystem.PopupClient(Loc.GetString(comp.UnsealedInProcessToggleFailPopup), uid, args.User);
            _audioSystem.PlayPredicted(comp.FailSound, uid, args.User);
            args.Cancel();

            return;
        }

        // Popup if sealed, but not in process
        if (comp.IsCurrentlySealed)
        {
            _popupSystem.PopupClient(Loc.GetString(comp.CurrentlySealedToggleFailPopup), uid, args.User);
            _audioSystem.PlayPredicted(comp.FailSound, uid, args.User);
            args.Cancel();

            return;
        }

        return;
    }
    #endregion

    /// <summary>
    ///     Tries to start sealing process
    /// </summary>
    /// <param name="control"></param>
    /// <returns></returns>
    public bool TryStartSealToggleProcess(Entity<SealableClothingControlComponent> control, EntityUid? user = null)
    {
        var (uid, comp) = control;

        // Prevent sealing/unsealing if modsuit don't have wearer or already started process
        if (comp.WearerEntity == null || comp.IsInProcess)
            return false;

        if (user == null)
            user = comp.WearerEntity;

        var ev = new ClothingSealAttemptEvent(user.Value);
        RaiseLocalEvent(control, ev);

        if (ev.Cancelled)
            return false;

        // For sealing: All parts required to be toggled
        // For unsealing: Only check currently available parts (more lenient for limb loss)
        if (!comp.IsCurrentlySealed)
        {
            // Sealing requires all parts to be toggled
            if (_toggleableSystem.GetAttachedToggleStatus(uid) != ToggleableClothingAttachedStatus.AllToggled)
            {
                _popupSystem.PopupClient(Loc.GetString(comp.ToggleFailedPopup), uid, user);
                _audioSystem.PlayPredicted(comp.FailSound, uid, user);
                return false;
            }
        }
        else
        {
            // Unsealing: Allow if we have any parts to unseal (more lenient)
            var toggleStatus = _toggleableSystem.GetAttachedToggleStatus(uid);
            if (toggleStatus == ToggleableClothingAttachedStatus.NoneToggled)
            {
                _popupSystem.PopupClient(Loc.GetString(comp.ToggleFailedPopup), uid, user);
                _audioSystem.PlayPredicted(comp.FailSound, uid, user);
                return false;
            }
        }

        // Trying to get all clothing to seal/unseal
        var sealeableList = _toggleableSystem.GetAttachedClothingsList(uid);
        if (sealeableList == null)
            return false;

        foreach (var sealeable in sealeableList)
        {
            if (!HasComp<SealableClothingComponent>(sealeable))
            {
                _popupSystem.PopupEntity(Loc.GetString(comp.ToggleFailedPopup), uid);
                _audioSystem.PlayPredicted(comp.FailSound, uid, user);

                comp.ProcessQueue.Clear();
                Dirty(control);

                return false;
            }

            comp.ProcessQueue.Enqueue(EntityManager.GetNetEntity(sealeable));
        }

        comp.IsInProcess = true;
        Dirty(control);

        NextSealProcess(control);

        return true;
    }

    /// <summary>
    ///     Recursively seals/unseals all parts of sealable clothing
    /// </summary>
    /// <param name="control"></param>
    private void NextSealProcess(Entity<SealableClothingControlComponent> control)
    {
        var (uid, comp) = control;

        // Finish sealing process
        if (comp.ProcessQueue.Count == 0)
        {
            comp.IsInProcess = false;
            comp.IsCurrentlySealed = !comp.IsCurrentlySealed;

            if (comp.IsCurrentlySealed)
                _audioSystem.PlayEntity(comp.SealCompleteSound, comp.WearerEntity!.Value, uid);
            else
                _audioSystem.PlayEntity(comp.UnsealCompleteSound, comp.WearerEntity!.Value, uid);

            var ev = new ClothingControlSealCompleteEvent(comp.IsCurrentlySealed);
            RaiseLocalEvent(control, ref ev);

            _appearanceSystem.SetData(uid, SealableClothingVisuals.Sealed, comp.IsCurrentlySealed);

            Dirty(control);
            return;
        }

        var processingPart = EntityManager.GetEntity(comp.ProcessQueue.Dequeue());
        Dirty(control);

        // If the entity no longer exists (e.g., due to limb loss), skip it and continue
        if (!EntityManager.EntityExists(processingPart))
        {
            NextSealProcess(control);
            return;
        }

        if (!TryComp<SealableClothingComponent>(processingPart, out var sealableComponent) || !comp.IsInProcess)
        {
            // For unsealing, if a part is missing, just skip it instead of failing
            if (comp.IsCurrentlySealed)
            {
                NextSealProcess(control);
                return;
            }

            _popupSystem.PopupClient(Loc.GetString(comp.ToggleFailedPopup), uid, comp.WearerEntity);
            _audioSystem.PlayPredicted(comp.FailSound, uid, comp.WearerEntity);

            NextSealProcess(control);
            return;
        }

        // If part is sealed when control trying to seal - it should just skip this part
        if (sealableComponent.IsSealed != comp.IsCurrentlySealed)
        {
            NextSealProcess(control);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, sealableComponent.SealingTime, new SealClothingDoAfterEvent(), uid, target: processingPart, showTo: comp.WearerEntity)
        {
            NeedHand = false,
            RequireCanInteract = false,
        };

        // Checking for client here to skip first process popup spam that happens. Predicted popups don't work here because doafter starts on sealable control, not on player.
        if (!_doAfterSystem.TryStartDoAfter(doAfterArgs) || _netManager.IsClient)
            return;

        if (comp.IsCurrentlySealed)

            _popupSystem.PopupEntity(Loc.GetString(sealableComponent.SealDownPopup,
                ("partName", Identity.Name(processingPart, EntityManager))),
                uid, comp.WearerEntity!.Value);
        else
            _popupSystem.PopupEntity(Loc.GetString(sealableComponent.SealUpPopup,
                ("partName", Identity.Name(processingPart, EntityManager))),
                uid, comp.WearerEntity!.Value);
    }
}

[Serializable, NetSerializable]
public sealed partial class SealClothingDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class StartSealingProcessDoAfterEvent : SimpleDoAfterEvent
{
}

public sealed partial class SealClothingEvent : InstantActionEvent
{
}

/// <summary>
///     Raises on control when clothing finishes it's sealing or unsealing process
/// </summary>
[ByRefEvent]
public readonly record struct ClothingControlSealCompleteEvent(bool IsSealed)
{
    public readonly bool IsSealed = IsSealed;
}

/// <summary>
///     Raises on part when clothing finishes it's sealing or unsealing process
/// </summary>
[ByRefEvent]
public readonly record struct ClothingPartSealCompleteEvent(bool IsSealed)
{
    public readonly bool IsSealed = IsSealed;
}

public sealed partial class ClothingSealAttemptEvent : CancellableEntityEventArgs
{
    public EntityUid User;

    public ClothingSealAttemptEvent(EntityUid user)
    {
        User = user;
    }
}
