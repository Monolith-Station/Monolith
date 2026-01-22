using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.Stickable;

public sealed partial class StickableSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StickableComponent, AfterInteractEvent>(OnInteract);
    }

    private void OnInteract(Entity<StickableComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted)
            return;
        if (args.Target is not { } target)
            return;

        var newCoord = _transform.WithEntityId(args.ClickLocation, target);
        _transform.SetCoordinates(ent, newCoord);
        _audio.PlayPvs(ent.Comp.AttachSound, ent);

        args.Handled = true;
    }
}
