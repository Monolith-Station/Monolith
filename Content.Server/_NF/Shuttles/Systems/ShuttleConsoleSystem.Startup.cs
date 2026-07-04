// Monolith - This file is licensed under AGPLv3
// Copyright (c) 2025 Monolith
// See AGPLv3.txt for details.

using Content.Server.DeviceLinking.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.DeviceLinking;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    /// <summary>
    /// Ensures the shuttle console has the necessary components for device linking
    /// </summary>
    private void EnsureDeviceLinkComponents(EntityUid uid, ShuttleConsoleComponent component)
    {
        // Check if the component exists
        var sourceComp = EnsureComp<DeviceLinkSourceComponent>(uid);
        _deviceLink.EnsureSourcePorts(uid, component.SourcePorts.ToArray());

        // Clear all signal states to prevent unwanted signals when establishing new connections
        foreach (var sourcePort in component.SourcePorts)
        {
            _deviceLink.ClearSignal((uid, sourceComp), sourcePort);
        }
    }
}
