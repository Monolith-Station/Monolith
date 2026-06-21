using Content.Shared.ArtilleryDetection.Components;

namespace Content.Shared.ArtilleryDetection.Systems;

/// <summary>
/// Shared base system for artillery detection.
/// </summary>
public abstract class SharedArtilleryDetectionSystem : EntitySystem
{
    /// <summary>
    /// Dictionary storing fire events per detector entity.
    /// </summary>
    public Dictionary<EntityUid, List<ArtilleryFireEvent>> DetectorEvents = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArtilleryDetectorComponent, ComponentShutdown>(OnDetectorShutdown);
    }

    private void OnDetectorShutdown(Entity<ArtilleryDetectorComponent> ent, ref ComponentShutdown args)
    {
        DetectorEvents.Remove(ent.Owner);
    }

    /// <summary>
    /// Registers a fire detection event.
    /// </summary>
    public void RegisterFireEvent(EntityUid detectorId, ArtilleryFireEvent fireEvent)
    {
        if (!DetectorEvents.ContainsKey(detectorId))
        {
            DetectorEvents[detectorId] = new List<ArtilleryFireEvent>();
        }

        DetectorEvents[detectorId].Add(fireEvent);
    }

    /// <summary>
    /// Gets all fire events from a detector.
    /// </summary>
    public List<ArtilleryFireEvent> GetFireEvents(EntityUid detectorId)
    {
        if (DetectorEvents.TryGetValue(detectorId, out var events))
        {
            return new List<ArtilleryFireEvent>(events);
        }

        return new List<ArtilleryFireEvent>();
    }

    /// <summary>
    /// Removes a fire event from the log.
    /// </summary>
    public void RemoveFireEvent(EntityUid detectorId, Guid eventId)
    {
        if (DetectorEvents.TryGetValue(detectorId, out var events))
        {
            events.RemoveAll(e => e.Id == eventId);
        }
    }

    /// <summary>
    /// Clears all events from a detector.
    /// </summary>
    public void ClearEvents(EntityUid detectorId)
    {
        if (DetectorEvents.ContainsKey(detectorId))
        {
            DetectorEvents[detectorId].Clear();
        }
    }
}
