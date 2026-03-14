using Content.Server._Mono.NPC.HTN.Operators;
using Content.Server.NPC.HTN;
using Content.Shared.Popups;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Shuttles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Mono.Shuttles;

public sealed partial class ShuttleConsoleAutopilotSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, ShuttleConsoleAutopilotPositionMessage>(OnAutopilotMessage);
        SubscribeLocalEvent<ShuttleConsoleComponent, SteeringDoneEvent>(OnSteeringDone);
    }

    private void OnAutopilotMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleAutopilotPositionMessage args)
    {
        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        var blackboard = htn.Blackboard;
        blackboard.SetValue(ent.Comp.AutopilotTargetKey, _transform.ToCoordinates(args.Coordinates));
        blackboard.SetValue(ent.Comp.AutopilotRotationKey, args.Angle + MathF.PI);
    }

    private void OnSteeringDone(Entity<ShuttleConsoleComponent> ent, ref SteeringDoneEvent args)
    {
        _audio.PlayPvs(ent.Comp.AutopilotDoneSound, ent);
        _popup.PopupEntity(Loc.GetString("shuttle-console-autopilot-popup-done"), ent, PopupType.Medium);
    }
}
