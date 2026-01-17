using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Claws;

public abstract partial class SharedClawsSystem
{
    private void InitializeNailClippers()
    {
        SubscribeLocalEvent<NailCutterComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<NailCutterComponent, AfterInteractEvent>(OnTargetUse);
        SubscribeLocalEvent<ClawsComponent, NailClipperDoAfterEvent>(ClipNails);
    }

    private void OnUse(EntityUid uid, NailCutterComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryClipNails(component, args.User);
    }

    private void OnTargetUse(EntityUid uid, NailCutterComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        args.Handled = TryClipNails(component, args.User, args.Target.Value);
    }

    /// <summary>
    /// Used to handle nail clipping action, either from user itself or on the target.
    /// Reduces stage based on <see cref="NailCutterComponent"/>
    /// </summary>
    /// <param name="component"></param>
    /// <param name="user"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool TryClipNails(NailCutterComponent component, EntityUid user, EntityUid? target = null)
    {
        target ??= user;

        if (!TryComp<ClawsComponent>(user, out var claws))
        {
            _popup.PopupClient(Loc.GetString("has-no-claws-popup"), Transform(user).Coordinates, user);
            return false;
        }

        if (claws.ClawStage == 0)
        {
            _popup.PopupClient(Loc.GetString("claws-too-short-popup"), Transform(user).Coordinates, user);
            return false;
        }

        _popup.PopupClient(Loc.GetString("claws-clipping-doafter"), Transform(user).Coordinates, user);

        var doAfterArgs = new DoAfterArgs(EntityManager,
            user,
            component.ClipDoAfter,
            new NailClipperDoAfterEvent(),
            target)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
        };

        return _doafter.TryStartDoAfter(doAfterArgs);
    }

    public void ClipNails(EntityUid uid, ClawsComponent component, NailClipperDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        component.ClawStage -= 1;
        _popup.PopupClient(Loc.GetString("claws-clipping-success"), Transform(uid).Coordinates, uid);

        UpdateClaws(uid, component);
        Dirty(uid, component);
    }
}

[Serializable, NetSerializable]
public sealed partial class NailClipperDoAfterEvent : SimpleDoAfterEvent;
