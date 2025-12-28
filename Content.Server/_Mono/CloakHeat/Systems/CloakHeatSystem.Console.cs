using Content.Server.Shuttles.Components;
using Content.Shared._Mono.CloakHeat;
using Content.Shared.Examine;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Timing;

namespace Content.Server._Mono.CloakHeat.Systems;

/// <summary>
/// Adds CloakHeat component to grids that have IFF consoles with Hide capability.
/// </summary>
public sealed class CloakHeatServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IFFConsoleComponent, ComponentStartup>(OnIFFConsoleStartup);
        SubscribeLocalEvent<IFFConsoleComponent, ExaminedEvent>(OnIFFConsoleExamined);
    }

    private void OnIFFConsoleStartup(EntityUid uid, IFFConsoleComponent component, ComponentStartup args)
    {
        TryAddHeatToGrid(uid, component);
    }

    private void TryAddHeatToGrid(EntityUid consoleUid, IFFConsoleComponent component)
    {
        // Check if this IFF console has Hide flag in allowedFlags
        if ((component.AllowedFlags & IFFFlags.Hide) == 0)
            return;

        // Get the grid this console is on
        if (!TryComp<TransformComponent>(consoleUid, out var xform) || xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;

        // Add CloakHeat component to the grid if it doesn't already have one
        if (!HasComp<CloakHeatComponent>(gridUid))
        {
            AddComp<CloakHeatComponent>(gridUid);
        }
    }

    private void OnIFFConsoleExamined(EntityUid uid, IFFConsoleComponent consoleComp, ExaminedEvent args)
    {
        // Only show heat for consoles that can control Hide flag
        if ((consoleComp.AllowedFlags & IFFFlags.Hide) == 0)
            return;

        // Get the grid this console is on
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null)
            return;

        // Get the heat component from the grid
        if (!TryComp<CloakHeatComponent>(xform.GridUid.Value, out var heatComp))
            return;

        var currentTime = _timing.CurTime;

        if (heatComp.IsOverheated && heatComp.CooldownEndTime.HasValue)
        {
            var remainingCooldown = heatComp.CooldownEndTime.Value - currentTime;
            if (remainingCooldown > TimeSpan.Zero)
            {
                var minutes = (int)remainingCooldown.TotalMinutes;
                var seconds = remainingCooldown.Seconds;
                args.PushMarkup(Loc.GetString("cloak-heat-overheated",
                    ("minutes", minutes), ("seconds", seconds)));
                return;
            }
        }

        // Show heat level
        var heatPercent = (int)(heatComp.CurrentHeat * 100);
        if (heatPercent > 0)
        {
            string heatStatus;
            if (heatPercent < 25)
                heatStatus = Loc.GetString("cloak-heat-low");
            else if (heatPercent < 50)
                heatStatus = Loc.GetString("cloak-heat-moderate");
            else if (heatPercent < 75)
                heatStatus = Loc.GetString("cloak-heat-high");
            else
                heatStatus = Loc.GetString("cloak-heat-critical");

            args.PushMarkup(Loc.GetString("cloak-heat-status",
                ("percent", heatPercent), ("status", heatStatus)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("cloak-heat-cool"));
        }
    }
}
