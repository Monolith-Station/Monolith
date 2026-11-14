using Content.Shared._KS14.Packets.Widgets;
using Robust.Shared.Serialization;

namespace Content.Shared._KS14.Packets.BUI
{
    [Serializable, NetSerializable]
    public enum ExecutorUiKey
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class StartExecutionMessage : BoundUserInterfaceMessage
    {
        public string Command;

        public StartExecutionMessage(string command)
        {
            Command = command;
        }
    }

    [Serializable, NetSerializable]
    public sealed class SendExecutionPacketMessage : BoundUserInterfaceMessage
    {
        public int Frequency;
        public string Address;
        public string Command;

        public SendExecutionPacketMessage(int frequency, string address, string command)
        {
            Frequency = frequency;
            Address = address;
            Command = command;
        }
    }

    [Serializable, NetSerializable]
    public sealed class SaveExecutorCommandMessage : BoundUserInterfaceMessage
    {
        public string Command;

        public SaveExecutorCommandMessage(string command)
        {
            Command = command;
        }
    }

    [Serializable, NetSerializable]
    public sealed class LogExecutorMessage : BoundUserInterfaceMessage
    {
        public string Log;

        public LogExecutorMessage(string log)
        {
            Log = log;
        }
    }

    [Serializable, NetSerializable]
    public sealed class TerminateExecutorMessage : BoundUserInterfaceMessage;

    [Serializable, NetSerializable]
    public sealed class ReloadModulesMessage : BoundUserInterfaceMessage;

    [Serializable, NetSerializable]
    public sealed class ExecutorBoundUserInterfaceState : BoundUserInterfaceState
    {
        public int MaxStatements;
        public int MaxMemory;

        public string Command;
        public Widget[] Widgets;

        public ExecutorBoundUserInterfaceState(int maxStatements, int maxMemory, string command, Widget[] widgets)
        {
            MaxStatements = maxStatements;
            MaxMemory = maxMemory;
            Command = command;
            Widgets = widgets;
        }
    }

    [Serializable, NetSerializable]
    public sealed class WidgetSendMessage : BoundUserInterfaceMessage
    {
        public Widget Widget;

        public WidgetSendMessage(Widget widget)
        {
            Widget = widget;
        }
    }

    [Serializable, NetSerializable]
    public sealed class WidgetUpdateElementMessage : BoundUserInterfaceMessage
    {
        public WidgetElement Element;

        public WidgetUpdateElementMessage(WidgetElement element)
        {
            Element = element;
        }
    }

    [Serializable, NetSerializable]
    public sealed class WidgetButtonClickedMessage : BoundUserInterfaceMessage
    {
        public string Button;
        public Dictionary<string, string> Arguments;

        public WidgetButtonClickedMessage(string button, Dictionary<string, string> arguments)
        {
            Button = button;
            Arguments = arguments;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ExecutorRequestCallsInfoMessage : BoundUserInterfaceMessage
    {
        public List<DeviceCallEntry>? Calls;
    }
}
