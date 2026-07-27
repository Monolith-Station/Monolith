using Content.Server.Administration;
using Content.Shared._Forge.WallPaint;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Toolshed;

namespace Content.Server._Forge.WallPaint;

[AdminCommand(AdminFlags.Mapping)]
public sealed partial class FillWallPaintCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IEntitySystemManager _systemManager = default!;

    public string Command => "fpaint";
    public string Description => "Paints every paintable wall/window on a grid.";
    public string Help => $"{Command} <grid/entity NetEntity> <#RRGGBB|#RRGGBBAA|clear> - paint all paintable walls/windows on a paused grid, or clear paint.";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.NetEntities(args[0], _entManager), "<grid/entity NetEntity>"),
            2 => CompletionResult.FromHint("#RRGGBB, #RRGGBBAA, or clear"),
            _ => CompletionResult.Empty,
        };
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!TryGetGrid(args[0], out var gridUid, out var gridTransform, out var error))
        {
            shell.WriteError(error);
            return;
        }

        var mapSystem = _systemManager.GetEntitySystem<SharedMapSystem>();
        if (!mapSystem.IsPaused(gridTransform.MapID))
        {
            shell.WriteError("Wall paint is only available on paused mapping maps.");
            return;
        }

        var remove = args[1].Equals("clear", StringComparison.OrdinalIgnoreCase);
        var color = Color.White;

        if (!remove)
        {
            var parsed = Color.TryFromHex(args[1]);
            if (parsed == null)
            {
                shell.WriteError($"Failed to parse color '{args[1]}'. Expected #RRGGBB or #RRGGBBAA.");
                return;
            }

            color = parsed.Value;
            color = WallPaintColor.Clamp(color);
        }

        var wallPaint = _systemManager.GetEntitySystem<WallPaintSystem>();
        var count = wallPaint.PaintGrid(gridUid, color, remove);
        var action = remove ? "Cleared paint from" : $"Painted with {color.ToHex()}";
        shell.WriteLine($"{action} {count} walls/windows.");
    }

    private bool TryGetGrid(
        string rawId,
        out EntityUid gridUid,
        out TransformComponent gridTransform,
        out string error)
    {
        gridUid = default;
        gridTransform = default!;

        if (!NetEntity.TryParse(rawId, out var netId) ||
            !_entManager.TryGetEntity(netId, out var uid) ||
            !_entManager.TryGetComponent(uid.Value, out TransformComponent? transform))
        {
            error = $"Failed to parse entity id '{rawId}'. Expected an entity id like n123 or 123.";
            return false;
        }

        if (_entManager.HasComponent<MapGridComponent>(uid.Value))
        {
            gridUid = uid.Value;
            gridTransform = transform;
            error = string.Empty;
            return true;
        }

        if (transform.GridUid is { } parentGrid &&
            _entManager.HasComponent<MapGridComponent>(parentGrid) &&
            _entManager.TryGetComponent(parentGrid, out TransformComponent? parentGridTransform))
        {
            gridUid = parentGrid;
            gridTransform = parentGridTransform;
            error = string.Empty;
            return true;
        }

        error = $"Entity '{rawId}' is not a grid and is not located on a grid.";
        return false;
    }
}
