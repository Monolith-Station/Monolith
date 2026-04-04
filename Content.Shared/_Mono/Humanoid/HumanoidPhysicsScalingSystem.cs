using System.Linq;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared._Shitmed.Humanoid.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Sprite;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Mono.Humanoid;

/// <summary>
/// System that adjusts physics hitboxes of humanoid entities based on their height and weight (width).
/// </summary>
public sealed class HumanoidPhysicsScalingSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Listen for when a humanoid appearance is loaded (character creation/spawning)
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentStartup>(OnComponentStartup);

        // Listen for when humanoid appearance changes (admin commands, mutations, etc.)
        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentRemove>(OnHumanoidShutdown);

        SubscribeLocalEvent<HumanoidAppearanceComponent, QueryMobThresholdsEvent>(OnQueryMobThresholds);
    }

    private void OnComponentStartup(EntityUid uid, HumanoidAppearanceComponent component, ComponentStartup args)
    {
        AssignDefaultHitboxes(uid, component);
        UpdatePhysicsHitbox(uid, component);
    }

    private void OnHumanoidShutdown(EntityUid uid, HumanoidAppearanceComponent component, ComponentRemove args)
    {
        // Reset hitbox to default when component is removed
        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            ResetToDefaultHitbox(uid, component, fixtures);
        }
    }

    /// <summary>
    /// Public method to manually update a humanoid's hitbox
    /// </summary>
    /// <param name="uid">The entity to update</param>
    public void UpdateHitbox(EntityUid uid)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            UpdatePhysicsHitbox(uid, humanoid);
        }
    }

    /// <summary>
    /// Public method to set specific height and width then update hitbox.
    /// </summary>
    /// <param name="uid">The entity to update</param>
    /// <param name="height">Height multiplier (1.0 = default)</param>
    /// <param name="width">Width multiplier (1.0 = default)</param>
    public void UpdateHitbox(EntityUid uid, float height, float width)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            humanoid.Height = height;
            humanoid.Width = width;
            UpdatePhysicsHitbox(uid, humanoid);
        }
    }

    public void AssignDefaultHitboxes(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (fixture.Shape is PhysShapeCircle)
            {
                var oldRadius = fixture.Shape.Radius;
                humanoid.DefaultFixtures[fixtureId] = oldRadius;
            }
        }
    }

    /// <summary>
    /// Updates the physics hitbox based on the humanoid's height and width.
    /// </summary>
    /// <param name="uid">The entity to update</param>
    /// <param name="humanoid">The humanoid appearance component</param>
    public void UpdatePhysicsHitbox(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        // Calculate the new radius based on height and width
        // We take the average of height and width for a circular hitbox
        var scale = CalculateScale(humanoid);
        // Update all circular fixtures (most humanoids should have just one main fixture)
        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (fixture.Shape is PhysShapeCircle circle && humanoid.DefaultFixtures.TryGetValue(fixtureId, out var oldRadius))
            {
                var newRadius = oldRadius * scale;
                _physics.SetRadius(uid, fixtureId, fixture, circle, newRadius, fixtures);

                // Log the change for debugging
                Log.Debug($"Updated physics hitbox for {ToPrettyString(uid)}: Fixture={fixtureId:F2} Height={humanoid.Height:F2}, Width={humanoid.Width:F2}, Radius={newRadius:F2}");
            }
        }
    }

    private void OnQueryMobThresholds(Entity<HumanoidAppearanceComponent> ent, ref QueryMobThresholdsEvent args)
    {
        args.Scale = CalculateScale(ent.Comp);
        Log.Debug($"Updated damage scale for {ToPrettyString(ent)}: Scale={args.Scale:F2} Height={ent.Comp.Height:F2}, Width={ent.Comp.Width:F2}");
    }

    public float CalculateScale(HumanoidAppearanceComponent humanoid)
    {
        return MathF.Sqrt(MathF.Pow(humanoid.Height, 2) + MathF.Pow(humanoid.Width, 2)) / MathF.Sqrt(2.0f);
    }

    /// <summary>
    /// Resets a humanoid's hitbox to the default size.
    /// </summary>
    /// <param name="uid">The entity to reset</param>
    /// <param name="fixtures">The fixtures component</param>
    private void ResetToDefaultHitbox(EntityUid uid, HumanoidAppearanceComponent humanoid, FixturesComponent fixtures)
    {
        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (fixture.Shape is PhysShapeCircle circle && humanoid.DefaultFixtures.TryGetValue(fixtureId, out var oldRadius))
            {
                _physics.SetRadius(uid, fixtureId, fixture, circle, oldRadius, fixtures);
            }
        }
    }
}
[ByRefEvent]
public record struct QueryMobThresholdsEvent(float Scale, float DeathOffset, float CritOffset);
