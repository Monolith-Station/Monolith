using Robust.Client.Graphics;
using Robust.Shared.GameStates;

namespace Content.Client.Shuttles;

/// <summary>
/// A component that emits a visible exhaust plume if the entity is an active thruster.
/// Managed by <see cref="ThrusterSystem"/>
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ThrusterSystem))]
public sealed partial class ThrusterComponent : Component
{
    internal readonly Dictionary<int, ThrusterLayerVisual> TransitLayers = new();
    internal bool LayersCached;
}

internal readonly record struct ThrusterLayerVisual(Color Color, RSI? Rsi, RSI.StateId State);
