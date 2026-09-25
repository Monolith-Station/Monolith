using Content.Shared._Mono.MassFabricator;
using Content.Shared.Eye;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

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
