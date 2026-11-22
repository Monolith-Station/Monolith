using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client._FarHorizons.StarSystem;
using Content.Client.Parallax;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._CE.Planets.Shields;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.Planets.Hellpods;

public sealed partial class CEHellpodDepartureSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier RicochetScream = new("/Audio/_Mono/Voice/tom_scream.ogg");
    private EntityUid? _aimedPod;
    private Angle _originalRotation;
    private bool _playedScream;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEHellpodDepartureComponent, ComponentStartup>(OnDepartureStartup);
        _overlays.AddOverlay(new DepartureOverlay(this));
    }

    public override void Shutdown()
    {
        ResetRiderAim();
        _overlays.RemoveOverlay<DepartureOverlay>();
        base.Shutdown();
    }

    internal static bool ShouldRicochet(IEntityManager entities, EntityUid planet, CEHellpodRicochet? ricochet) =>
        ricochet is { } bounce && (bounce.Confirmed ||
            entities.TryGetComponent<CEPlanetShieldComponent>(planet, out var shield) && shield.Active);

    internal (PlanetViewProjection Projection, Vector2 Heading) GetRiderProjection(
        CEHellpodApproachComponent approach, CEHellpodComponent? pod, Vector2 viewCenter,
        Vector2 planetPosition, float parallax, float baseRadius)
    {
        var crossing = approach.End;
        var now = _timing.CurTime;
        var duration = crossing - approach.Start;
        var elapsed = now - approach.Start;
        var progress = GetProgress(elapsed, duration);
        var radius = PlanetViewProjection.GrowRadius(baseRadius, progress);
        var projection = PlanetViewProjection.FromSurfacePoint(approach.FormationCenter, approach.SurfacePoint, radius);
        if (pod == null)
            return (projection, Vector2.Zero);

        var position = _transform.GetWorldPosition(pod.Owner);
        var entry = projection.SurfacePoint(pod.OrbitalEntry);
        var heading = entry - position;
        if (now >= crossing && ShouldRicochet(EntityManager, approach.Planet, pod.Ricochet) && pod.Ricochet is { } bounce)
        {
            var rebound = ReboundTravel(now - crossing, duration);
            // Express the final orbital projection in the rider's current map coordinates.
            var orbit = PlanetViewProjection.Orbit(viewCenter, planetPosition - (bounce.Exit - position), parallax, baseRadius);
            var surface = Vector2.Lerp(position, orbit.SurfacePoint(pod.OrbitalEntry), rebound);
            radius = MathHelper.Lerp(radius, baseRadius, rebound);
            return (PlanetViewProjection.FromSurfacePoint(surface, pod.OrbitalEntry, radius), heading);
        }

        var (travel, _) = ProjectFlight(progress, parallax);
        return (PlanetViewProjection.FromSurfacePoint(Vector2.Lerp(entry, position, travel), pod.OrbitalEntry, radius), heading);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!TryComp<TransformComponent>(_player.LocalEntity, out var rider) ||
            !TryComp<CEHellpodApproachComponent>(rider.MapUid, out var approach) ||
            !TryComp<CEHellpodComponent>(rider.ParentUid, out var pod) || !pod.InFlight ||
            !TryComp<SpriteComponent>(rider.ParentUid, out var sprite) ||
            !TryGetPlanet(approach.Planet, out var planet, out var planetPosition))
        {
            ResetRiderAim();
            return;
        }

        if (_aimedPod != rider.ParentUid)
        {
            ResetRiderAim();
            _aimedPod = rider.ParentUid;
            _originalRotation = sprite.Rotation;
        }

        var (parallax, radiusScale) = planet.GetProjection();
        var (_, heading) = GetRiderProjection(approach, pod, _eye.CurrentEye.Position.Position,
            planetPosition, parallax, planet.Radius * radiusScale);
        var target = Facing(heading, _eye.CurrentEye.Rotation);
        _sprites.SetRotation((rider.ParentUid, sprite), Angle.Lerp(sprite.Rotation, target, Math.Clamp(frameTime * 12f, 0f, 1f)));

        if (!_playedScream && _timing.CurTime >= approach.End &&
            ShouldRicochet(EntityManager, approach.Planet, pod.Ricochet))
        {
            _playedScream = true;
            _audio.PlayGlobal(RicochetScream, Filter.Local(), false);
        }
    }

    private void ResetRiderAim()
    {
        if (_aimedPod is { } uid && TryComp<SpriteComponent>(uid, out var sprite))
            _sprites.SetRotation((uid, sprite), _originalRotation);
        _aimedPod = null;
        _playedScream = false;
    }

    private static float GetProgress(TimeSpan elapsed, TimeSpan duration) =>
        duration > TimeSpan.Zero ? Math.Clamp((float) (elapsed / duration), 0f, 1f) : 1f;

    private static float ReboundTravel(TimeSpan elapsed, TimeSpan duration)
    {
        var progress = GetProgress(elapsed, duration);
        return progress * (2f - progress);
    }

    private static Angle Facing(Vector2 heading, Angle eyeRotation) => heading.LengthSquared() > 0.0001f
        ? Angle.FromWorldVec(eyeRotation.RotateVec(heading))
        : Angle.Zero;

    private bool TryGetPlanet(EntityUid uid, [NotNullWhen(true)] out Planet? planet, out Vector2 position)
    {
        planet = null;
        position = default;
        if (!TryComp<PlanetBodyComponent>(uid, out var body) ||
            !TryComp<TransformComponent>(uid, out var xform) ||
            !TryComp<StarSystemMapComponent>(body.StarSystemMap, out var system) ||
            system.StarSystem is not { } starSystem || (uint) body.Index >= (uint) starSystem.Planets.Count)
            return false;

        planet = starSystem.Planets[body.Index];
        position = xform.LocalPosition;
        return true;
    }

    private void OnDepartureStartup(Entity<CEHellpodDepartureComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprites.SetVisible((ent, sprite), false);
    }

    internal static (float Travel, float Scale) ProjectFlight(float progress, float parallax)
    {
        var squared = progress * progress;
        if (parallax <= 0f)
            return (squared, 1f - MathF.Sqrt(progress));

        var distance = squared * squared;
        var depth = MathHelper.Lerp(parallax, 1f, distance);
        return (distance / depth, parallax / depth);
    }

    private void DrawDepartures(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye)
            return;

        var up = (-eye.Rotation).RotateVec(Vector2.UnitY);

        var pods = EntityQueryEnumerator<CEHellpodDepartureComponent, SpriteComponent, TransformComponent>();
        while (pods.MoveNext(out var uid, out var departure, out var sprite, out var xform))
        {
            var duration = departure.End - departure.Start;
            var now = _timing.CurTime;
            if (xform.MapUid != args.MapUid || duration <= TimeSpan.Zero)
                continue;

            var bouncing = now >= departure.End && ShouldRicochet(EntityManager, departure.Planet, departure.Ricochet);
            var visibleUntil = bouncing ? departure.End + duration : departure.End;
            if (now >= visibleUntil)
                continue;

            var progress = GetProgress(now - departure.Start, duration);
            var origin = _transform.GetWorldPosition(xform);
            var start = origin + up * 0.5f;
            var end = start - up * 2f;
            var (travel, scale) = ProjectFlight(progress, 0f);
            if (TryGetPlanet(departure.Planet, out var planet, out var planetPosition))
            {
                var (parallax, radiusScale) = planet.GetProjection();
                var projection = PlanetViewProjection.Orbit(eye.Position.Position, planetPosition, parallax, planet.Radius * radiusScale);
                end = projection.SurfacePoint(departure.Entry);

                (travel, scale) = ProjectFlight(progress, parallax);
            }

            if (bouncing && departure.Ricochet is { } ricochet)
            {
                start = end;
                end = ricochet.Exit;
                travel = ReboundTravel(now - departure.End, duration);
                scale = MathHelper.Lerp(scale, 1f, travel);
            }

            // Cancel camera zoom, not the depth-based shrink.
            var spriteScale = scale * eye.Scale.X;
            if (spriteScale < 0.005f)
                continue;

            _sprites.SetScale((uid, sprite), new Vector2(spriteScale));
            _sprites.SetRotation((uid, sprite), Facing(end - start, eye.Rotation));
            _sprites.SetOffset((uid, sprite), Vector2.Zero);
            _sprites.RenderSprite((uid, sprite), args.WorldHandle, eye.Rotation, Angle.Zero, Vector2.Lerp(start, end, travel));
        }
    }

    private sealed class DepartureOverlay : Overlay
    {
        private readonly CEHellpodDepartureSystem _system;
        public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

        public DepartureOverlay(CEHellpodDepartureSystem system)
        {
            _system = system;
            ZIndex = ParallaxSystem.ParallaxZIndex + 2;
        }

        protected override void Draw(in OverlayDrawArgs args) => _system.DrawDepartures(args);
    }
}
