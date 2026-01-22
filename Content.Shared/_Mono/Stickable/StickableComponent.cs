using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Stickable;

/// <summary>
/// Allows to stick this on entities by clicking with it in hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StickableComponent : Component
{
    /// <summary>
    /// Noise made when stickied.
    /// </summary>
    [DataField]
    public SoundSpecifier AttachSound = new SoundPathSpecifier("/Audio/Items/squeezebottle.ogg");
}
