using System.Collections.Generic;
using Content.Server._Mono.Speech.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Mono.Speech;
using Content.Shared._NF.Item;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Mobs;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mono.Speech.EntitySystems;

/// <summary>
/// Handles contextual speech triggered by entity events.
/// </summary>
public sealed class ContextualSpeechSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private readonly Dictionary<ProtoId<LocalizedDatasetPrototype>, LocalizedDatasetPrototype> _cachedDatasets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContextualSpeechComponent, SpeechTriggerEvent>(OnSpeechTrigger);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload);
        SubscribeLocalEvent<ContextualSpeechComponent, PickedUpEvent>(OnPickedUp);
        SubscribeLocalEvent<ContextualSpeechComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<ContextualSpeechComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnPickedUp(EntityUid uid, ContextualSpeechComponent component, PickedUpEvent args)
    {
        var speechEvent = new SpeechTriggerEvent(SpeechTrigger.PickedUp);
        RaiseLocalEvent(uid, ref speechEvent);
    }

    private void OnThrown(EntityUid uid, ContextualSpeechComponent component, ref ThrownEvent args)
    {
        var speechEvent = new SpeechTriggerEvent(SpeechTrigger.Thrown);
        RaiseLocalEvent(uid, ref speechEvent);
    }

    private void OnMobStateChanged(EntityUid uid, ContextualSpeechComponent component, MobStateChangedEvent args)
    {
        SpeechTrigger? trigger = args.NewMobState switch
        {
            MobState.Critical when args.OldMobState == MobState.Alive => SpeechTrigger.Critical,
            MobState.Dead => SpeechTrigger.Dead,
			MobState.Alive when args.OldMobState == MobState.Critical => SpeechTrigger.Revived,
            _ => null
        };

        if (trigger == null)
            return;

        var speechEvent = new SpeechTriggerEvent(trigger.Value);
        RaiseLocalEvent(uid, ref speechEvent);
    }

    private void OnSpeechTrigger(EntityUid uid, ContextualSpeechComponent component, ref SpeechTriggerEvent args)
    {
        if (!component.Triggers.TryGetValue(args.Trigger, out var trigger))
            return;

        if (!_random.Prob(trigger.SpeechChance))
            return;

        if (!_cachedDatasets.TryGetValue(trigger.Dataset, out var dataset))
        {
            if (!_prototypeManager.TryIndex(trigger.Dataset, out dataset))
                return;

            _cachedDatasets[trigger.Dataset] = dataset;
        }

        if (dataset.Values.Count == 0)
            return;

        var message = Loc.GetString(_random.Pick(dataset.Values));

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, hideChat: true, ignoreActionBlocker: true);
    }

    private void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        _cachedDatasets.Clear();
    }
}