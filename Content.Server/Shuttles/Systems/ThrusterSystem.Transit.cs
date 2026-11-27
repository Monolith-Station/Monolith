using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ThrusterSystem
{
    private const int PlanetTransitHeatDamage = 5000;
    private static readonly List<Vector2> PlanetTransitBurnPoly =
    [
        new(-0.8f, 0.5f),
        new(-0.1f, 3.5f),
        new(0.1f, 3.5f),
        new(0.8f, 0.5f),
    ];

    public void SetPlanetTransitVisuals(ShuttleComponent component, bool enabled)
    {
        var appearanceQuery = GetEntityQuery<AppearanceComponent>();
        var thrusterQuery = GetEntityQuery<ThrusterComponent>();
        foreach (var thrusters in component.LinearThrusters)
        {
            foreach (var uid in thrusters)
            {
                appearanceQuery.TryGetComponent(uid, out var appearance);
                _appearance.SetData(uid, ThrusterVisualState.PlanetTransit, enabled, appearance);

                if (thrusterQuery.TryGetComponent(uid, out var thruster))
                    SetPlanetTransitBurnFixture(uid, thruster, enabled);
            }
        }
    }

    private bool IsPlanetTransitOverclocked(EntityUid uid)
    {
        var grid = Transform(uid).GridUid;
        return grid != null &&
               TryComp<PlanetTransitComponent>(grid, out var transit) &&
               transit.Phase is PlanetTransitPhase.Charging or PlanetTransitPhase.Departing;
    }

    private void ApplyPlanetTransitBurnDamage(EntityUid target)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Heat"] = FixedPoint2.New(PlanetTransitHeatDamage);
        _damageable.TryChangeDamage(target, damage);
    }

    private void SetPlanetTransitBurnFixture(EntityUid uid, ThrusterComponent component, bool enabled)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body))
            return;

        _fixtureSystem.DestroyFixture(uid, BurnFixture, body: body);
        if (!component.IsOn || component.BurnPoly.Count == 0)
            return;

        var shape = new PolygonShape();
        shape.Set(enabled ? PlanetTransitBurnPoly : component.BurnPoly);
        _fixtureSystem.TryCreateFixture(uid,
            shape,
            BurnFixture,
            hard: false,
            collisionLayer: (int) CollisionGroup.FullTileMask,
            body: body);
    }
}
