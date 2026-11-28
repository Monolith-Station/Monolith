namespace Content.Server._KS14.Packet.Modules.Network;

[ModuleMethod("NetworkPacketModule")]
public sealed class SendDataMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Action<int, string, object> ModuleExec { get; }

    private void Func(int frequency, string address, object data)
    {
        if (Module is not NetworkPacketModule module)
            return;

        var packetSys = module.PacketSystem;

        if (!packetSys.TryGetReceiver((module.Executor.Owner, module.Executor.Comp2), frequency, address, out var receiver))
        {
            module.Log(LogState.Error, "executor-log-no-device");
            return;
        }

        packetSys.TryWrapSystemCall(() => packetSys.SendData(data, receiver));
    }

    public SendDataMethod(PacketModule? module) : base(module)
    {
        Id = "data_send";
        Module = module;
        ModuleExec = Func;
    }
}
