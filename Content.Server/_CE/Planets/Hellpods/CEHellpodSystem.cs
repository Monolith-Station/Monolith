using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Damage;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._Mono.Ships;
using Content.Shared.Body.Components;
using Content.Shared.DragDrop;
using Content.Shared.DoAfter;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CE.Planets.Hellpods;

public sealed partial class CEHellpodSystem : EntitySystem
{
    private const string ImpactExplosion = "CEHellpodImpact";
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CESharedZLevelsSystem _levels = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CEZLevelDamageSystem _fallDamage = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeConsole();
        SubscribeLocalEvent<CEHellpodHatchComponent, DragDropTargetEvent>(OnBoard);
        SubscribeLocalEvent<CEHellpodHatchComponent, CEHellpodBoardDoAfterEvent>(OnBoardComplete);
        SubscribeLocalEvent<BodyComponent, CEHellpodExitAlertEvent>(OnExit);
        SubscribeLocalEvent<BodyComponent, AtmosExposedGetAirEvent>(OnGetCabinAir);
        SubscribeLocalEvent<CEHellpodHatchComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CEHellpodComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<CEHellpodComponent, CEZLevelHitEvent>(OnLand, after: new[] { typeof(CEZLevelDamageSystem) });
        SubscribeLocalEvent<CEHellpodComponent, ShipShieldCrossedEvent>(OnShieldCrossed);
        SubscribeLocalEvent<CEHellpodRiderProtectionComponent, GetExplosionResistanceEvent>(OnImpactResistance);
    }

    private ContainerSlot Seat(EntityUid uid) => _containers.EnsureContainer<ContainerSlot>(uid, "occupant");

    private void OnGetCabinAir(Entity<BodyComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        var parent = args.Entity.Comp.ParentUid;
        if (args.Handled || !TryComp<CEHellpodComponent>(parent, out var pod) || !pod.InFlight ||
            Seat(parent).ContainedEntity != ent.Owner)
            return;

        if (pod.CabinAir == null)
        {
            const float volume = 250f;
            var air = new GasMixture(volume) { Temperature = Atmospherics.T20C };
            var cellRatio = volume / Atmospherics.CellVolume;
            air.SetMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard * cellRatio);
            air.SetMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard * cellRatio);
            pod.CabinAir = air;
        }

        args.Gas = pod.CabinAir;
        args.Handled = true;
    }

    private void OnBoard(Entity<CEHellpodHatchComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.User != args.Dragged || !HasComp<BodyComponent>(args.User)
            || !CanBoard(ent))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.EntryDelay,
            new CEHellpodBoardDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        });
    }

    private void OnBoardComplete(Entity<CEHellpodHatchComponent> ent, ref CEHellpodBoardDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !CanBoard(ent)
            || !HasComp<BodyComponent>(args.User) || !_containers.Insert(args.User, Seat(ent)))
            return;

        args.Handled = true;
        _alerts.ShowAlert(args.User, "CEHellpodHatch");
        _appearance.SetData(ent, CEHellpodVisuals.Open, false);
        ent.Comp.LaunchAt = null;
    }

    private void OnExit(Entity<BodyComponent> ent, ref CEHellpodExitAlertEvent args)
    {
        var hatch = Transform(ent).ParentUid;
        if (args.Handled || !HasComp<CEHellpodHatchComponent>(hatch) || Seat(hatch).ContainedEntity != ent.Owner)
            return;
        args.Handled = _containers.Remove(ent.Owner, Seat(hatch));
    }

    private void OnRemoved(Entity<CEHellpodHatchComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "occupant")
            return;
        _alerts.ClearAlert(args.Entity, "CEHellpodHatch");
        ent.Comp.LaunchAt = null;
    }

    private void OnRemoveAttempt(Entity<CEHellpodComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.InFlight && args.Container.ID == "occupant")
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateConsole();
        var query = EntityQueryEnumerator<CEHellpodHatchComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hatch, out var xform))
        {
            var occupied = Seat(uid).ContainedEntity;
            var dropMap = xform.MapUid.GetValueOrDefault();
            var planet = dropMap.IsValid() && IsPlanetaryDrop(dropMap);
            _appearance.SetData(uid, CEHellpodVisuals.Open, occupied == null &&
                (planet || xform.Anchored && IsBoarding(uid, xform.GridUid)));
            if (occupied is not { } occupant || !planet ||
                !TryGetDropStart(dropMap, out var lowerMap, out var height, out var inTransit))
            {
                hatch.LaunchAt = null;
                continue;
            }

            hatch.LaunchAt ??= _timing.CurTime + hatch.LaunchDelay;
            if (_timing.CurTime < hatch.LaunchAt.Value)
                continue;

            var pod = Spawn(hatch.Pod, xform.Coordinates);
            if (inTransit)
                _transform.SetMapCoordinates(pod, new MapCoordinates(_transform.GetWorldPosition(xform), Transform(lowerMap).MapID));
            else if (!_levels.TryMoveDown(pod))
            {
                QueueDel(pod);
                hatch.LaunchAt = null;
                continue;
            }
            else if (HasComp<CEZTransitMapComponent>(Transform(pod).MapUid))
                height = _levels.GetAbsoluteAltitude(uid) - _levels.GetAbsoluteAltitude(pod);

            if (!_containers.Insert(occupant, Seat(pod)))
            {
                QueueDel(pod);
                hatch.LaunchAt = null;
                continue;
            }

            var component = Comp<CEHellpodComponent>(pod);
            component.InFlight = true;
            Dirty(pod, component);
            _levels.SetZPosition(pod, height);
            _levels.SetZVelocity(pod, -component.DropSpeed);
        }
    }

    private bool CanBoard(EntityUid hatch)
    {
        var xform = Transform(hatch);
        return Seat(hatch).ContainedEntity == null &&
               (xform.MapUid is { } map && IsPlanetaryDrop(map) || xform.Anchored && IsBoarding(hatch, xform.GridUid));
    }

    private bool IsPlanetaryDrop(EntityUid map) => TryGetDropStart(map, out _, out _, out _);

    private bool TryGetDropStart(EntityUid map, out EntityUid lowerMap, out float height, out bool inTransit)
    {
        lowerMap = default;
        height = 1f;
        inTransit = TryComp<CEZTransitMapComponent>(map, out var transit);
        var anchor = transit?.LowerMap ?? map;
        if (!_levels.TryGetMapNetwork(anchor, out var network) ||
            !HasComp<PlanetSurfaceComponent>(network) &&
            !network.Comp.SortedZLevels.Any(level => HasComp<CEZGroundLayerComponent>(level)))
            return false;

        if (transit != null)
        {
            lowerMap = anchor;
            height = _levels.GetTransitProgress(transit);
            return true;
        }

        return _levels.TryMapDown(map, out _);
    }

    private void OnShieldCrossed(Entity<CEHellpodComponent> ent, ref ShipShieldCrossedEvent args)
    {
        if (ent.Comp.InFlight && ent.Comp.CrossedShields.Add(args.Shield))
            LoseIntegrity(ent.Comp, CEHellpodComponent.ShieldIntegrityLoss);
    }

    private static void LoseIntegrity(CEHellpodComponent pod, int amount) =>
        pod.Integrity = Math.Max(0, pod.Integrity - amount);

    private void OnLand(Entity<CEHellpodComponent> ent, ref CEZLevelHitEvent args) => FinishPod(ent);

    private void FinishPod(Entity<CEHellpodComponent> ent)
    {
        if (!ent.Comp.InFlight)
            return;
        ent.Comp.InFlight = false;
        Dirty(ent);
        _fallDamage.TryGetLandingTile(ent, out var tile);
        var damagingImpact = tile?.FallDamageMultiplier != 0f;
        var rider = Seat(ent).ContainedEntity;
        if (rider is { } occupant)
        {
            EnsureComp<CEHellpodRiderProtectionComponent>(occupant).Pod = ent.Owner;
            _containers.Remove(occupant, Seat(ent));
            if (HasComp<CEZPhysicsComponent>(occupant))
                _levels.SetZVelocity(occupant, 0f);
            if (damagingImpact && ent.Comp.Integrity < CEHellpodComponent.FullIntegrity)
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Blunt"] = FixedPoint2.New(CEHellpodComponent.FullIntegrity - ent.Comp.Integrity);
                _damageable.TryChangeDamage(occupant, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            }
        }
        if (tile?.LandingSound is { } sound)
            _audio.PlayPvs(sound, ent);
        if (damagingImpact)
            _explosions.QueueExplosion(ent, ImpactExplosion,
                ent.Comp.ImpactIntensity, 3f, 5f, canCreateVacuum: false);
        QueueDel(ent);
    }

    private static void OnImpactResistance(Entity<CEHellpodRiderProtectionComponent> ent, ref GetExplosionResistanceEvent args)
    {
        if (args.ExplosionPrototype == ImpactExplosion && args.Cause == ent.Comp.Pod)
            args.DamageCoefficient = 0f;
    }
}

[RegisterComponent]
public sealed partial class CEHellpodRiderProtectionComponent : Component
{
    public EntityUid Pod;
}
