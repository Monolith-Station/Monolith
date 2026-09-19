namespace Content.Server._KS14.Packet.Modules.Base;

/// <summary>
/// This method is responsible for managing which frequencies allow or deny
/// You wont be able to detect this device if you send network message with denied frequency
/// </summary>
[ModuleMethod("BasePacketModule")]
public sealed class FrequencyMethod : ModuleMethod
{
    public override PacketModule? Module { get; set; }

    public override Action<int, string> ModuleExec { get; }

    private void Func(int confFreq, string operation)
    {
        if (Module is not BasePacketModule module)
            return;

        if (Module.Executor.Comp2 is not { } receiver)
            return;

        switch (operation)
        {
            case "ALLOW":
                receiver.ListeningFrequencies.Add(module.PacketSystem.GetFrequency(confFreq));
                break;
            case "DENY":
                receiver.ListeningFrequencies.Remove(module.PacketSystem.GetFrequency(confFreq));
                break;
        }
    }

    public FrequencyMethod(PacketModule? module) : base(module)
    {
        Id = "frequency";
        Module = module;
        ModuleExec = Func;
    }
}
