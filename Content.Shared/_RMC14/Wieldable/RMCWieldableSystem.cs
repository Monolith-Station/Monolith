using Content.Shared._RMC14.Wieldable.Components;
using Content.Shared._RMC14.Wieldable.Events;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Wieldable;

public sealed class RMCWieldableSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly UseDelaySystem _useDelaySystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private const string WieldUseDelayId = "RMCWieldDelay";

    public override void Initialize()
    {

        SubscribeLocalEvent<WieldDelayComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<WieldDelayComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WieldDelayComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WieldDelayComponent, ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<WieldDelayComponent, ItemWieldedEvent>(OnItemWieldedWithDelay);
    }

    private void OnMapInit(Entity<WieldDelayComponent> wieldable, ref MapInitEvent args)
    {
        wieldable.Comp.ModifiedDelay = wieldable.Comp.BaseDelay;
        Dirty(wieldable);
    }

#region Wield delay
    private void OnGotEquippedHand(Entity<WieldDelayComponent> wieldable, ref GotEquippedHandEvent args)
    {
        _useDelaySystem.SetLength(wieldable.Owner, wieldable.Comp.ModifiedDelay, WieldUseDelayId);
        _useDelaySystem.TryResetDelay(wieldable.Owner, id: WieldUseDelayId);
    }

    private void OnUseInHand(Entity<WieldDelayComponent> wieldable, ref UseInHandEvent args)
    {
        if (!TryComp(wieldable.Owner, out UseDelayComponent? useDelayComponent) ||
            !_useDelaySystem.IsDelayed((wieldable.Owner, useDelayComponent), WieldUseDelayId))
        {
            return;
        }

        args.Handled = true;

        if (!_useDelaySystem.TryGetDelayInfo((wieldable.Owner, useDelayComponent), out var info, WieldUseDelayId))
        {
            return;
        }

        var time = $"{(info.EndTime - _timing.CurTime).TotalSeconds:F1}";

        _popupSystem.PopupClient(Loc.GetString("rmc-wield-use-delay", ("seconds", time), ("wieldable", wieldable.Owner)), args.User, args.User);
    }

    public void RefreshWieldDelay(Entity<WieldDelayComponent?> wieldable)
    {
        wieldable.Comp = EnsureComp<WieldDelayComponent>(wieldable);

        var ev = new GetWieldDelayEvent(wieldable.Comp.BaseDelay);
        RaiseLocalEvent(wieldable, ref ev);

        wieldable.Comp.ModifiedDelay = ev.Delay >= TimeSpan.Zero ? ev.Delay : TimeSpan.Zero;
        Dirty(wieldable);
    }

    private void OnItemWieldedWithDelay(Entity<WieldDelayComponent> wieldable, ref ItemWieldedEvent args)
    {
        // TODO RMC14 +0.5s if Dazed
        var delay = wieldable.Comp.ModifiedDelay;

        _useDelaySystem.SetLength(wieldable.Owner, delay, WieldUseDelayId);
        _useDelaySystem.TryResetDelay(wieldable.Owner, id: WieldUseDelayId);
    }

    public void OnShotAttempt(Entity<WieldDelayComponent> wieldable, ref ShotAttemptedEvent args)
    {
        if (!wieldable.Comp.PreventFiring)
            return;

        if (!TryComp(wieldable.Owner, out UseDelayComponent? useDelayComponent) ||
            !_useDelaySystem.IsDelayed((wieldable.Owner, useDelayComponent), WieldUseDelayId) ||
            !_useDelaySystem.TryGetDelayInfo((wieldable.Owner, useDelayComponent), out var info, WieldUseDelayId))
        {
            return;
        }

        args.Cancel();

        var time = $"{(info.EndTime - _timing.CurTime).TotalSeconds:F1}";

        //_popupSystem.PopupClient(Loc.GetString("rmc-shoot-use-delay", ("seconds", time), ("wieldable", wieldable.Owner)), args.User, args.User);
        // Uncomment when there's a cooldown on popups from a source.
    }

#endregion
}
