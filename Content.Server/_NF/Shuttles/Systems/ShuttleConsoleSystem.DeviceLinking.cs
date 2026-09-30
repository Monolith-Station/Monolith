// Monolith - This file is licensed under AGPLv3
// Copyright (c) 2025 Monolith
// See AGPLv3.txt for details.

using Content.Server.DeviceLinking.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private DeviceLinkSystem _deviceLink = default!;

    /// <summary>
    /// Initialize event handlers for device linking related functionality
    /// </summary>
    private void InitializeDeviceLinking()
    {
        // Subscribe to the message sent from the UI when a port button is pressed
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ShuttlePortButtonPressedMessage>(OnShuttlePortButtonPressed);
        });
    }

    /// <summary>
    /// Handles when a network port button is pressed on the shuttle console UI
    /// </summary>
    private void OnShuttlePortButtonPressed(EntityUid uid, ShuttleConsoleComponent component, ShuttlePortButtonPressedMessage args)
    {
        // Send a signal through the device link system when a button is pressed
        _deviceLink.SendSignal(uid, args.SourcePort, true);
    }
}
