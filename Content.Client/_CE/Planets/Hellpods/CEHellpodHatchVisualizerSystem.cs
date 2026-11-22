using Content.Shared._CE.Planets.Hellpods;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._CE.Planets.Hellpods;

public sealed partial class CEHellpodHatchVisualizerSystem : VisualizerSystem<CEHellpodHatchVisualsComponent>
{
    [Dependency] private AnimationPlayerSystem _animations = default!;
    private const string AnimationKey = "hellpod-hatch";

    protected override void OnAppearanceChange(EntityUid uid, CEHellpodHatchVisualsComponent comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !AppearanceSystem.TryGetData<bool>(uid, CEHellpodVisuals.Open, out var open, args.Component)
            || comp.Open == open)
            return;

        comp.Open = open;
        EnsureComp<AnimationPlayerComponent>(uid);
        _animations.Stop(uid, AnimationKey);
        _animations.Play(uid, new Animation
        {
            Length = TimeSpan.FromSeconds(0.7),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = "hatch",
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(open ? "opening" : "closing", 0f),
                        new AnimationTrackSpriteFlick.KeyFrame(open ? "open" : "closed", 0.7f),
                    },
                },
            },
        }, AnimationKey);
    }
}
