using System.Numerics;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.CloakHeat.Systems;

/// <summary>
/// System that manages heat buildup for cloaking.
/// </summary>
public sealed class CloakHeatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttle = default!;

    private bool isMoving;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CloakHeatComponent, ComponentInit>(OnComponentInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CloakHeatComponent, IFFComponent>();

        while (query.MoveNext(out var gridUid, out var heatComp, out var iffComp))
        {
            // Initialize timing if needed
            if (heatComp.LastUpdateTime == TimeSpan.Zero)
            {
                heatComp.LastUpdateTime = currentTime;
                Dirty(gridUid, heatComp);
                continue;
            }

            var deltaTime = (float)(currentTime - heatComp.LastUpdateTime).TotalSeconds;
            heatComp.LastUpdateTime = currentTime;

            // Check if we're in cooldown
            if (heatComp.CooldownEndTime.HasValue)
            {
                if (currentTime >= heatComp.CooldownEndTime.Value)
                {
                    // Cooldown finished
                    heatComp.CooldownEndTime = null;
                    heatComp.IsOverheated = false;
                    heatComp.CurrentHeat = 0f;
                    Dirty(gridUid, heatComp);
                }
                else
                {
                    // Still in cooldown
                    if ((iffComp.Flags & IFFFlags.Hide) != 0)
                    {
                        _shuttle.RemoveIFFFlag(gridUid, IFFFlags.Hide, iffComp);
                    }
                    continue;
                }
            }

            if (TryComp<PhysicsComponent>(gridUid, out var pcomp))
                isMoving = pcomp.LinearVelocity != Vector2.Zero;

            // Check if Hide flag is active on this grid
            bool hideActive = (iffComp.Flags & IFFFlags.Hide) != 0;

            // Update heat based on Hide flag status
            if (hideActive && isMoving)
            {
                // Build up heat
                heatComp.CurrentHeat += heatComp.HeatBuildupRate * deltaTime;

                // Check for overheat
                if (heatComp.CurrentHeat >= 1.0f)
                {
                    heatComp.CurrentHeat = 1.0f;
                    heatComp.IsOverheated = true;
                    heatComp.CooldownEndTime = currentTime + heatComp.CooldownDuration;

                    // Remove Hide flag due to overheating
                    _shuttle.RemoveIFFFlag(gridUid, IFFFlags.Hide, iffComp);
                }
            }
            else
            {
                // Dissipate heat
                heatComp.CurrentHeat -= heatComp.HeatDissipationRate * deltaTime;
                heatComp.CurrentHeat = Math.Max(0f, heatComp.CurrentHeat);
            }

            Dirty(gridUid, heatComp);
        }
    }

    private void OnComponentInit(EntityUid uid, CloakHeatComponent component, ComponentInit args)
    {
        component.LastUpdateTime = _timing.CurTime;
    }
}
