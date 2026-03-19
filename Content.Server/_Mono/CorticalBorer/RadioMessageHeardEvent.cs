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
public record struct RadioMessageHeardEvent(
    EntityUid Headset,
    MsgChatMessage Msg,
    RadioChannelPrototype Channel
);
