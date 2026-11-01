using Content.Shared._Mono.Tools.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Serialization;
using System.Linq;

namespace Content.Shared._Mono.Tools;

[Serializable, NetSerializable]
public sealed partial class SeparatoryFunnelDoAfterEvent : SimpleDoAfterEvent
{
}

public sealed partial class SeparatoryFunnelSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = null!;
    [Dependency] private SharedDoAfterSystem _doAfter = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private TagSystem _tag = null!;

    public override void Initialize()
    {
        base.Initialize();

        // Strip the inherited beaker tag as soon as the entity initializes
        SubscribeLocalEvent<SeparatoryFunnelComponent, MapInitEvent>(OnMapInit);

        // Stop pouring normally
        SubscribeLocalEvent<SeparatoryFunnelComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(SolutionTransferSystem)]);
        SubscribeLocalEvent<SeparatoryFunnelComponent, SeparatoryFunnelDoAfterEvent>(OnDoAfter);
    }

    private void OnMapInit(EntityUid uid, SeparatoryFunnelComponent component, MapInitEvent args)
    {
        // Prevents players from using a finished funnel to craft ANOTHER funnel
        _tag.RemoveTag(uid, "GlassBeaker");
    }

    private void OnAfterInteract(EntityUid uid, SeparatoryFunnelComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        var user = args.User;

        // Stop chemicals from disappearing when used on player
        if (HasComp<MobStateComponent>(target) || !_solutionContainer.TryGetInjectableSolution(target, out _, out _))
            return;

        if (!_solutionContainer.TryGetSolution(uid, "beaker", out _, out var funnelSolution) || funnelSolution.Contents.Count == 0)
        {
            _popup.PopupClient("The separatory funnel is empty!", uid, user);
            return;
        }

        args.Handled = true;
        _popup.PopupClient("You begin to drain something from the funnel...", uid, user);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, component.Delay, new SeparatoryFunnelDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        });
    }

    private void OnDoAfter(EntityUid uid, SeparatoryFunnelComponent component, SeparatoryFunnelDoAfterEvent args)
    {
        var user = args.Args.User;

        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        if (HasComp<MobStateComponent>(target) || !_solutionContainer.TryGetInjectableSolution(target, out var targetSolutionEnt, out var targetSolution))
            return;

        if (!_solutionContainer.TryGetSolution(uid, "beaker", out var funnelSolutionEnt, out var funnelSolution) || funnelSolution.Contents.Count == 0)
        {
            _popup.PopupClient("The separatory funnel is empty!", uid, user);
            return;
        }

        // Grab the reagent with the lowest volume
        var lowestReagent = funnelSolution.Contents.MinBy(r => r.Quantity);

        // Dump all of that specific reagent
        var transferAmount = FixedPoint2.Min(lowestReagent.Quantity, targetSolution.AvailableVolume);

        if (transferAmount <= FixedPoint2.Zero)
        {
            _popup.PopupClient("That container is full!", target, user);
            return;
        }

        _solutionContainer.RemoveReagent(funnelSolutionEnt.Value, lowestReagent.Reagent.Prototype, transferAmount);
        _solutionContainer.TryAddReagent(targetSolutionEnt.Value, lowestReagent.Reagent.Prototype, transferAmount, out _);

        _popup.PopupClient($"You filter off {transferAmount} unit(s) of the solution.", uid, user);
        args.Handled = true;
    }
}
