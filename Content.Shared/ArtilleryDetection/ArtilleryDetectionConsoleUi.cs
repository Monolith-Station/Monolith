using Robust.Shared.Serialization;

namespace Content.Shared.ArtilleryDetection;

[Serializable, NetSerializable]
public enum ArtilleryDetectionConsoleUiKey : byte
{
    Key
}

/// <summary>
/// UI state for the artillery detection console.
/// </summary>
[Serializable, NetSerializable]
public sealed class ArtilleryDetectionConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// List of detected artillery fire events.
    /// </summary>
    public List<ArtilleryFireEvent> Events = new();

    /// <summary>
    /// Names of detector systems currently connected to this console.
    /// </summary>
    public List<string> ConnectedSystems = new();
}

/// <summary>
/// Message to request deletion of a fire event from the log.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeleteArtilleryFireEventMessage : BoundUserInterfaceMessage
{
    public Guid EventId { get; set; }

    public DeleteArtilleryFireEventMessage() { }
    public DeleteArtilleryFireEventMessage(Guid eventId) => EventId = eventId;
}

/// <summary>
/// Message to refresh the event list from the detector.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestArtilleryFireEventsMessage : BoundUserInterfaceMessage
{
}
