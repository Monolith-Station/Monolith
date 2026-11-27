using System.Numerics;
using Content.Client._CE.Planets.Hellpods;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._FarHorizons.StarSystem;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.StarSystem;

public sealed class PlanetTransitOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly IPrototypeManager _prototypes;
    private readonly PlanetTransitSystem _transits;
    private ShaderInstance? _cloudShader;
    private (EntityUid Pod, TimeSpan Arrival)? _hellpodArrival;
    private TimeSpan _hellpodFadeStart;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PlanetTransitOverlay(IEntityManager entities,
        IPlayerManager player,
        IGameTiming timing,
        IPrototypeManager prototypes,
        PlanetTransitSystem transits)
    {
        _entities = entities;
        _player = player;
        _timing = timing;
        _prototypes = prototypes;
        _transits = transits;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            !_entities.TryGetComponent<TransformComponent>(player, out var xform))
        {
            return;
        }

        var alpha = GetCoverage(xform, args.MapUid);
        if (alpha <= 0.001f || args.Viewport.Eye is not { } eye)
            return;

        eye.GetViewMatrixInv(out var inverseView, args.Viewport.RenderScale);
        var size = (Vector2) args.Viewport.Size / EyeManager.PixelsPerMeter;
        _cloudShader ??= _prototypes.Index<ShaderPrototype>("CEZClouds").InstanceUnique();
        _cloudShader.SetParameter("WORLD_ORIGIN", Vector2.Transform(-size / 2f, inverseView));
        _cloudShader.SetParameter("WORLD_X", Vector2.TransformNormal(new Vector2(size.X, 0f), inverseView));
        _cloudShader.SetParameter("WORLD_Y", Vector2.TransformNormal(new Vector2(0f, size.Y), inverseView));
        _cloudShader.SetParameter("CLOUD_COLOR", Vector3.One);
        _cloudShader.SetParameter("COVERAGE", alpha);
        _cloudShader.SetParameter("WISP", 0f);

        args.ScreenHandle.UseShader(_cloudShader);
        args.ScreenHandle.DrawRect(args.ViewportBounds, Color.White);
        args.ScreenHandle.UseShader(null);
    }

    private float GetCoverage(TransformComponent xform, EntityUid viewMap)
    {
        if (xform.MapUid == viewMap &&
            _entities.TryGetComponent<CEHellpodComponent>(xform.ParentUid, out var pod) && pod.InFlight)
        {
            if (_entities.TryGetComponent<CEHellpodApproachComponent>(viewMap, out var approach))
            {
                if (CEHellpodDepartureSystem.ShouldRicochet(_entities, approach.Planet, pod.Ricochet))
                    return 0f;

                return SmoothStep(GetProgress(approach.End - CEHellpodApproachComponent.CloudFadeIn, approach.End));
            }

            if (pod.OrbitalArrivalAt is { } arrival)
            {
                var key = (pod.Owner, arrival);
                var now = _timing.RealTime;
                if (_hellpodArrival != key)
                {
                    _hellpodArrival = key;
                    _hellpodFadeStart = now;
                }

                var fadeProgress = (float) ((now - _hellpodFadeStart) / CEHellpodApproachComponent.CloudFadeOut);
                return SmoothStep(1f - Math.Clamp(fadeProgress, 0f, 1f));
            }
        }

        if (xform.GridUid is not { } grid || !_transits.TryGetTransit(grid, out var transit))
            return 0f;

        var progress = GetProgress(transit.PhaseStart, transit.PhaseEnd);
        return transit.Phase switch
        {
            PlanetTransitPhase.Departing => SmoothStep(Math.Clamp((progress - 0.55f) / 0.45f, 0f, 1f)),
            PlanetTransitPhase.Arriving => SmoothStep(1f - progress),
            _ => 0f,
        };
    }

    private float GetProgress(TimeSpan start, TimeSpan end) => end <= start
        ? 1f
        : Math.Clamp((float) ((_timing.CurTime - start).TotalSeconds / (end - start).TotalSeconds), 0f, 1f);

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);
}
