using Content.Server.Radio.EntitySystems;
using Content.Shared._Mono.POI.Components;
using Content.Shared.Access.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Popups;
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


    private TimeSpan _nextUpdate;


    public override void Initialize()
    {
        base.Initialize();

        _nextUpdate = _gameTiming.CurTime;
    }


    public override void Update(float frameTime)
    {
        // Only process captures once per second
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
        // Capture no longer has an owner
        if (capture.CapturingEntity == null)
        {
            CancelCapture(uid);
            return;
        }


        // Ensure this grid is capturable
        if (!TryComp<CapturablePOIComponent>(uid, out var poi))
        {
            CancelCapture(uid);
            return;
        }


        // First capture announcement
        if (capture.LastBroadcastPercent == -1)
        {
            var capturerName = "Unknown";

            if (capture.CapturingEntity is { } capturer)
                capturerName = Name(capturer);


            Logger.Info(
                $"POI Capture Started: {Name(uid)} by {capturerName}");


            _radio.SendRadioMessage(
                uid,
                $"{Name(uid)} is now being captured by {capturerName}. Stand by.",
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


        // Broadcast progress every 20%
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


        // Assign ownership deed to ID card
        if (capture.CapturingIdCard is { } idCard &&
            TryComp<IdCardComponent>(idCard, out var card))
        {
            var deed = EnsureComp<ShuttleDeedComponent>(idCard);


            deed.ShuttleUid = uid;
            deed.ShuttleName = Name(uid);
            deed.ShuttleOwner = card.FullName ?? "Unknown";
            deed.DeedHolder = idCard;
        }


        _radio.SendRadioMessage(
            uid,
            $"{Name(uid)} has been captured.",
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