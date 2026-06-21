using Content.Server.Station.Systems;
using Content.Shared.ArtilleryDetection;
using Content.Shared.ArtilleryDetection.Components;
using Content.Shared.ArtilleryDetection.Systems;
using Robust.Server.GameObjects;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Map;
using System;
using System.Numerics;

namespace Content.Server.ArtilleryDetection.Systems;

/// <summary>
/// Server-side system for detecting and logging artillery fire.
/// </summary>
public sealed class ArtilleryDetectionSystem : SharedArtilleryDetectionSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    /// <summary>
    /// Dictionary of pending fire detections with their scheduled times.
    /// Format: (detectorId, fireEvent, scheduledTime)
    /// </summary>
    private List<(EntityUid DetectorId, ArtilleryFireEvent Event, float ScheduledTime)> _pendingDetections = new();

    /// <summary>
    /// Counter for generating local sequential IDs for events.
    /// </summary>
    private int _localEventIdCounter = 0;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = (float)_gameTiming.CurTime.TotalSeconds;

        for (int i = _pendingDetections.Count - 1; i >= 0; i--)
        {
            var (detectorId, fireEvent, scheduledTime) = _pendingDetections[i];

            if (currentTime < scheduledTime)
                continue;

            if (!Exists(detectorId))
            {
                _pendingDetections.RemoveAt(i);
                continue;
            }

            RegisterFireEvent(detectorId, fireEvent);

            var query = EntityQueryEnumerator<ArtilleryDetectionConsoleComponent>();
            while (query.MoveNext(out var consoleUid, out _))
            {
                UpdateConsoleUi(consoleUid);
            }

            _pendingDetections.RemoveAt(i);
        }
    }

    /// <summary>
    /// Called when artillery (such as a mortar) fires to detect it.
    /// </summary>
    public void OnArtilleryFired(MapCoordinates artilleryPosition, string weaponType, TimeSpan detectionTime, string artilleryType = "Unknown", string projectileType = "Unknown")
    {
        var mapId = artilleryPosition.MapId;

        var detectorQuery = EntityQueryEnumerator<ArtilleryDetectorComponent>();

        while (detectorQuery.MoveNext(out var detectorUid, out var detector))
        {
            var detectorCoords = _transformSystem.GetMapCoordinates(detectorUid);

            if (detectorCoords.MapId != mapId)
                continue;

            var distance = Vector2.Distance(artilleryPosition.Position, detectorCoords.Position);

            if (distance > detector.DetectionRadius)
                continue;

            var offsetX = (float)(_random.NextGaussian() - 0.5f) * detector.AccuracyX * 2f;
            var offsetY = (float)(_random.NextGaussian() - 0.5f) * detector.AccuracyY * 2f;

            var detectedCoords = new Vector2(
                artilleryPosition.Position.X + offsetX,
                artilleryPosition.Position.Y + offsetY
            );

            var filteredArtilleryType = detector.ShowArtilleryType ? artilleryType : "Unknown";
            var filteredProjectileType = detector.ShowProjectileType ? projectileType : "Unknown";

            _localEventIdCounter++;
            var fireEvent = new ArtilleryFireEvent(
                coordinates: detectedCoords,
                weaponType: weaponType,
                detectionTime: detectionTime,
                artilleryType: filteredArtilleryType,
                projectileType: filteredProjectileType,
                localId: _localEventIdCounter
            );

            var scheduledTime = (float)_gameTiming.CurTime.TotalSeconds + detector.DetectionDelay;
            _pendingDetections.Add((detectorUid, fireEvent, scheduledTime));
        }
    }

    public ArtilleryDetectionConsoleState BuildConsoleState(EntityUid consoleUid)
    {
        var state = new ArtilleryDetectionConsoleState();

        if (!EntityManager.TryGetComponent<DeviceNetworkComponent>(consoleUid, out var consoleNet) ||
            !EntityManager.TryGetComponent<DeviceListComponent>(consoleUid, out var deviceList) ||
            !_deviceNetwork.IsDeviceConnected(consoleUid, consoleNet))
        {
            return state;
        }

        foreach (var detectorUid in deviceList.Devices)
        {
            if (!HasComp<ArtilleryDetectorComponent>(detectorUid))
                continue;

            if (!TryComp(detectorUid, out DeviceNetworkComponent? detectorNet))
                continue;

            if (!_deviceNetwork.IsDeviceConnected(detectorUid, detectorNet))
                continue;

            if (detectorNet.DeviceNetId != consoleNet.DeviceNetId ||
                detectorNet.ReceiveFrequency != consoleNet.ReceiveFrequency ||
                detectorNet.TransmitFrequency != consoleNet.TransmitFrequency)
                continue;

            state.ConnectedSystems.Add(Name(detectorUid));

            if (DetectorEvents.TryGetValue(detectorUid, out var events))
                state.Events.AddRange(events);
        }

        state.ConnectedSystems.Sort(StringComparer.Ordinal);
        state.Events.Sort((a, b) => b.DetectionTime.CompareTo(a.DetectionTime));
        return state;
    }

    /// <summary>
    /// Updates the UI for an artillery detection console.
    /// </summary>
    private void UpdateConsoleUi(EntityUid consoleUid)
    {
        if (!TryComp<ArtilleryDetectionConsoleComponent>(consoleUid, out _))
            return;

        var state = BuildConsoleState(consoleUid);

        _ui.SetUiState(consoleUid, ArtilleryDetectionConsoleUiKey.Key, state);
    }

    /// <summary>
    /// Called when a console requests to delete a fire event.
    /// </summary>
    public void DeleteFireEvent(Guid eventId)
    {
        foreach (var (_, events) in DetectorEvents)
        {
            events.RemoveAll(e => e.Id == eventId);
        }
    }
}
