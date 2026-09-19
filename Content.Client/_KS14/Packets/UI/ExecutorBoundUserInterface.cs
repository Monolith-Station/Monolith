using Content.Shared._KS14.Packets.BUI;
using Robust.Client.UserInterface;
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
            _menu.OnSaveButton += OnSave;
            _menu.OnLoadButton += OnLoad;
            _menu.OnTerminateButton += OnTerminate;
            _menu.OnSendInputButton += OnSendInput;
        }

        protected override void Dispose(bool disposing)
        {
            _menu?.ExecutorLog.Text = String.Empty;
            base.Dispose(disposing);
        }

        private void OnSave()
        {
            SendMessage(new SaveExecutorCommandMessage(Rope.Collapse(_menu?.Input.TextRope ?? Rope.Leaf.Empty)));
        }

        private void OnExecute()
        {
            SendMessage(new StartExecutionMessage());
        }

        private void OnLoad()
        {
            SendMessage(new ReloadModulesMessage());
        }

        private void OnTerminate()
        {
            SendMessage(new TerminateExecutorMessage());
        }

        private void OnSendInput()
        {
            SendMessage(new InputExecutorMessage(Rope.Collapse(_menu?.DataInput.TextRope ?? Rope.Leaf.Empty)));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            if (state is not ExecutorBoundUserInterfaceState rState)
                return;

            _menu?.UpdateState(rState);
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_menu == null)
                return;

            if (message is not LogExecutorMessage cast)
                return;

            _menu.WriteLog(cast.Log);
        }
    }
}
