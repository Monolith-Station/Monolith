using Content.Shared._Mono.POI.Components;
using Content.Shared.Access.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.POI.Systems;

public sealed class CapturableShuttleConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CapturableShuttleConsoleComponent, EntInsertedIntoContainerMessage>(OnIdInserted);
        SubscribeLocalEvent<CapturableShuttleConsoleComponent, EntRemovedFromContainerMessage>(OnIdRemoved);

        SubscribeLocalEvent<CapturableShuttleConsoleComponent, GetVerbsEvent<Verb>>(AddCaptureVerb);
    }


    private void OnIdInserted(
        EntityUid uid,
        CapturableShuttleConsoleComponent component,
        EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.IdSlot)
            return;


        if (!HasComp<IdCardComponent>(args.Entity))
            return;


        component.InsertedId = args.Entity;
    }


    private void OnIdRemoved(
        EntityUid uid,
        CapturableShuttleConsoleComponent component,
        EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.IdSlot)
            return;


        component.InsertedId = null;
    }


    private void AddCaptureVerb(
        EntityUid uid,
        CapturableShuttleConsoleComponent component,
        GetVerbsEvent<Verb> args)
    {
        if (component.InsertedId == null)
            return;


        args.Verbs.Add(new Verb
        {
            Text = "Start Capture",

            Act = () =>
            {
                TryStartCapture(
                    uid,
                    component,
                    args.User);
            }
        });
    }


    private void TryStartCapture(
        EntityUid console,
        CapturableShuttleConsoleComponent component,
        EntityUid user)
    {
        if (component.InsertedId == null)
        {
            _popup.PopupClient(
                "Insert an ID card first.",
                console,
                user);

            return;
        }


        var consoleTransform = Transform(console);


        if (consoleTransform.GridUid == null)
        {
            _popup.PopupClient(
                "This console is not located on a valid grid.",
                console,
                user);

            return;
        }


        var grid = consoleTransform.GridUid.Value;


        var poi = EnsureComp<CapturablePOIComponent>(grid);


        if (TryComp<POICaptureComponent>(grid, out var existingCapture) &&
            existingCapture.CapturingEntity != null)
        {
            _popup.PopupClient(
                "This location is already being captured.",
                console,
                user);

            return;
        }


        var capture = EnsureComp<POICaptureComponent>(grid);


        // Person who pressed the capture button
        capture.CapturingEntity = user;
        capture.CapturingPlayerName = Name(user);


        // ID card in the console
        capture.CapturingIdCard = component.InsertedId.Value;


        // Capture timing
        capture.CaptureStart = _gameTiming.CurTime;
        capture.CaptureDuration = TimeSpan.FromSeconds(component.CaptureTime);


        // Force start announcement
        capture.LastBroadcastPercent = -1;


        // Store faction/company from inserted ID card
        if (TryComp<IdCardComponent>(component.InsertedId.Value, out var card))
        {
            capture.CapturingFaction = card.CompanyName.ToString();
        }
        else
        {
            capture.CapturingFaction = "None";
        }


        poi.CaptureProgress = 0;
        poi.IsBeingCaptured = true;


        Dirty(grid, poi);


        _popup.PopupClient(
            "POI capture started.",
            console,
            user);
    }
}