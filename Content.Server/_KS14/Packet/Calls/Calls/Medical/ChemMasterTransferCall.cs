using Content.Server._KS14.Packet.Attributes;
using Content.Server._KS14.Packet.Components;
using Content.Server.Ame.Components;
using Content.Server.Chemistry.Components;
using Content.Server.Lathe;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet.States.Calls;

public sealed class ChemMasterTransferCall : DeviceCall
{
    [PacketField(PacketFieldType.Accepts)]
    public string? Mode { get; set; }

    [PacketField(PacketFieldType.Accepts)]
    public string[] Reagents = [];
    [PacketField(PacketFieldType.Accepts)]
    public float[] Amount = [];

    [PacketField(PacketFieldType.Accepts)]
    public bool? FromBuffer { get; set; }
    protected override void GetCall(EntityUid entity, PacketSystem packetSystem, EntityManager entityMan, IPrototypeManager protoMan, PacketExecutorComponent executor)
    {
        if (!entityMan.TryGetComponent<ChemMasterComponent>(entity, out var chemMasterComponent))
            return;

        ChemMasterMode mode = chemMasterComponent.Mode;

        if (Enum.TryParse<ChemMasterMode>(Mode, out var chemMode))
            mode =  chemMode;

        if (mode == ChemMasterMode.Transfer)
        {
            for (var i = 0; i < Reagents.Length; i++)
            {
                packetSystem.ChemMasterSystem.TransferReagents((entity, chemMasterComponent), new ReagentId(Reagents[i], null), Amount[i], FromBuffer ?? true);
            }
        }

        if (mode == ChemMasterMode.Discard)
        {
            for (var i = 0; i < Reagents.Length; i++)
            {
                packetSystem.ChemMasterSystem.DiscardReagents((entity, chemMasterComponent), new ReagentId(Reagents[i], null), Amount[i], FromBuffer ?? true);
            }
        }
    }
}
