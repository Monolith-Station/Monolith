using Robust.Shared.Serialization;

namespace Content.Shared._KS14.Packets.BUI
{
    [Serializable, NetSerializable]
    public enum ExecutorUiKey
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class StartExecutionMessage : BoundUserInterfaceMessage;

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
    public sealed class InputExecutorMessage : BoundUserInterfaceMessage
    {
        public string Input;

        public InputExecutorMessage(string input)
        {
            Input = input;
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

        public ExecutorBoundUserInterfaceState(int maxStatements, int maxMemory, string command)
        {
            MaxStatements = maxStatements;
            MaxMemory = maxMemory;
            Command = command;
        }
    }
}
