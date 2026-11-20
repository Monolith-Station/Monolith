using System.Linq;
using System.Numerics;
using Content.Shared._Mono.MassFabricator;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class AlignMassFabricator : PlacementMode
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IInputManager _input = default!;
    private HashSet<EntityCoordinates>? _dragPreview;
    private static readonly Vector2[] Neighbors = [Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY];

    public AlignMassFabricator(PlacementManager manager) : base(manager)
    {
        IoCManager.InjectDependencies(this);
        InvalidPlaceColor = Color.Transparent;
    }

    public override bool HasLineMode => true;
    public override bool HasGridMode => true;

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        var entities = pManager.EntityManager;
        var transform = entities.System<SharedTransformSystem>();
        var dragging = pManager.PlacementType != PlacementManager.PlacementTypes.None;
        var raw = entities.System<MassFabricatorClientSystem>().GetBuildCursor(mouseScreen, lookUp: !dragging);
        MouseCoords = default;
        if (dragging)
        {
            if (!raw.IsValid(entities) || !HasValidDragCursor(mouseScreen))
            {
                pManager.PlacementType = PlacementManager.PlacementTypes.None;
                return;
            }

            raw = transform.WithEntityId(raw, pManager.StartPoint.EntityId);
        }

        var kind = pManager.CurrentPermission!.IsTile
            ? MassFabricatorBlueprintKind.Tile
            : MassFabricatorBlueprintKind.Entity;
        var system = entities.System<MassFabricatorClientSystem>();
        if (_players.LocalEntity is not { } user ||
            !system.TryGetPlacementCoordinates(user, kind, raw, out var coordinates))
            return;
        MouseCoords = coordinates;

        if (transform.GetGrid(MouseCoords) is not { } uid ||
            !entities.TryGetComponent<MapGridComponent>(uid, out var grid))
            return;

        var maps = entities.System<SharedMapSystem>();
        CurrentTile = maps.GetTileRef(uid, grid, MouseCoords);
        GridDistancing = grid.TileSize;
        var offset = pManager.CurrentPermission.IsTile ? Vector2.Zero : (Vector2) pManager.PlacementOffset;
        MouseCoords = maps.GridTileToLocal(uid, grid, CurrentTile.GridIndices).Offset(offset);
    }

    public override void Render(in OverlayDrawArgs args)
    {
        if (pManager.PlacementType != PlacementManager.PlacementTypes.None &&
            !HasValidDragCursor(_input.MouseScreenPosition))
            return;

        if (pManager.CurrentPermission?.IsTile == true && pManager.PlacementType != PlacementManager.PlacementTypes.None)
            _dragPreview = GetConnectedDragTiles().ToHashSet();

        try
        {
            base.Render(args);
        }
        finally
        {
            _dragPreview = null;
        }
    }

    public void PlaceDragTiles()
    {
        var system = pManager.EntityManager.System<MassFabricatorClientSystem>();
        if (HasValidDragCursor(_input.MouseScreenPosition))
        {
            foreach (var tile in GetConnectedDragTiles())
                system.Place(tile, Angle.Zero);
        }

        pManager.PlacementType = PlacementManager.PlacementTypes.None;
    }

    private IEnumerable<EntityCoordinates> GetConnectedDragTiles()
    {
        if (_players.LocalEntity is not { } user)
            yield break;

        var system = pManager.EntityManager.System<MassFabricatorClientSystem>();
        var coordinates = pManager.PlacementType == PlacementManager.PlacementTypes.Grid
            ? GridCoordinates()
            : LineCoordinates();
        var pending = new HashSet<EntityCoordinates>();
        var connected = new HashSet<EntityCoordinates>();
        var queue = new Queue<EntityCoordinates>();
        foreach (var tile in coordinates)
        {
            if (!system.TryGetPlacementCoordinates(user, MassFabricatorBlueprintKind.Tile, tile, out var resolved) ||
                resolved.EntityId != tile.EntityId || !system.CanSeePlanPlacement(user, resolved))
                continue;

            if (!system.CanPlaceTile(user, resolved))
                pending.Add(resolved);
            else if (connected.Add(resolved))
                queue.Enqueue(resolved);
        }

        // Submit supported cells before the cells that depend on them.
        for (var count = 0; count < 256 && queue.TryDequeue(out var tile); count++)
        {
            yield return tile;
            foreach (var neighbor in Neighbors)
            {
                var next = tile.Offset(neighbor * GridDistancing);
                if (pending.Remove(next) && system.CanPlaceTile(user, next, connected) && connected.Add(next))
                    queue.Enqueue(next);
            }
        }
    }

    public bool HasValidDragCursor(ScreenCoordinates mouse)
    {
        var entities = pManager.EntityManager;
        if (!mouse.IsValid || !pManager.StartPoint.IsValid(entities))
            return false;

        var map = entities.System<SharedTransformSystem>().GetMapId(pManager.StartPoint);
        return map != MapId.Nullspace && pManager.EyeManager.PixelToMap(mouse).MapId == map;
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        var system = pManager.EntityManager.System<MassFabricatorClientSystem>();
        var isTile = pManager.CurrentPermission?.IsTile == true;
        var kind = isTile ? MassFabricatorBlueprintKind.Tile : MassFabricatorBlueprintKind.Entity;
        if (_players.LocalEntity is not { } user ||
            !system.TryGetPlacementCoordinates(user, kind, position, out var coordinates) ||
            !system.CanSeePlanPlacement(user, coordinates))
            return false;

        if (!isTile)
            return system.CanPlacePlanOnTile(coordinates);

        return _dragPreview?.Contains(coordinates) ?? system.CanPreviewTile(user, coordinates);
    }
}
