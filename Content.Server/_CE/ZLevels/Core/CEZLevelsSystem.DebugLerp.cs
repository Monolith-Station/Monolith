using Content.Server._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Drives <see cref="CEZDebugAltitudeLerpComponent"/>: smoothly moves a
    /// transiting grid towards a target absolute altitude, then removes itself.
    /// </summary>
    private void UpdateDebugAltitudeLerps(float frameTime)
    {
        var query = EntityQueryEnumerator<CEZDebugAltitudeLerpComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var lerp, out var grid))
        {
            lerp.Elapsed += frameTime;
            var t = lerp.Duration <= 0f
                ? 1f
                : Math.Clamp(lerp.Elapsed / lerp.Duration, 0f, 1f);

            // Smoothstep so the ship eases in and out instead of jerking.
            var eased = t * t * (3f - 2f * t);
            var altitude = MathHelper.Lerp(lerp.StartAltitude, lerp.TargetAltitude, eased);

            var ok = SetTransitAltitude((uid, grid), altitude);

            // Z-gravity keeps integrating velocity while we override position;
            // zero it so the lerp stays smooth and doesn't slam on release.
            foreach (var gridUid in CollectGridSet(uid))
            {
                if (ZPhysicsQuery.TryComp(gridUid, out var zPhys) && zPhys.Velocity != 0f)
                {
                    zPhys.Velocity = 0f;
                    DirtyField(gridUid, zPhys, nameof(CEZPhysicsComponent.Velocity));
                }
            }

            // Done, or the grid left transit (landed / deleted) under us.
            if (!ok || t >= 1f)
                RemCompDeferred<CEZDebugAltitudeLerpComponent>(uid);
        }
    }
}
