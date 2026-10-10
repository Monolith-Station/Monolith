using System.Collections.Generic;
using Content.Shared._Mono.Speech;
using Robust.Shared.GameObjects;

namespace Content.Server._Mono.Speech.Components;

[RegisterComponent]
[ComponentProtoName("contextualSpeech")]
public sealed partial class ContextualSpeechComponent : Component
{
    [DataField]
    public Dictionary<SpeechTrigger, ContextualSpeechTrigger> Triggers { get; private set; } = new();
}