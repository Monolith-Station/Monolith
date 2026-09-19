namespace Content.Server._KS14.Packet.Modules.Network;

[ModuleMethod("NetworkPacketModule")]
public sealed class GetNetworkMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Func<string, string[]> ModuleExec { get; }

    private string[] Func(string address)
    {
        if (Module is not NetworkPacketModule module)
            return [];

        if (!module.PacketSystem.TryGetNetwork(address, out var network))
        {
            module.Log(LogState.Error, "executor-log-no-network");
            return [];
        }

        return network.Addresses;
    }

    public GetNetworkMethod(PacketModule? module) : base(module)
    {
        Id = "get_network";
        Module = module;
        ModuleExec = Func;
    }
}
