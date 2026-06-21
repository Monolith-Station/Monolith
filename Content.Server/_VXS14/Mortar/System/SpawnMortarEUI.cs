using Content.Server.EUI;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Content.Server._VXS14.Mortar;
using Content.Shared._VXS14.Mortar;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._VXS14.Mortar;

/// <summary>
///     Mortar Eui
/// </summary>
///


[UsedImplicitly]
public sealed class MortarEui : BaseEui
{
    private int Count = 0;
    private readonly EntityUid Mortar;

    public MortarEui(EntityUid uid)
    {
        Mortar = uid;
    }

    public override void Opened()
    {
        base.Opened();

        // Send mortar configuration to the client
        var entMan = IoCManager.Resolve<IEntityManager>();
        var mortarComp = entMan.GetComponent<SharedMortarComponent>(Mortar);
        SendMessage(new MortarSpawnExplosionEuiMsg.MortarConfig(
            mortarComp.MinOffsetX,
            mortarComp.MaxOffsetX,
            mortarComp.MinOffsetY,
            mortarComp.MaxOffsetY,
            mortarComp.MinSafeDistance));
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not MortarSpawnExplosionEuiMsg.MortarCords request)
        {
            Close();
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        var mortarComp = entMan.GetComponent<SharedMortarComponent>(Mortar);

        // Store the target offsets in the mortar component for auto-fire on shell insert
        mortarComp.TargetOffsetX = request.OffsetX;
        mortarComp.TargetOffsetY = request.OffsetY;

        // Check if there's a shell loaded — if so, fire immediately
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var itemSlots = sysMan.GetEntitySystem<ItemSlotsSystem>();
        var rocket = itemSlots.GetItemOrNull(Mortar, "mortar_chamber");

        if (rocket != null)
        {
            var mortarSystem = sysMan.GetEntitySystem<MortarSystem>();
            mortarSystem.FireMortar(Mortar, mortarComp, request.OffsetX, request.OffsetY);
        }

        Close();
    }
}
