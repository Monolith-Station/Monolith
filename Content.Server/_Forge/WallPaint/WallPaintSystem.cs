using Content.Server.Administration.Managers;
using Content.Shared._Forge.WallPaint;
using Content.Shared.Administration;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Server._Forge.WallPaint;

public sealed partial class WallPaintSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<WallPaintRequestEvent>(OnPaintRequest);
    }

    public bool TrySetPaint(EntityUid uid, Color color, bool remove)
    {
        if (!TryGetPaintSettings(uid, out var protectTransparent))
            return false;

        if (remove)
            return RemComp<WallPaintComponent>(uid);

        var paint = EnsureComp<WallPaintComponent>(uid);
        paint.Color = WallPaintColor.Clamp(color);
        paint.ProtectTransparent = protectTransparent;
        Dirty(uid, paint);
        return true;
    }

    public int PaintGrid(EntityUid gridUid, Color color, bool remove)
    {
        var count = 0;
        var enumerator = Transform(gridUid).ChildEnumerator;

        while (enumerator.MoveNext(out var uid))
        {
            if (TrySetPaint(uid, color, remove))
                count++;
        }

        return count;
    }

    public bool CanUseMappingPaint(ICommonSession session, EntityUid target)
    {
        if (!_admin.IsAdmin(session, true) ||
            !_admin.HasAdminFlag(session, AdminFlags.Mapping) ||
            !TryGetPaintSettings(target, out _))
        {
            return false;
        }

        if (session.AttachedEntity is not { } attached ||
            !TryComp(attached, out TransformComponent? actorTransform) ||
            !TryComp(target, out TransformComponent? targetTransform))
        {
            return false;
        }

        var targetMap = targetTransform.MapID;
        if (targetMap == MapId.Nullspace ||
            actorTransform.MapID != targetMap ||
            !_mapSystem.IsPaused(targetMap))
        {
            return false;
        }

        return true;
    }

    private bool TryGetPaintSettings(EntityUid uid, out bool protectTransparent)
    {
        if (TryComp(uid, out PaintableWallComponent? paintable))
        {
            protectTransparent = paintable.ProtectTransparent;
            return true;
        }

        protectTransparent = false;
        return false;
    }

    private void OnPaintRequest(WallPaintRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } session ||
            !TryGetEntity(ev.Target, out var target) ||
            !CanUseMappingPaint(session, target.Value))
        {
            return;
        }

        TrySetPaint(target.Value, ev.Color, ev.Remove);
    }
}
