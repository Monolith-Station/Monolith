using Content.Server.EUI;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Content.Server._VXS14.Mortar;
using Content.Shared._VXS14.Mortar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Robust.Shared.Timing;
using RobustTimer = Robust.Shared.Timing.Timer;
using Robust.Shared.Map;
using System.Threading;

namespace Content.Server._VXS14.Mortar;

[UsedImplicitly]
public sealed class MortarEui : BaseEui
{
    private readonly EntityUid _mortar;
    private CancellationTokenSource? _distanceCheckCts;

    public MortarEui(EntityUid uid)
    {
        _mortar = uid;
    }

    public override void Opened()
    {
        base.Opened();

        var entMan = IoCManager.Resolve<IEntityManager>();
        var mortarComp = entMan.GetComponent<SharedMortarComponent>(_mortar);
        SendMessage(new MortarSpawnExplosionEuiMsg.MortarConfig(
            mortarComp.MinOffsetX,
            mortarComp.MaxOffsetX,
            mortarComp.MinOffsetY,
            mortarComp.MaxOffsetY,
            mortarComp.MinSafeDistance));

        var timerMan = IoCManager.Resolve<ITimerManager>();
        _distanceCheckCts = new CancellationTokenSource();
        timerMan.AddTimer(new RobustTimer(500, true, CheckDistance), _distanceCheckCts.Token);
    }

    public override void Closed()
    {
        base.Closed();
        _distanceCheckCts?.Cancel();
    }

    private void CheckDistance()
    {
        var playerEntity = Player.AttachedEntity;

        if (playerEntity == null)
        {
            Close();
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        if (!entMan.EntityExists(_mortar))
        {
            Close();
            return;
        }

        var mortarPos = entMan.System<SharedTransformSystem>().GetMapCoordinates(_mortar);
        var playerPos = entMan.System<SharedTransformSystem>().GetMapCoordinates(playerEntity.Value);

        if (mortarPos.MapId != playerPos.MapId ||
            (mortarPos.Position - playerPos.Position).LengthSquared() > SharedInteractionSystem.InteractionRangeSquared)
        {
            Close();
        }
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
        var mortarComp = entMan.GetComponent<SharedMortarComponent>(_mortar);

        mortarComp.TargetOffsetX = request.OffsetX;
        mortarComp.TargetOffsetY = request.OffsetY;

        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        var itemSlots = sysMan.GetEntitySystem<ItemSlotsSystem>();
        var rocket = itemSlots.GetItemOrNull(_mortar, "mortar_chamber");

        if (rocket != null)
        {
            var mortarSystem = sysMan.GetEntitySystem<MortarSystem>();
            mortarSystem.FireMortar(_mortar, mortarComp, request.OffsetX, request.OffsetY);
        }

        Close();
    }
}
