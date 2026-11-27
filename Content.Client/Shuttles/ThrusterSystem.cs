using Content.Shared.Shuttles.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Shuttles;

/// <summary>
/// Handles making a thruster visibly turn on/emit an exhaust plume according to its state.
/// </summary>
public sealed partial class ThrusterSystem : VisualizerSystem<ThrusterComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        InitializeTransit();
    }

    protected override void OnAppearanceChange(EntityUid uid, ThrusterComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<bool>(uid, ThrusterVisualState.State, out var state, args.Component))
            return;

        SetLayerVisible((uid, args.Sprite), ThrusterVisualLayers.ThrustOn, state);

        var transit = UpdateTransitAppearance(uid, comp, args.Component, args.Sprite);
        var thrusting = AppearanceSystem.TryGetData<bool>(uid,
            ThrusterVisualState.Thrusting,
            out var active,
            args.Component) && active;

        SetThrusting((uid, args.Sprite), state && (transit || thrusting));
    }

    private void SetThrusting(Entity<SpriteComponent?> sprite, bool value)
    {
        SetLayerVisible(sprite, ThrusterVisualLayers.Thrusting, value);
        SetLayerVisible(sprite, ThrusterVisualLayers.ThrustingUnshaded, value);
    }

    private void SetLayerVisible(Entity<SpriteComponent?> sprite, ThrusterVisualLayers key, bool value)
    {
        if (SpriteSystem.TryGetLayer(sprite, key, out var layer, false))
            SpriteSystem.LayerSetVisible(layer, value);
    }
}

public enum ThrusterVisualLayers : byte
{
    Base,
    ThrustOn,
    Thrusting,
    ThrustingUnshaded,
}
