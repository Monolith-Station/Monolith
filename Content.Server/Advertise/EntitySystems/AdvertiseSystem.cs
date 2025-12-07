using System.Collections.Generic; // Mono
using Content.Server.Advertise.Components;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Shared.Chat; // Einstein Engines - Languages
using Content.Shared.Dataset;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Advertise.EntitySystems;

// Mono - update delay replaced with priority queue
public sealed class AdvertiseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    // Mono
    private PriorityQueue<EntityUid, TimeSpan> _advertQueue = new();

    // Mono - cache dataset protos for performance reasons
    private Dictionary<ProtoId<LocalizedDatasetPrototype>, LocalizedDatasetPrototype> _cachedDatasets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<AdvertiseComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<ApcPowerReceiverComponent, AttemptAdvertiseEvent>(OnPowerReceiverAttemptAdvertiseEvent);
        SubscribeLocalEvent<VendingMachineComponent, AttemptAdvertiseEvent>(OnVendingAttemptAdvertiseEvent);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReload); // Mono
    }

    // Mono - has to be ComponentInit so it doesn't break when loading again after MapInit
    // Mono - ComponentInit component modifications is a nono, back to mapInit with a check
    private void OnMapInit(EntityUid uid, AdvertiseComponent advert, MapInitEvent args)
    {
        if (advert.NextAdvertisementTime == TimeSpan.Zero)
        {
            var prewarm = advert.Prewarm;
            RandomizeNextAdvertTime(uid, advert, prewarm);
        }
    }

    private void RandomizeNextAdvertTime(EntityUid uid, AdvertiseComponent advert, bool prewarm = false)
    {
        var minDuration = prewarm ? 0 : Math.Max(1, advert.MinimumWait);
        var maxDuration = Math.Max(minDuration, advert.MaximumWait);
        var waitDuration = TimeSpan.FromSeconds(_random.Next(minDuration, maxDuration));

        advert.NextAdvertisementTime = _gameTiming.CurTime + waitDuration;
        _advertQueue.Enqueue(uid, advert.NextAdvertisementTime);
    }

    // Mono
    private void OnProtoReload(PrototypesReloadedEventArgs ev)
    {
        _cachedDatasets.Clear();
    }

    public void SayAdvertisement(EntityUid uid, AdvertiseComponent? advert = null)
    {
        if (!Resolve(uid, ref advert))
            return;

        var attemptEvent = new AttemptAdvertiseEvent(uid);
        RaiseLocalEvent(uid, ref attemptEvent);
        if (attemptEvent.Cancelled)
            return;

        // Mono
        if (!_cachedDatasets.ContainsKey(advert.Pack))
        {
            if (!_prototypeManager.TryIndex(advert.Pack, out var advertisements))
                return;

            _cachedDatasets[advert.Pack] = advertisements;
        }

        // Mono
        var adverts = _cachedDatasets[advert.Pack];
        // TODO: investigate why TrySendInGameICMessage takes entire milliseconds
        _chat.TrySendInGameICMessage(uid, Loc.GetString(_random.Pick(adverts.Values)), InGameICChatType.Speak, hideChat: true);
    }

    public override void Update(float frameTime)
    {
        var i = 0;
        while (true)
        {
            i++;
            if (!_advertQueue.TryPeek(out var uid, out var time))
                break;

            // failsafe - something went wrong (evil admeme setting advertise delay to negative?) but don't freeze the server
            if (i > _advertQueue.Count)
                break;
                                                                                                     // seems like it has changed
            if (TerminatingOrDeleted(uid) || !TryComp<AdvertiseComponent>(uid, out var advertise) || advertise.NextAdvertisementTime != time)
            {
                _advertQueue.Dequeue();
                continue;
            }

            // it's a priority queue so everything after this will be later
            if (advertise.NextAdvertisementTime > _gameTiming.CurTime)
                break;

            _advertQueue.Dequeue();
            SayAdvertisement(uid, advertise);
            RandomizeNextAdvertTime(uid, advertise);
        }
    }


    private static void OnPowerReceiverAttemptAdvertiseEvent(EntityUid uid, ApcPowerReceiverComponent powerReceiver, ref AttemptAdvertiseEvent args)
    {
        args.Cancelled |= !powerReceiver.Powered;
    }

    private static void OnVendingAttemptAdvertiseEvent(EntityUid uid, VendingMachineComponent machine, ref AttemptAdvertiseEvent args)
    {
        args.Cancelled |= machine.Broken;
    }
}

[ByRefEvent]
public record struct AttemptAdvertiseEvent(EntityUid? Advertiser)
{
    public bool Cancelled = false;
}
