using Content.Shared.Chat;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Radio;

namespace Content.Server.Radio;

// Einstein Engines - Language begin
/// <summary>
/// <param name="OriginalChatMsg">The message to display when the speaker can understand "language"</param>
/// <param name="LanguageObfuscatedChatMsg">The message to display when the Speaker cannot understand "language"</param>
/// </summary>
[ByRefEvent]
public readonly record struct RadioReceiveEvent(
    EntityUid MessageSource,
    RadioChannelPrototype Channel,
    ChatMessage OriginalChatMsg,
    ChatMessage LanguageObfuscatedChatMsg,
    LanguagePrototype Language,
    EntityUid RadioSource
);
// Einstein Engines - Language end

/// <summary>
/// Use this event to cancel sending message per receiver
/// </summary>

[ByRefEvent]
public record struct RadioReceiveAttemptEvent(RadioChannelPrototype Channel, EntityUid RadioSource, EntityUid RadioReceiver)
{
    public readonly RadioChannelPrototype Channel = Channel;
    public readonly EntityUid RadioSource = RadioSource;
    public readonly EntityUid RadioReceiver = RadioReceiver;
    public bool Cancelled = false;
}

/// <summary>
/// Use this event to cancel sending message to every receiver
/// </summary>
[ByRefEvent]
public record struct RadioSendAttemptEvent(RadioChannelPrototype Channel, EntityUid RadioSource, int Frequency)
{
    public readonly RadioChannelPrototype Channel = Channel;
    public readonly EntityUid RadioSource = RadioSource;
    public readonly int Frequency = Frequency;
    public bool Cancelled = false;
}
