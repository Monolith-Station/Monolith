using Content.Shared.ArtilleryDetection;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.ArtilleryDetection;

public sealed class ArtilleryDetectionConsoleBoundUserInterface : BoundUserInterface
{
    private ArtilleryDetectionConsoleWindow? _window;

    public ArtilleryDetectionConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new ArtilleryDetectionConsoleWindow(Owner);
        _window.OnDeleteEvent += eventId => SendMessage(new DeleteArtilleryFireEventMessage(eventId));
        _window.OnRefreshRequested += () => SendMessage(new RequestArtilleryFireEventsMessage());
        _window.OnClose += Close;

        var uiSys = EntMan.System<UserInterfaceSystem>();
        if (uiSys.TryGetPosition(Owner, UiKey, out var pos))
            _window.Open(pos);
        else
            _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ArtilleryDetectionConsoleState cast)
            return;

        _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Close();
    }
}
