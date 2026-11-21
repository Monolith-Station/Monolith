using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Lathe;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class FaxPrintCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string Label = "";
    [PacketField(PacketFieldType.Accepts)]
    public string Text = "";

    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<FaxMachineComponent>(entity, out var faxMachine))
            return;

        packetSystem.FaxSystem.PrintFile(entity, faxMachine, new FaxFileMessage(Label,  Text, false));
    }
}
