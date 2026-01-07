using Content.Server._Mono.Projectiles.TargetSeeking;
using Content.Server.Power.EntitySystems;
using Content.Shared.Power;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.TargetSeekingAlert;

/// <summary>
///     Handles logic for things that make alerts for entities with <see cref="TargetSeekingComponent"/>. 
/// </summary>

// I really wonder what might unexpectedly happen when you put a target-seeker-alerter on a planetmap, since those count as grids.
public sealed class TargetSeekerAlertSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;

    private EntityQuery<TargetSeekerAlertComponent> _alertQuery = new();

    public override void Initialize()
    {
        base.Initialize();

        _alertQuery = GetEntityQuery<TargetSeekerAlertComponent>();

        // I didn't make a subscription for ComponentStartup because i assume EntParentChanged gets raised on entities upon spawn.
        SubscribeLocalEvent<TargetSeekerAlertComponent, EntParentChangedMessage>(OnAlerterParentChanged);
        SubscribeLocalEvent<TargetSeekerAlertComponent, ComponentShutdown>(OnAlerterShutdown);
        SubscribeLocalEvent<TargetSeekerAlertComponent, PowerChangedEvent>(OnAlerterPowerChanged);

        SubscribeLocalEvent<TargetSeekerAlertComponent, TargetSeekerAlertStartedBeingTargetedEvent>(OnAlerterStartedBeingTargeted);
        SubscribeLocalEvent<TargetSeekerAlertComponent, TargetSeekerAlertDeactivatedEvent>(OnAlerterDeactivated);

        SubscribeLocalEvent<TargetSeekerAlertGridComponent, EntityStartedBeingSeekedTargetEvent>(OnGridStartingBeingTargeted);
        SubscribeLocalEvent<TargetSeekerAlertGridComponent, EntityStoppedBeingSeekedTargetEvent>(OnGridStoppedBeingTargeted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var alertGridEqe = EntityQueryEnumerator<TargetSeekerAlertGridComponent>();
        while (alertGridEqe.MoveNext(out var gridUid, out var alertGridComponent))
        {
            var gridTransform = Transform(gridUid);
            var closestSeekerDistance = float.MaxValue;

            foreach (var (_, seekerComponent, seekerTransform) in alertGridComponent.CurrentSeekers)
            {
                if (!seekerComponent.ExposesTracking ||
                    !gridTransform.Coordinates.TryDistance(EntityManager, seekerTransform.Coordinates, out var seekerDistance))
                    continue;

                if (seekerDistance < closestSeekerDistance)
                    closestSeekerDistance = seekerDistance;
            }

            foreach (var alertEntity in alertGridComponent.ActiveAlerters)
                UpdateActiveAlerter(alertEntity, closestSeekerDistance);
        }
    }

    private void OnAlerterPowerChanged(Entity<TargetSeekerAlertComponent> alertEntity, ref PowerChangedEvent args)
    {
        if (Transform(alertEntity).GridUid is not { } alertGridUid)
            return;

        if (args.Powered)
        {
            // This event doesn't get raised on spawn so we're fine.
            AddAlerterToGrid(alertGridUid, alertEntity);
        }
        else
        {
            if (TryComp<TargetSeekerAlertGridComponent>(alertGridUid, out var alertGridComponent))
                RemoveAlerterFromGrid((alertGridUid, alertGridComponent), alertEntity);
        }
    }

    private void UpdateActiveAlerter(Entity<TargetSeekerAlertComponent> alertEntity, float closestSeekerDistance)
    {
        SoundSpecifier? bestSound = null;
        float? newSoundKey = null;

        foreach (var alertSetting in alertEntity.Comp.DistanceAlertSettings)
        {
            if (alertSetting.MaximumDistance < closestSeekerDistance)
                continue;

            bestSound = alertSetting.Sound;
            newSoundKey = alertSetting.MaximumDistance;

            break;
        }

        if ((bestSound == null ||
            newSoundKey == null) ^
            alertEntity.Comp.ActiveAlertSoundKey == newSoundKey)
            return;

        alertEntity.Comp.ActiveAlertSoundKey = newSoundKey;
        if (alertEntity.Comp.Audio is { } currentAlertAudio)
            _audioSystem.Stop(currentAlertAudio);

        // Entity<T> isn't real
        // the reason ambient audio system isn't used is because it can get muted by player via settings
        var audioTuple = _audioSystem.PlayPvs(bestSound, alertEntity);
        if (audioTuple != null)
            alertEntity.Comp.Audio = audioTuple.Value.Entity;
    }

    private void OnAlerterStartedBeingTargeted(Entity<TargetSeekerAlertComponent> alertEntity, ref TargetSeekerAlertStartedBeingTargetedEvent args)
    {
        if (alertEntity.Comp.TargetGainSound is { } gainSound)
            _audioSystem.PlayPvs(gainSound, alertEntity.Owner);
    }

    private void OnAlerterDeactivated(Entity<TargetSeekerAlertComponent> alertEntity, ref TargetSeekerAlertDeactivatedEvent args)
    {
        if (alertEntity.Comp.Audio is { } alertAudio)
            _audioSystem.Stop(alertAudio);

        alertEntity.Comp.ActiveAlertSoundKey = null;
    }

    /// <summary>
    ///     adds an entity with <see cref="TargetSeekerAlertComponent"/> to a grid's list of alerters,
    ///         and ensures the grid's <see cref="TargetSeekerAlertGridComponent"/>. Raises associated
    ///         events on the grid.
    /// </summary>
    private void AddAlerterToGrid(EntityUid gridUid, EntityUid alertUid)
    {
        var alertGridComponent = EnsureComp<TargetSeekerAlertGridComponent>(gridUid);
        alertGridComponent.Alerters.Add(alertUid);

        var alerterActivatedEvent = new TargetSeekerAlertActivatedEvent();
        RaiseLocalEvent(alertUid, ref alerterActivatedEvent);
    }

    /// <summary>
    ///     Removes an entity with <see cref="TargetSeekerAlertComponent"/> from a grid's list of alerters,
    ///         and removes the grid's <see cref="TargetSeekerAlertGridComponent"/> if no more of
    ///         such entities are left specified as alerters on it. Raises associated events on the grid.
    /// </summary>
    private void RemoveAlerterFromGrid(Entity<TargetSeekerAlertGridComponent?> gridEntity, Entity<TargetSeekerAlertComponent> alertEntity)
    {
        if (!Resolve(gridEntity, ref gridEntity.Comp, logMissing: false))
            return;

        gridEntity.Comp.Alerters.Remove(alertEntity);
        gridEntity.Comp.ActiveAlerters.Remove(alertEntity);

        var alerterDeactivatedEvent = new TargetSeekerAlertDeactivatedEvent();
        RaiseLocalEvent(alertEntity, ref alerterDeactivatedEvent);

        if (gridEntity.Comp.Alerters.Count == 0)
        {
            RemComp(gridEntity, gridEntity.Comp);
            return;
        }
    }

    private void OnAlerterParentChanged(Entity<TargetSeekerAlertComponent> alertEntity, ref EntParentChangedMessage args)
    {
        if (!_powerReceiverSystem.IsPowered(alertEntity.Owner))
            return;

        var alertTransform = args.Transform;
        if (alertTransform.GridUid is not { } alertGridUid)
            return;

        AddAlerterToGrid(alertGridUid, alertEntity);

        // remove it from old parent if it was a grid, and if necessary
        if (TryComp<MapGridComponent>(args.OldParent, out _) &&
            TryComp<TargetSeekerAlertGridComponent>(alertGridUid, out var alertGridComponent))
            RemoveAlerterFromGrid((alertGridUid, alertGridComponent), alertEntity);
    }

    private void OnAlerterShutdown(Entity<TargetSeekerAlertComponent> alertEntity, ref ComponentShutdown args)
    {
        var alertTransform = Transform(alertEntity);

        if (alertTransform.GridUid is not { } alertGridUid ||
            !TryComp<TargetSeekerAlertGridComponent>(alertGridUid, out var alertGridComponent))
            return;

        RemoveAlerterFromGrid((alertGridUid, alertGridComponent), alertEntity);
    }

    private void OnGridStartingBeingTargeted(Entity<TargetSeekerAlertGridComponent> gridEntity, ref EntityStartedBeingSeekedTargetEvent args)
    {
        var wasAlreadyActive = gridEntity.Comp.CurrentSeekers.Count > 0;
        gridEntity.Comp.CurrentSeekers.Add(args.Seeker);

        foreach (var alertUid in gridEntity.Comp.Alerters)
        {
            if (!_alertQuery.TryGetComponent(alertUid, out var alertComponent))
                continue;

            Entity<TargetSeekerAlertComponent> alerterEntity = (alertUid, alertComponent);
            if (gridEntity.Comp.ActiveAlerters.Add(alerterEntity))
            {
                var alerterActivatedEvent = new TargetSeekerAlertActivatedEvent();
                RaiseLocalEvent(alerterEntity, ref alerterActivatedEvent);
            }

            var alerterTargetedEvent = new TargetSeekerAlertStartedBeingTargetedEvent(args.Seeker, wasAlreadyActive);
            RaiseLocalEvent(alerterEntity, ref alerterTargetedEvent);
        }
    }

    private void OnGridStoppedBeingTargeted(Entity<TargetSeekerAlertGridComponent> gridEntity, ref EntityStoppedBeingSeekedTargetEvent args)
    {
        gridEntity.Comp.CurrentSeekers.Remove(args.Seeker);
        var stillTargeted = gridEntity.Comp.CurrentSeekers.Count > 0;

        var alerterUntargetedEvent = new TargetSeekerAlertStoppedBeingTargetedEvent(args.Seeker, stillTargeted);

        foreach (var activeAlertEntity in gridEntity.Comp.ActiveAlerters)
        {
            RaiseLocalEvent(activeAlertEntity, ref alerterUntargetedEvent);
            if (!stillTargeted)
            {
                var alerterDeactivatedEvent = new TargetSeekerAlertDeactivatedEvent();
                RaiseLocalEvent(activeAlertEntity, ref alerterDeactivatedEvent);
            }
        }

        if (!stillTargeted)
            gridEntity.Comp.ActiveAlerters.Clear();
    }
}
