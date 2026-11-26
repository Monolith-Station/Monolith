using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.SubFloor;
using Content.Shared._FarHorizons.StarSystem;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Planets.Shields;

public sealed partial class CESharedShieldGeneratorSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly Dictionary<(NetEntity Generator, EntityUid Map), EntityUid> _controls = new();

    public override void Update(float frameTime)
    {
        _controls.Clear();
        var beams = EntityQueryEnumerator<CEShieldBeamComponent, TransformComponent>();
        while (beams.MoveNext(out var uid, out var beam, out var xform))
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (xform.MapUid is not { } map || !_controls.TryAdd((beam.Generator, map), uid))
                PredictedQueueDel(uid);
        }

        var now = _timing.CurTime;
        var generators = EntityQueryEnumerator<CEShieldGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out var uid, out var comp, out var xform))
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            var generator = (uid, comp);
            if (comp.Stage == CEShieldGeneratorStage.Cooldown && now >= comp.CooldownEndsAt)
                SetStage(generator, CEShieldGeneratorStage.Charging);

            if (xform.MapUid is not { } map)
                continue;

            if (comp.Stage == CEShieldGeneratorStage.SpinningUp && now >= comp.SpinupEndsAt)
                SetStage(generator, CEShieldGeneratorStage.Active);

            if (comp.Stage != CEShieldGeneratorStage.Active)
                continue;

            var netId = GetNetEntity(uid);
            var origin = GetOrigin(generator);
            foreach (var target in GetControlMaps(map))
            {
                if (!_controls.Remove((netId, target), out var control))
                    SpawnControl(netId, comp, map, target, origin);
                else
                {
                    _transform.SetWorldPosition(control, origin);
                    var beam = Comp<CEShieldBeamComponent>(control);
                    if (beam.Source != (target == map))
                    {
                        beam.Source = target == map;
                        Dirty(control, beam);
                    }
                }
            }
        }

        foreach (var (key, control) in _controls)
        {
            // Beam controls are globally visible even when their projector is outside PVS.
            if (_net.IsServer || (TryGetEntity(key.Generator, out var owner) && HasComp<CEShieldGeneratorComponent>(owner)))
                PredictedQueueDel(control);
        }

        VaporizeBeamContents();
    }

    private void VaporizeBeamContents()
    {
        var query = EntityQueryEnumerator<CEShieldBeamComponent>();
        while (query.MoveNext(out var uid, out var beam))
        {
            if (EntityManager.IsQueuedForDeletion(uid) ||
                !TryGetEntity(beam.Generator, out var generator) || generator is not { } owner ||
                !TryComp<CEShieldGeneratorComponent>(owner, out var projector) ||
                projector.Stage != CEShieldGeneratorStage.Active)
                continue;

            foreach (var victim in _lookup.GetEntitiesInRange(uid, beam.Radius, LookupFlags.Uncontained))
            {
                if (victim != owner)
                    Vaporize(victim, uid, beam.Generator);
            }
        }
    }

    private void Vaporize(EntityUid victim, EntityUid beam, NetEntity generator)
    {
        if (TerminatingOrDeleted(victim) || EntityManager.IsQueuedForDeletion(victim)
            || HasComp<MapComponent>(victim) || HasComp<MapGridComponent>(victim)
            || HasComp<CEShieldBeamComponent>(victim)
            || HasComp<AudioComponent>(victim) || HasComp<SubFloorHideComponent>(victim))
            return;
        var ev = new CEShieldBeamVaporizingEvent(beam, generator);
        RaiseLocalEvent(victim, ref ev);
        if (ev.Handled)
            return;
        var children = new List<EntityUid>();
        var enumerator = Transform(victim).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            children.Add(child);
        foreach (var child in children)
            Vaporize(child, beam, generator);
        PredictedQueueDel(victim);
    }

    public void BeginSpinup(Entity<CEShieldGeneratorComponent> ent, TimeSpan now)
    {
        ent.Comp.SpinupEndsAt = now + ent.Comp.SpinupTime;
        SetStage(ent, CEShieldGeneratorStage.SpinningUp);
    }

    public void BeginCooldown(Entity<CEShieldGeneratorComponent> ent, TimeSpan now)
    {
        ent.Comp.CooldownEndsAt = now + ent.Comp.CooldownTime;
        SetStage(ent, CEShieldGeneratorStage.Cooldown);
    }

    public void SetStage(Entity<CEShieldGeneratorComponent> ent, CEShieldGeneratorStage stage)
    {
        var previous = ent.Comp.Stage;
        if (previous == stage)
            return;

        ent.Comp.Stage = stage;
        Dirty(ent);
        _appearance.SetData(ent, CEShieldGeneratorVisuals.Stage, stage);
        var ev = new CEShieldGeneratorStageChangedEvent(previous, stage);
        RaiseLocalEvent(ent, ref ev);

        if (stage == CEShieldGeneratorStage.Active && ent.Comp.Stage == stage)
            PlayActivation(ent);
    }

    private void PlayActivation(Entity<CEShieldGeneratorComponent> ent)
    {
        if (Transform(ent).MapUid is not { } source)
            return;

        if (_net.IsClient &&
            (!_timing.IsFirstTimePredicted || ent.Comp.SpinupEndsAt <= _timing.CurTime - _timing.TickPeriod))
            return;

        var ev = new CEShieldActivationEvent(GetNetEntity(ent.Owner), _timing.CurTick,
            GetNetCoordinates(new EntityCoordinates(source, _transform.GetWorldPosition(ent))),
            _audio.ResolveSound(ent.Comp.FireSound), ent.Comp.FireSound.Params,
            (float) ent.Comp.FireFlashDuration.TotalSeconds, ent.Comp.FireShakeDuration, ent.Comp.FireShakeAmplitude);

        if (_net.IsClient)
        {
            RaiseLocalEvent(new CEShieldPredictedActivationEvent(ev));
            return;
        }

        var planet = GetPlanet(source);
        RaiseNetworkEvent(ev, Filter.Empty().AddWhereAttachedEntity(viewer => IsOnPlanet(viewer, source, planet)));
    }

    private bool IsOnPlanet(EntityUid viewer, EntityUid source, EntityUid? planet) =>
        Transform(viewer).MapUid is { } map && (map == source || (planet != null && GetPlanet(map) == planet));

    private EntityUid? GetPlanet(EntityUid map) =>
        _levels.TryGetMapNetwork(map, out var network) && TryComp<PlanetSurfaceComponent>(network.Owner, out var surface)
            ? surface.Planet
            : null;

    private HashSet<EntityUid> GetControlMaps(EntityUid map)
    {
        var maps = new HashSet<EntityUid> { map };
        if (TryComp<CEZMapComponent>(map, out var level))
        {
            maps.UnionWith(_levels.GetAllMapsAbove((map, level)));
            var transits = EntityQueryEnumerator<CEZTransitMapComponent>();
            while (transits.MoveNext(out var transitMap, out var transit))
            {
                if (transit.LowerMap is { } lower && TryComp<CEZMapComponent>(lower, out var anchor)
                    && anchor.NetworkUid == level.NetworkUid && anchor.Depth >= level.Depth)
                    maps.Add(transitMap);
            }
        }
        return maps;
    }

    private Vector2 GetOrigin(Entity<CEShieldGeneratorComponent> generator) =>
        Vector2.Transform(generator.Comp.BeamOffset, _transform.GetWorldMatrix(generator));

    private void SpawnControl(NetEntity generator, CEShieldGeneratorComponent spec, EntityUid map,
        EntityUid target, Vector2 origin)
    {
        var uid = PredictedSpawnAttachedTo("CEShieldBeam", new EntityCoordinates(target, origin));
        var beam = Comp<CEShieldBeamComponent>(uid);
        beam.Generator = generator;
        beam.Source = target == map;
        beam.Overlay = spec.BeamOverlay;
        beam.Radius = spec.BeamRadius;
        Dirty(uid, beam);
    }
}

[ByRefEvent]
public readonly record struct CEShieldGeneratorStageChangedEvent(CEShieldGeneratorStage Previous, CEShieldGeneratorStage Current);
