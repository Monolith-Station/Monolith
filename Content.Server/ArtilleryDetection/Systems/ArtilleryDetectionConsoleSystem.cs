using Content.Server.ArtilleryDetection.Systems;
using Content.Shared.ArtilleryDetection;
using Content.Shared.ArtilleryDetection.Components;
using Content.Shared.DeviceNetwork.Systems;
using Robust.Server.GameObjects;
using Content.Shared.UserInterface;

namespace Content.Server.ArtilleryDetection.Systems;

/// <summary>
/// Server-side console system for displaying artillery fire detection logs.
/// </summary>
public sealed class ArtilleryDetectionConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ArtilleryDetectionSystem _detection = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<ArtilleryDetectionConsoleComponent>(ArtilleryDetectionConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<DeleteArtilleryFireEventMessage>(OnDeleteEvent);
            subs.Event<RequestArtilleryFireEventsMessage>(OnRequestEvents);
        });

        SubscribeLocalEvent<ArtilleryDetectionConsoleComponent, DeviceListUpdateEvent>(OnDeviceListUpdate);
    }

    private void OnConsoleOpened(Entity<ArtilleryDetectionConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsoleUi(ent.Owner);
    }

    private void OnDeleteEvent(Entity<ArtilleryDetectionConsoleComponent> ent, ref DeleteArtilleryFireEventMessage msg)
    {
        _detection.DeleteFireEvent(msg.EventId);
        UpdateConsoleUi(ent.Owner);
    }

    private void OnRequestEvents(Entity<ArtilleryDetectionConsoleComponent> ent, ref RequestArtilleryFireEventsMessage msg)
    {
        UpdateConsoleUi(ent.Owner);
    }

    private void OnDeviceListUpdate(Entity<ArtilleryDetectionConsoleComponent> ent, ref DeviceListUpdateEvent args)
    {
        UpdateConsoleUi(ent.Owner);
    }

    /// <summary>
    /// Updates the console UI with current fire events.
    /// </summary>
    private void UpdateConsoleUi(EntityUid consoleUid)
    {
        if (!TryComp<ArtilleryDetectionConsoleComponent>(consoleUid, out _))
            return;

        var state = _detection.BuildConsoleState(consoleUid);

        _ui.SetUiState(consoleUid, ArtilleryDetectionConsoleUiKey.Key, state);
    }
}
