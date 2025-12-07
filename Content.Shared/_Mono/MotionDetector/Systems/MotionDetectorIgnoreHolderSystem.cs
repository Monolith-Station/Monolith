using Content.Shared._Mono.MotionDetector.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Hands.Components;
using Content.Shared.ProximityDetection;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Mono.MotionDetector.Systems;

/// <summary>
/// Prevents motion detectors from detecting the holder.
/// </summary>
public sealed class MotionDetectorIgnoreHolderSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, ProximityDetectionAttemptEvent>(OnProximityDetectionAttempt);
    }

    private void OnProximityDetectionAttempt(EntityUid targetEntity, MetaDataComponent component, ref ProximityDetectionAttemptEvent args)
    {
        var detectorEntity = args.Detector.Owner;

        if (!HasComp<MotionDetectorIgnoreHolderComponent>(detectorEntity))
            return;

        var holder = GetEntityHolder(detectorEntity);

        if (holder != null && holder == targetEntity)
        {
            args.Cancel = true;
            return;
        }

        if (holder != null && ShouldIgnoreCompanyMember(holder.Value, targetEntity))
        {
            args.Cancel = true;
            return;
        }

        if (!IsEntityMoving(targetEntity))
        {
            args.Cancel = true;
        }
    }

    /// <summary>
    /// Checks if the target entity is actually moving.
    /// </summary>
    private bool IsEntityMoving(EntityUid entity)
    {
        if (!TryComp<PhysicsComponent>(entity, out var physics))
            return false;

        const float minVelocityThreshold = 0.1f;
        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();

        return speed > minVelocityThreshold;
    }

    /// <summary>
    /// Checks if the target entity should be ignored based on company affiliation.
    /// </summary>
    private bool ShouldIgnoreCompanyMember(EntityUid holder, EntityUid target)
    {
        if (!TryComp<CompanyComponent>(holder, out var holderCompany))
            return false;

        if (!TryComp<CompanyComponent>(target, out var targetCompany))
            return false;

        if (string.IsNullOrEmpty(holderCompany.CompanyName) ||
            string.IsNullOrEmpty(targetCompany.CompanyName) ||
            holderCompany.CompanyName == "None" ||
            targetCompany.CompanyName == "None")
        {
            return false;
        }

        return holderCompany.CompanyName == targetCompany.CompanyName;
    }

    /// <summary>
    /// Finds the holder.
    /// </summary>
    private EntityUid? GetEntityHolder(EntityUid item)
    {
        if (!_containerSystem.TryGetContainingContainer((item, null, null), out var container))
            return null;

        var containerOwner = container.Owner;

        if (HasComp<HandsComponent>(containerOwner))
        {
            return containerOwner;
        }

        return GetEntityHolder(containerOwner);
    }
}
