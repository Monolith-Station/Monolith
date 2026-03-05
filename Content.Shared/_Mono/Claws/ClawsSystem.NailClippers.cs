using Content.Shared._Mono.Claws.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Claws;

public abstract partial class SharedClawsSystem
{
    private void InitializeNailClippers()
    {
        SubscribeLocalEvent<NailClipperComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<NailClipperComponent, AfterInteractEvent>(OnTargetUse);
        SubscribeLocalEvent<ClawsComponent, NailClipperDoAfterEvent>(ClipNails);
    }

    private void OnUse(Entity<NailClipperComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryClipNails(ent, ent, args.User);
    }

    private void OnTargetUse(Entity<NailClipperComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !args.Target.HasValue)
            return;

        args.Handled = TryClipNails(ent, ent, args.User, args.Target.Value);
    }

    /// <summary>
    /// Used to handle nail clipping action, either from user itself or on the target.
    /// Reduces stage based on <see cref="NailClipperComponent"/>
    /// </summary>
    /// <param name="component"></param>
    /// <param name="nailClipper"></param>
    /// <param name="user"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool TryClipNails(NailClipperComponent component, EntityUid nailClipper, EntityUid user, EntityUid? target = null)
    {
        target ??= user;

        if (!TryComp<ClawsComponent>(target, out var claws)||
            TryGetStage(claws, out var stage) && !stage.CanBeCut)
        {
            _popup.PopupClient(Loc.GetString("has-no-claws-popup"), Transform(user).Coordinates, user);
            return false;
        }

        if ((TryGetStageNumber(claws) <= 0) & (component.DeclawChance < 1))
        {
            _popup.PopupClient(Loc.GetString("claws-too-short-popup"), Transform(user).Coordinates, user);
            return false;
        }

        _popup.PopupClient(Loc.GetString("claws-clipping-doafter"), Transform(user).Coordinates, user);

        var doAfterArgs = new DoAfterArgs(EntityManager,
            user,
            component.ClipDoAfter,
            new NailClipperDoAfterEvent(),
            target,
            target,
            nailClipper)
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

        if (args.Used == null || !TryComp<NailClipperComponent>(args.Used, out var nailClipper))
            return;

        if (nailClipper.DeclawChance > _random.NextFloat())
        {
            Declaw(uid,component);
            return;
        }

        component.ClawStage = component.Claws.GetValueOrDefault(
            Math.Clamp(TryGetStageNumber(component) - nailClipper.StageReduction, 0, int.MaxValue));
        _popup.PopupClient(Loc.GetString("claws-clipping-success"), Transform(uid).Coordinates, uid);

        UpdateClaws(uid, component);
        Dirty(uid, component);
    }
}

[Serializable, NetSerializable]
public sealed partial class NailClipperDoAfterEvent : SimpleDoAfterEvent;
