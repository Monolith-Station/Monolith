using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.Events;

/// <summary>
///     Unassign a vessel deed from an ID card without selling the ship.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsoleUnassignDeedMessage : BoundUserInterfaceMessage
{
    public ShipyardConsoleUnassignDeedMessage()
    {
    }
}