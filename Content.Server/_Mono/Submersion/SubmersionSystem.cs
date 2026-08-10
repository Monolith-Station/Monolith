using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._Mono.Submersion;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Standing;
using Content.Shared.Sprite;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Submersion;

/// <summary>
/// glub
/// </summary>
public sealed partial class SubmersionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedScaleVisualsSystem _scale = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly EntProtoId SplashEffect = "MonoSubmergeSplash";
    private const float SplashSpriteTiles = 1f;
    private const float SplashScale = 2.2f;
    private const float SplashMinimumScale = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubmergedComponent, InhaleLocationEvent>(OnInhale);
    }

    /// <summary>
    /// You aren't a fish. You can't drink water. (Unless you are a fish, in which case this should be edited to account for that.)
    /// (If you DO add an aquatic species or want to add support for animals to this, just add a proper CanBreatheWater component...)
    /// </summary>
    private void OnInhale(Entity<SubmergedComponent> ent, ref InhaleLocationEvent args)
    {
        args.Gas ??= new GasMixture(Atmospherics.BreathVolume);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StandingStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var standing, out var xform))
        {
            var submerged = HasComp<SubmergedComponent>(uid);

            if (standing.CurrentState == StandingState.Standing && !submerged)
                continue;

            var wants = standing.CurrentState == StandingState.Lying && IsSubmersible(xform);

            if (wants == submerged)
                continue;

            if (wants)
                AddComp<SubmergedComponent>(uid);
            else
                RemComp<SubmergedComponent>(uid);

            Splash(uid);
        }

        SinkItems();
    }

    private void SinkItems()
    {
        var query = EntityQueryEnumerator<ItemComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            // Virtual items don't exist.
            if (HasComp<VirtualItemComponent>(uid))
                continue;

            var submerged = HasComp<SubmergedComponent>(uid);

            var wants = !xform.Anchored
                && !_container.IsEntityInContainer(uid)
                && IsSubmersible(xform);

            if (wants == submerged)
                continue;

            if (wants)
                AddComp<SubmergedComponent>(uid);
            else
                RemComp<SubmergedComponent>(uid);

            Splash(uid);
        }
    }

    private void Splash(EntityUid uid)
    {
        var effect = Spawn(SplashEffect, Transform(uid).Coordinates);

        var bounds = _lookup.GetWorldAABB(uid);
        var size = MathF.Max(bounds.Width, bounds.Height);

        if (size <= 0f)
            return;

        var scale = MathF.Max(size / SplashSpriteTiles * SplashScale, SplashMinimumScale);

        _scale.SetSpriteScale(effect, new Vector2(scale));
    }

    private bool IsSubmersible(TransformComponent xform)
    {
        if (xform.GridUid == null || !_turf.TryGetTileRef(xform.Coordinates, out var tileRef))
            return false;

        return _turf.GetContentTileDefinition(tileRef.Value).Submersible;
    }
}
