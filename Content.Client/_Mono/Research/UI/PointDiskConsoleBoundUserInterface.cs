using Content.Shared._Mono.Research;
using Content.Shared.Research.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Research.UI
{
    public sealed class PointDiskConsoleBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private PointDiskConsoleMenu? _menu;

        public PointDiskConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindow<PointDiskConsoleMenu>();

            _menu.OnServerButtonPressed += () =>
            {
                SendMessage(new ConsoleServerSelectionMessage());
            };
            _menu.OnPrint1KButtonPressed += () =>
            {
                SendMessage(new PointDiskConsolePrint1KDiskMessage());
            };
            _menu.OnPrint5KButtonPressed += () =>
            {
                SendMessage(new PointDiskConsolePrint5KDiskMessage());
            };
            _menu.OnPrint10KButtonPressed += () =>
            {
                SendMessage(new PointDiskConsolePrint10KDiskMessage());
            };
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not PointDiskConsoleBoundUserInterfaceState msg)
                return;

            _menu?.Update(msg);
        }
    }
}
