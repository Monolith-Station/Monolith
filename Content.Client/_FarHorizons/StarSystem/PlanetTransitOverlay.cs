using System.Numerics;
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
            !_entities.TryGetComponent<TransformComponent>(player, out var xform) ||
            xform.GridUid is not { } grid ||
            !_transits.TryGetTransit(grid, out var transit))
        {
            return;
        }

        var duration = (transit.PhaseEnd - transit.PhaseStart).TotalSeconds;
        var progress = duration <= 0
            ? 1f
            : Math.Clamp((float)((_timing.CurTime - transit.PhaseStart).TotalSeconds / duration), 0f, 1f);

        var alpha = transit.Phase switch
        {
            PlanetTransitPhase.Departing => SmoothStep(Math.Clamp((progress - 0.55f) / 0.45f, 0f, 1f)),
            PlanetTransitPhase.Arriving => SmoothStep(1f - progress),
            _ => 0f,
        };

        if (alpha <= 0.001f)
            return;

        _cloudShader ??= _prototypes.Index<ShaderPrototype>("CEZClouds").InstanceUnique();
        _cloudShader.SetParameter("CLOUD_COLOR", Vector3.One);
        _cloudShader.SetParameter("COVERAGE", alpha);
        _cloudShader.SetParameter("WISP", 0f);

        args.ScreenHandle.UseShader(_cloudShader);
        args.ScreenHandle.DrawRect(args.ViewportBounds, Color.White);
        args.ScreenHandle.UseShader(null);
    }

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);
}
