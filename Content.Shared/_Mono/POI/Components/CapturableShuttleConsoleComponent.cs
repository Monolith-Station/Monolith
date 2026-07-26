using Content.Shared._Mono.POI.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.POI.Components;

/// <summary>
/// Component for a capturable POI console.
/// Stores configuration and the currently inserted ID card.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CapturableShuttleConsoleSystem))]
public sealed partial class CapturableShuttleConsoleComponent : Component
{
    /// <summary>
    /// Time required to complete a capture in seconds.
    /// </summary>
    [DataField]
    public float CaptureTime = 6f;

    /// <summary>
    /// Percentage interval between capture progress announcements.
    /// </summary>
    [DataField]
    public int BroadcastInterval = 2;

    /// <summary>
    /// Name of the ID card container slot.
    /// Must match the slot name in the YAML.
    /// </summary>
    [DataField]
    public string IdSlot = "id_slot";

    /// <summary>
    /// The ID card currently inserted into the console.
    /// Set by EntInsertedIntoContainerMessage and
    /// cleared by EntRemovedFromContainerMessage.
    /// </summary>
    public EntityUid? InsertedId;
}