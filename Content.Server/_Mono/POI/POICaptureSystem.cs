using Content.Server.Radio.EntitySystems;
using Content.Shared._Mono.POI.Components;
using Content.Shared.Access.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.POI;

/// <summary>
/// Handles active POI capture progress.
/// Runs once per second and only checks active captures.
/// </summary>
public sealed class POICaptureSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;


    private TimeSpan _nextUpdate;


    public override void Initialize()
    {
        base.Initialize();

        _nextUpdate = _gameTiming.CurTime;
    }


    public override void Update(float frameTime)
    {
        if (_gameTiming.CurTime < _nextUpdate)
            return;


        _nextUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(1);


        var query = EntityQueryEnumerator<POICaptureComponent>();

        while (query.MoveNext(out var uid, out var capture))
        {
            TickCapture(uid, capture);
        }
    }


    private void TickCapture(
        EntityUid uid,
        POICaptureComponent capture)
    {
        if (capture.CapturingEntity == null)
        {
            CancelCapture(uid);
            return;
        }


        if (!TryComp<CapturablePOIComponent>(uid, out var poi))
        {
            CancelCapture(uid);
            return;
        }


        //
        // Initial capture announcement
        //
        if (capture.LastBroadcastPercent == -1)
        {
            var playerName = "Unknown";
            var factionName = "Unknown";


            if (capture.CapturingEntity is { } capturer)
                playerName = Name(capturer);


            if (capture.CapturingIdCard is { } idCard &&
                TryComp<IdCardComponent>(idCard, out var card))
            {
                if (_prototype.TryIndex(card.Faction, out var faction))
                    factionName = faction.Name;
                else
                    factionName = card.Faction;
            }


            _radio.SendRadioMessage(
                uid,
                $"{playerName} is capturing for {factionName} {Name(uid)}. Stand by.",
                "Traffic",
                uid);


            capture.LastBroadcastPercent = 0;
        }


        var elapsed =
            _gameTiming.CurTime - capture.CaptureStart;


        var progress =
            (float)(elapsed.TotalSeconds /
            capture.CaptureDuration.TotalSeconds) * 100f;


        poi.CaptureProgress = Math.Clamp(progress, 0f, 100f);
        poi.IsBeingCaptured = true;


        Dirty(uid, poi);


        //
        // Progress announcements every 20%
        //
        var broadcast =
            (int)(poi.CaptureProgress / 20) * 20;


        if (broadcast > capture.LastBroadcastPercent)
        {
            capture.LastBroadcastPercent = broadcast;


            _radio.SendRadioMessage(
                uid,
                $"{Name(uid)} capture progress: {broadcast}%.",
                "Traffic",
                uid);
        }


        if (poi.CaptureProgress >= 100f)
        {
            CompleteCapture(uid, poi, capture);
        }
    }


    private void CompleteCapture(
        EntityUid uid,
        CapturablePOIComponent poi,
        POICaptureComponent capture)
    {
        poi.OwnerFaction = "Rogue";

        poi.CaptureProgress = 0;
        poi.IsBeingCaptured = false;


        Dirty(uid, poi);


        var playerName = "Unknown";
        var factionName = "Unknown";


        if (capture.CapturingEntity is { } capturer)
            playerName = Name(capturer);


        if (capture.CapturingIdCard is { } idCard &&
            TryComp<IdCardComponent>(idCard, out var card))
        {
            if (_prototype.TryIndex(card.Faction, out var faction))
                factionName = faction.Name;
            else
                factionName = card.Faction;


            var deed = EnsureComp<ShuttleDeedComponent>(idCard);

            deed.ShuttleUid = uid;
            deed.ShuttleName = Name(uid);
            deed.ShuttleOwner = card.FullName ?? "Unknown";
            deed.DeedHolder = idCard;
        }


        _radio.SendRadioMessage(
            uid,
            $"{Name(uid)} has been captured by {playerName} for faction {factionName}.",
            "Traffic",
            uid);


        _popup.PopupEntity(
            $"{Name(uid)} capture complete. Ownership transferred.",
            uid);


        RemComp<POICaptureComponent>(uid);
    }


    private void CancelCapture(EntityUid uid)
    {
        if (TryComp<CapturablePOIComponent>(uid, out var poi))
        {
            poi.CaptureProgress = 0;
            poi.IsBeingCaptured = false;

            Dirty(uid, poi);
        }


        RemComp<POICaptureComponent>(uid);
    }
}