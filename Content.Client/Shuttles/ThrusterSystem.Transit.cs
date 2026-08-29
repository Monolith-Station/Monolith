using Content.Shared._FarHorizons.StarSystem;
using Content.Shared.Shuttles.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Shuttles;

public sealed partial class ThrusterSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float MaximumSpeed = 4f;
    private static readonly ResPath OverclockedRsi = new("_FarHorizons/Effects/overclocked_thruster.rsi");
    private static readonly RSI.StateId OverclockedState = new("overclocked");
    private static readonly TimeSpan FailureFadeTime = TimeSpan.FromSeconds(2);
    private readonly HashSet<EntityUid> _transitThrusters = new();

    private void InitializeTransit()
        => SubscribeLocalEvent<ThrusterComponent, ComponentShutdown>(OnTransitShutdown);

    private void OnTransitShutdown(Entity<ThrusterComponent> ent, ref ComponentShutdown args)
        => _transitThrusters.Remove(ent);

    private bool UpdateTransitAppearance(
        EntityUid uid,
        ThrusterComponent comp,
        AppearanceComponent appearance,
        SpriteComponent sprite)
    {
        CacheTransitLayers((uid, sprite), comp);

        var transit = AppearanceSystem.TryGetData<bool>(uid,
            ThrusterVisualState.PlanetTransit,
            out var active,
            appearance) && active;

        if (transit)
            _transitThrusters.Add(uid);
        else
        {
            _transitThrusters.Remove(uid);
            RestoreTransitLayers((uid, sprite), comp);
        }

        return transit;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var uid in _transitThrusters)
        {
            if (!TryComp<ThrusterComponent>(uid, out var comp) ||
                !TryComp<SpriteComponent>(uid, out var sprite) ||
                !TryComp(uid, out TransformComponent? xform))
                continue;

            CacheTransitLayers((uid, sprite), comp);
            if (xform.GridUid is not { } grid)
            {
                RestoreTransitLayers((uid, sprite), comp);
                continue;
            }

            if (TryComp<PlanetTransitFailureComponent>(grid, out var failure))
            {
                AnimateTransitFailure((uid, sprite), comp, failure, frameTime);
                continue;
            }

            if (!TryComp<PlanetTransitComponent>(grid, out var transit))
                continue;

            switch (transit.Phase)
            {
                case PlanetTransitPhase.Charging:
                {
                    var progress = PhaseProgress(transit);
                    ApplyTransitPlumes((uid, sprite), comp, MathHelper.Lerp(1f, MaximumSpeed, progress), frameTime, true);
                    break;
                }
                case PlanetTransitPhase.Departing:
                {
                    var progress = PhaseProgress(transit);
                    ApplyTransitPlumes((uid, sprite), comp, MathHelper.Lerp(MaximumSpeed, 1f, progress), frameTime, progress < 1f);
                    break;
                }
                default:
                    ApplyTransitPlumes((uid, sprite), comp, 1f, frameTime, false);
                    break;
            }
        }
    }

    private float PhaseProgress(PlanetTransitComponent transit)
    {
        var duration = (transit.PhaseEnd - transit.PhaseStart).TotalSeconds;
        var elapsed = (_timing.CurTime - transit.PhaseStart).TotalSeconds;
        var progress = duration > 0 ? Math.Clamp((float) (elapsed / duration), 0f, 1f) : 1f;
        return progress * progress * (3f - 2f * progress);
    }

    private void AnimateTransitFailure(
        Entity<SpriteComponent?> sprite,
        ThrusterComponent comp,
        PlanetTransitFailureComponent failure,
        float frameTime)
    {
        var progress = Math.Clamp((float) ((_timing.CurTime - failure.Start) / FailureFadeTime), 0f, 1f);
        var alpha = 1f - progress;
        ApplyTransitPlumes(sprite, comp, 1f, frameTime, null, alpha, true);

        if (SpriteSystem.LayerMapTryGet(sprite, ThrusterVisualLayers.ThrustOn, out var index, false) &&
            SpriteSystem.TryGetLayer(sprite, index, out var layer, false) &&
            comp.TransitLayers.TryGetValue(index, out var original))
        {
            SpriteSystem.LayerSetVisible(layer, progress < 1f);
            SpriteSystem.LayerSetColor(layer, original.Color.WithAlpha(original.Color.A * alpha));
            sprite.Comp!.LayerSetShader(index, "PlanetTransitFailure");
        }
    }

    private void ApplyTransitPlumes(
        Entity<SpriteComponent?> sprite,
        ThrusterComponent comp,
        float speed,
        float frameTime,
        bool? overclocked,
        float alpha = 1f,
        bool failed = false)
    {
        ApplyTransitPlume(sprite, comp, ThrusterVisualLayers.Thrusting, speed, frameTime, overclocked, alpha, failed);
        ApplyTransitPlume(sprite, comp, ThrusterVisualLayers.ThrustingUnshaded, speed, frameTime, overclocked, alpha, failed);
    }

    private void ApplyTransitPlume(
        Entity<SpriteComponent?> sprite,
        ThrusterComponent comp,
        ThrusterVisualLayers key,
        float speed,
        float frameTime,
        bool? overclocked,
        float alpha,
        bool failed)
    {
        if (!SpriteSystem.LayerMapTryGet(sprite, key, out var index, false) ||
            !SpriteSystem.TryGetLayer(sprite, index, out var layer, false) ||
            !comp.TransitLayers.TryGetValue(index, out var original))
            return;

        SpriteSystem.LayerSetVisible(layer, alpha > 0f);
        SpriteSystem.LayerSetColor(layer, original.Color.WithAlpha(original.Color.A * alpha));

        if (overclocked != null)
            SetOverclockedState(sprite, index, layer, original, overclocked.Value);

        if (speed > 1f)
            SpriteSystem.LayerSetAnimationTime(layer, layer.AnimationTime + frameTime * (speed - 1f));

        if (failed)
            sprite.Comp!.LayerSetShader(index, "PlanetTransitFailure");
    }

    private void SetOverclockedState(
        Entity<SpriteComponent?> sprite,
        int index,
        SpriteComponent.Layer layer,
        ThrusterLayerVisual original,
        bool overclocked)
    {
        if (overclocked)
        {
            if (layer.State == OverclockedState)
                return;

            SpriteSystem.LayerSetRsi(sprite, index, OverclockedRsi, OverclockedState);
            return;
        }

        if (layer.State != OverclockedState)
            return;

        SpriteSystem.LayerSetRsi(sprite, index, original.Rsi, original.State);
    }

    private void CacheTransitLayers(Entity<SpriteComponent?> sprite, ThrusterComponent comp)
    {
        if (comp.LayersCached)
            return;

        CacheTransitLayer(sprite, comp, ThrusterVisualLayers.ThrustOn);
        CacheTransitLayer(sprite, comp, ThrusterVisualLayers.Thrusting);
        CacheTransitLayer(sprite, comp, ThrusterVisualLayers.ThrustingUnshaded);
        comp.LayersCached = true;
    }

    private void CacheTransitLayer(Entity<SpriteComponent?> sprite, ThrusterComponent comp, ThrusterVisualLayers key)
    {
        if (SpriteSystem.LayerMapTryGet(sprite, key, out var index, false) &&
            SpriteSystem.TryGetLayer(sprite, index, out var layer, false))
            comp.TransitLayers.TryAdd(index, new ThrusterLayerVisual(layer.Color, layer.RSI, layer.State));
    }

    private void RestoreTransitLayers(Entity<SpriteComponent?> sprite, ThrusterComponent comp)
    {
        foreach (var (index, original) in comp.TransitLayers)
        {
            SpriteSystem.LayerSetColor(sprite, index, original.Color);
            SpriteSystem.LayerSetRsi(sprite, index, original.Rsi, original.State);
        }

        RestoreTransitShader(sprite.Comp!, ThrusterVisualLayers.ThrustOn);
        RestoreTransitShader(sprite.Comp!, ThrusterVisualLayers.Thrusting);
        RestoreTransitShader(sprite.Comp!, ThrusterVisualLayers.ThrustingUnshaded);
    }

    private static void RestoreTransitShader(SpriteComponent sprite, ThrusterVisualLayers key)
    {
        if (!sprite.LayerMapTryGet(key, out var index, false))
            return;

        if (key == ThrusterVisualLayers.Thrusting)
            sprite.LayerSetShader(index, null, null);
        else
            sprite.LayerSetShader(index, "unshaded");
    }
}
