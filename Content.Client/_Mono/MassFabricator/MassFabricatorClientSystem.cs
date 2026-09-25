using Content.Client.Gameplay;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Timing;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorClientSystem : MassFabricatorSystem
{
    [Dependency] private IStateManager _states = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;

    public bool Returning { get; private set; }
    private EntityUid? _viewUser;
    private EntityUid? _hiddenUser;
    private uint _returnStartTick;
    private uint _returnFinishTick;
    private bool _completedLocally;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MassFabricatorViewEvent>(OnView);
        _overlays.AddOverlay(new MassFabricatorEyeOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<MassFabricatorEyeOverlay>();
        base.Shutdown();
    }

    protected override void OnUse(Entity<MassFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (Returning || StartView(ent, ref args) == null || args.User != _players.LocalEntity || !_timing.IsFirstTimePredicted)
            return;

        Returning = false;
        _completedLocally = false;
        _returnStartTick = 0;
        _viewUser = args.User;
        _hiddenUser = null;
        _states.RequestStateChange<MassFabricatorState>();
    }

    private void OnView(MassFabricatorViewEvent ev)
    {
        if (ev.Enabled && _states.CurrentState is not MassFabricatorState)
        {
            _hiddenUser = null;
            _viewUser = _players.LocalEntity;
            _states.RequestStateChange<MassFabricatorState>();
        }
        else if (!ev.Enabled)
        {
            Returning = false;
            _completedLocally = false;
            _returnStartTick = 0;
            if (_viewUser is { } user && !Deleted(user))
                StopPredictedView(user);
            _viewUser = null;
            if (_states.CurrentState is MassFabricatorState)
                _states.RequestStateChange<GameplayState>();
        }
    }

    public bool IsEyeHidden(EntityUid user) => _hiddenUser == user;

    protected override bool CanBeginReturn(EntityUid user, EntityUid eye) => _viewUser == user;

    protected override void OnReturnStarted(EntityUid user, EntityUid eye)
    {
        if (!Returning)
            _returnStartTick = _timing.CurTick.Value;
        Returning = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!Returning || !_timing.InPrediction || _timing.CurTick.Value < _returnStartTick ||
            _players.LocalEntity is not { } user)
            return;

        if (_completedLocally)
        {
            if (_timing.CurTick.Value >= _returnFinishTick)
                StopPredictedView(user);
            return;
        }

        if (!TryComp<EyeComponent>(user, out var eye) || eye.Target is not { } target)
            return;

        if (HasComp<MobMoverComponent>(target))
            BeginReturn(user, target);
        if (UpdateReturn(user, target, frameTime, _returnStartTick))
        {
            _completedLocally = true;
            _returnFinishTick = _timing.CurTick.Value;
            _hiddenUser = user;
            StopPredictedView(user);
            if (_timing.IsFirstTimePredicted && _states.CurrentState is MassFabricatorState)
                _states.RequestStateChange<GameplayState>();
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_completedLocally && Returning && _timing.CurTick.Value >= _returnFinishTick &&
            _viewUser is { } user && !Deleted(user) &&
            (HasComp<RelayInputMoverComponent>(user) ||
             TryComp<EyeComponent>(user, out var eye) && eye.Target != null))
            StopPredictedView(user);

        if (_completedLocally && _timing.CurTick.Value >= _returnFinishTick &&
            _states.CurrentState is MassFabricatorState)
            _states.RequestStateChange<GameplayState>();
    }

}
