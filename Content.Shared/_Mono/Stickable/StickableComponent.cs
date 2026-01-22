using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Stickable;

/// <summary>
/// Allows to stick this on entities by clicking with it in hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StickableComponent : Component
{
    [DataField]
    public EntityUid? AttachedParent = null;

    /// <summary>
    /// Noise made when stickied.
    /// </summary>
    [DataField]
    public SoundSpecifier AttachSound = new SoundPathSpecifier("/Audio/Items/squeezebottle.ogg");

    /// <summary>
    /// If set, will spawn and attach a new entity instead of us.
    /// </summary>
    [DataField]
    public EntProtoId? ReuseProto = null;
}
