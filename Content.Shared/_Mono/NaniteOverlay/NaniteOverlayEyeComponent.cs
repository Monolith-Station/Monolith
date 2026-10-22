using Robust.Shared.GameStates;

namespace Content.Shared._Mono.NaniteOverlay;

/// <summary>
/// Added by server to client to tell it that it can now start showing repair ghosts.
/// Needed because without this client will start showing missing entities immediately but that will also show underfloor entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NaniteOverlayEyeComponent : Component
{
    [DataField]
    public int Count = 0;

    /// <summary>
    /// How far away should the repairable entities be colored?
    /// </summary>
    [DataField]
    public int Range = 5;
}
