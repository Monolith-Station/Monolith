using Content.Shared._Forge.Clothing;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Forge.Clothing;

public sealed partial class ModsuitGauntletToolsSystem : SharedModsuitGauntletToolsSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitGauntletToolsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ModsuitGauntletToolsComponent, ComponentShutdown>(OnShutdown);

        SubscribeAllEvent<ModsuitGauntletToggleToolMessage>(OnToggleToolRequest);
    }

    private void OnToggleToolRequest(ModsuitGauntletToggleToolMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } wearer)
            return;

        if (!TryGetEntity(msg.Gauntlets, out var gauntlets) || gauntlets is not { } gauntletsEnt)
            return;

        if (!TryComp(gauntletsEnt, out ModsuitGauntletToolsComponent? comp))
            return;

        if (GetWearer(gauntletsEnt) != wearer)
            return;

        ToggleToolBySlot((gauntletsEnt, comp), wearer, msg.Slot);
    }

    private void OnMapInit(Entity<ModsuitGauntletToolsComponent> ent, ref MapInitEvent args)
    {
        var slots = ent.Comp.EnabledSlots;

        if (slots.HasFlag(ModsuitGauntletEnabledSlots.Urk))
            EnsureGauntletTool(ent, ent.Comp.UrkProto, ref ent.Comp.UrkEntity);

        if (slots.HasFlag(ModsuitGauntletEnabledSlots.Omnitool))
            EnsureGauntletTool(ent, ent.Comp.OmnitoolProto, ref ent.Comp.OmnitoolEntity);

        if (slots.HasFlag(ModsuitGauntletEnabledSlots.Welder))
            EnsureGauntletTool(ent, ent.Comp.WelderProto, ref ent.Comp.WelderEntity);

        if (slots.HasFlag(ModsuitGauntletEnabledSlots.NaniteApplicator))
            EnsureGauntletTool(ent, ent.Comp.NaniteApplicatorProto, ref ent.Comp.NaniteApplicatorEntity);

        if (slots.HasFlag(ModsuitGauntletEnabledSlots.Auxiliary))
            EnsureGauntletTool(ent, ent.Comp.AuxiliaryProto, ref ent.Comp.AuxiliaryEntity);

        if (slots.HasFlag(ModsuitGauntletEnabledSlots.Piping))
            EnsureGauntletTool(ent, ent.Comp.PipingProto, ref ent.Comp.PipingEntity);

        Dirty(ent);
    }

    private void EnsureGauntletTool(Entity<ModsuitGauntletToolsComponent> gauntlets, EntProtoId proto, ref EntityUid? entity)
    {
        if (entity != null)
            return;

        var tool = Spawn(proto, MapCoordinates.Nullspace);
        EnsureToolLink(tool, gauntlets);
        StoreToolHidden(tool);
        entity = tool;
    }

    private void OnShutdown(Entity<ModsuitGauntletToolsComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.UrkEntity != null)
            QueueDel(ent.Comp.UrkEntity);

        if (ent.Comp.OmnitoolEntity != null)
            QueueDel(ent.Comp.OmnitoolEntity);

        if (ent.Comp.WelderEntity != null)
            QueueDel(ent.Comp.WelderEntity);

        if (ent.Comp.NaniteApplicatorEntity != null)
            QueueDel(ent.Comp.NaniteApplicatorEntity);

        if (ent.Comp.AuxiliaryEntity != null)
            QueueDel(ent.Comp.AuxiliaryEntity);

        if (ent.Comp.PipingEntity != null)
            QueueDel(ent.Comp.PipingEntity);
    }
}
