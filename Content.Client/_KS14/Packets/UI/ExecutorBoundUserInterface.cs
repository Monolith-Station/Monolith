using System.Linq;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared._KS14.Packets.Widgets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._KS14.Packets.UI
{
    public sealed partial class ExecutorBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private ExecutorMenu? _menu;

        public ExecutorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            IoCManager.InjectDependencies(this);
        }

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindow<ExecutorMenu>();

            _menu.OnExecutionButton += OnExecute;
            _menu.OnLoadButton += OnLoad;
            _menu.OnTerminateButton += OnTerminate;

            _menu.Input.OnMouseExited += OnInputExit;
        }

        protected override void Dispose(bool disposing)
        {
            if (_menu is not { } menu)
                return;

            SaveCode();
            menu.ExecutorLog.TextRope = Rope.Leaf.Empty;

            for (int i = menu.ExecutorTabs.Children.Count() - 1; i >= 0; i--)
            {
                var child = menu.ExecutorTabs.Children[i];
                if (child.Name != "Executor")
                    menu.ExecutorTabs.RemoveChild(child);
            }

            base.Dispose(disposing);
        }

        private void OnInputExit(GUIMouseHoverEventArgs args)
        {
            SaveCode();
        }

        private void OnExecute()
        {
            SendMessage(new StartExecutionMessage(Rope.Collapse(_menu?.Input.TextRope ?? Rope.Leaf.Empty)));
        }

        private void OnLoad()
        {
            SendMessage(new ExecutorRequestCallsInfoMessage());
        }

        private void OnTerminate()
        {
            SendMessage(new TerminateExecutorMessage());
        }


        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            if (state is not ExecutorBoundUserInterfaceState rState)
                return;

            _menu?.UpdateState(rState);

            foreach (var widget in rState.Widgets)
            {
                InitWidget(widget);
            }
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_menu == null)
                return;

            switch (message)
            {
                case LogExecutorMessage cast:
                    _menu.WriteLog(cast.Log);
                    break;
                case ExecutorRequestCallsInfoMessage info:
                    _menu.UpdateDefinitions(info.Calls);
                    break;
                case WidgetSendMessage widget:
                    InitWidget(widget.Widget);
                    break;
                case WidgetUpdateElementMessage update:
                    UpdateWidgetElement(update.Element);
                    break;
            }
        }

        private void SaveCode()
        {
            SendMessage(new SaveExecutorCommandMessage(Rope.Collapse(_menu?.Input.TextRope ?? Rope.Leaf.Empty)));
        }
    }
}
