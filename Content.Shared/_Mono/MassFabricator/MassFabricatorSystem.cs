using System.Numerics;
using Content.Shared.Input;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.MassFabricator;

public abstract partial class MassFabricatorSystem : EntitySystem
{
    private const float ReturnStartSpeed = 2f;
    private const float ReturnAcceleration = 32f;
    private const float ReturnMaxSpeed = 20f;
    private const float ReturnRadius = 0.7f;

    [Dependency] private SharedEyeSystem _eyes = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MassFabricatorComponent, UseInHandEvent>(OnUse);
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.EscapeContext, InputCmdHandler.FromDelegate(HandleReturn, handle: false, outsidePrediction: false))
            .Register<MassFabricatorSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<MassFabricatorSystem>();
        base.Shutdown();
    }

    private void HandleReturn(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } user ||
            !TryComp<EyeComponent>(user, out var component) || component.Target is not { } eye ||
            Prototype(eye)?.ID != "HZMassFabricatorEye" || !CanBeginReturn(user, eye))
            return;

        BeginReturn(user, eye);
        OnReturnStarted(user, eye);
    }

    protected virtual bool CanBeginReturn(EntityUid user, EntityUid eye) => true;
    protected virtual void OnReturnStarted(EntityUid user, EntityUid eye) { }

    protected virtual void OnUse(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        StartView(ent, ref args);
    }

    protected EntityUid? StartView(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || HasComp<RelayInputMoverComponent>(args.User))
            return null;

        var eye = PredictedSpawnAtPosition("HZMassFabricatorEye", Transform(args.User).Coordinates);
        var visual = Comp<MassFabricatorEyeComponent>(eye);
        visual.User = args.User;
        visual.Color = ent.Comp.EyeColor;
        visual.BodyPosition = _transforms.GetWorldPosition(args.User);
        visual.BodyMap = Transform(args.User).MapUid;
        Dirty(eye, visual);

        var seed = SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value,
            GetNetEntity(args.User).Id, GetNetEntity(ent.Owner).Id);
        var angle = (float) (new System.Random(seed).NextDouble() * Math.Tau);
        _physics.SetLinearVelocity(eye, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 50f);

        _eyes.SetDrawFov(args.User, false);
        _eyes.SetTarget(args.User, eye);
        _mover.SetRelay(args.User, eye);
        args.Handled = true;
        return eye;
    }

    protected void BeginReturn(EntityUid user, EntityUid eye)
    {
        RemComp<MobMoverComponent>(eye);
        var delta = _transforms.GetWorldPosition(user) - _transforms.GetWorldPosition(eye);
        if (delta.LengthSquared() > 0f)
            _physics.SetLinearVelocity(eye, Vector2.Normalize(delta) * ReturnStartSpeed);
    }

    protected bool UpdateReturn(EntityUid user, EntityUid eye, float frameTime, uint startTick)
    {
        if (Deleted(eye) || Transform(user).MapID != Transform(eye).MapID)
            return true;

        var destination = _transforms.GetWorldPosition(user);
        var delta = destination - _transforms.GetWorldPosition(eye);
        var distance = delta.Length();
        var elapsed = (_timing.CurTick.Value - startTick) * _timing.TickPeriod.TotalSeconds;
        var speed = MathF.Min(ReturnMaxSpeed, ReturnStartSpeed + ReturnAcceleration * (float) elapsed);
        if (distance <= ReturnRadius + speed * frameTime)
        {
            _transforms.SetWorldPosition(eye, destination);
            return true;
        }

        _physics.SetLinearVelocity(eye, delta / distance * speed);
        return false;
    }

    protected void StopPredictedView(EntityUid user)
    {
        var target = TryComp<RelayInputMoverComponent>(user, out var relay) ? relay.RelayEntity : EntityUid.Invalid;
        if (TryComp<EyeComponent>(user, out var eye) && eye.Target is { } eyeTarget)
            target = eyeTarget;

        _eyes.SetTarget(user, null);
        RemComp<RelayInputMoverComponent>(user);
        _eyes.SetDrawFov(user, true);
        if (target.IsValid() && !Deleted(target) && EntityManager.IsClientSide(target))
            QueueDel(target);
    }
}
