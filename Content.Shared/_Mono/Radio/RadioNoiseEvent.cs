using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Radio;

/// <summary>
/// Network event sent to play radio noise sounds.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadioNoiseEvent : EntityEventArgs
{
    /// <summary>
    /// The headset that should play the radio noise.
    /// </summary>
    public NetEntity Entity { get; }

    /// <summary>
    /// The radio channel ID to determine which sound to play.
    /// </summary>
    public string ChannelId { get; }

    public RadioNoiseEvent(NetEntity entity, string channelId)
    {
        Entity = entity;
        ChannelId = channelId;
    }
}
