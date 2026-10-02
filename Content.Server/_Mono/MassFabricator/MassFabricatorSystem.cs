using Content.Shared._Mono.MassFabricator;
using Content.Shared.Eye;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._Mono.MassFabricator;

public sealed partial class MassFabricatorSystem : Content.Shared._Mono.MassFabricator.MassFabricatorSystem
{
    [Dependency] private SharedEyeSystem _eyes = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MassFabricatorViewComponent, ComponentShutdown>(OnViewShutdown);
        SubscribeLocalEvent<MassFabricatorComponent, ComponentShutdown>(OnFabricatorShutdown);
        SubscribeNetworkEvent<MassFabricatorSelectEvent>(OnSelect);
    }

    protected override void OnUse(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (HasComp<MassFabricatorViewComponent>(args.User))
            return;

        if (StartView(ent, ref args) is not { } eye)
            return;

        var view = EnsureComp<MassFabricatorViewComponent>(args.User);
        view.Eye = eye;
        view.Fabricator = ent.Owner;
        RaiseNetworkEvent(new MassFabricatorViewEvent(true), actor.PlayerSession);
    }

    private void OnSelect(MassFabricatorSelectEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<HandsComponent>(user, out var hands) ||
            !TryGetEntity(message.Fabricator, out var uid) || uid is not { } fabricator ||
            hands.ActiveHandEntity != fabricator ||
            !TryComp<MassFabricatorComponent>(fabricator, out var comp))
            return;

        TrySelectBlueprint((fabricator, comp), message.Blueprint, message.Target);
    }

    protected override bool CanModifyPlans(EntityUid user, EntityUid gun) =>
        TryComp<MassFabricatorViewComponent>(user, out var view) && !view.Returning &&
        view.Fabricator == gun && TryComp<HandsComponent>(user, out var hands) &&
        hands.ActiveHandEntity == gun;

    protected override bool CanBeginReturn(EntityUid user, EntityUid eye)
    {
        return TryComp<MassFabricatorViewComponent>(user, out var view) &&
               view.Eye == eye && !view.Returning;
    }

    protected override void OnReturnStarted(EntityUid user, EntityUid eye)
    {
        var view = Comp<MassFabricatorViewComponent>(user);
        view.Returning = true;
        view.ReturnStartTick = _timing.CurTick.Value;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var fading = EntityQueryEnumerator<MassFabricatorHologramComponent>();
        while (fading.MoveNext(out var uid, out var plan))
            if (plan.PrintState == MassFabricatorPrintState.Fading &&
                (_timing.CurTime - plan.PrintStarted).TotalSeconds >= MassFabricatorHologramComponent.FadeSeconds)
                QueueDel(uid);

        var finished = new List<EntityUid>();
        var query = EntityQueryEnumerator<MassFabricatorViewComponent>();
        while (query.MoveNext(out var user, out var view))
        {
            if (TryComp<MassFabricatorEyeComponent>(view.Eye, out var visual))
            {
                var position = _transforms.GetWorldPosition(user);
                var map = Transform(user).MapUid;
                if (visual.BodyPosition != position || visual.BodyMap != map)
                {
                    visual.BodyPosition = position;
                    visual.BodyMap = map;
                    Dirty(view.Eye, visual);
                }
            }

            if (view.Returning && UpdateReturn(user, view.Eye, frameTime, view.ReturnStartTick))
                finished.Add(user);
        }

        foreach (var user in finished)
            RemComp<MassFabricatorViewComponent>(user);
    }

    private void OnFabricatorShutdown(Entity<MassFabricatorComponent> ent, ref ComponentShutdown args)
    {
        DeleteOwnedPlans(ent);

        var query = EntityQueryEnumerator<MassFabricatorViewComponent>();
        while (query.MoveNext(out var user, out var view))
        {
            if (view.Fabricator == ent.Owner)
                RemComp<MassFabricatorViewComponent>(user);
        }
    }

    private void OnViewShutdown(Entity<MassFabricatorViewComponent> ent, ref ComponentShutdown args)
    {
        RemComp<RelayInputMoverComponent>(ent.Owner);
        _eyes.SetTarget(ent.Owner, null);
        _eyes.SetDrawFov(ent.Owner, true);
        QueueDel(ent.Comp.Eye);
        if (TryComp<ActorComponent>(ent.Owner, out var actor))
            RaiseNetworkEvent(new MassFabricatorViewEvent(false), actor.PlayerSession);
    }
}
