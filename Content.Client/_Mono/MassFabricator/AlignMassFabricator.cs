using System.Numerics;
using Content.Shared._Mono.MassFabricator;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class AlignMassFabricator : PlacementMode
{
    [Dependency] private IPlayerManager _players = default!;

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
        var raw = ScreenToCursorGrid(mouseScreen);
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

        CurrentTile = entities.System<SharedMapSystem>().GetTileRef(uid, grid, MouseCoords);
        GridDistancing = grid.TileSize;
        var offset = pManager.CurrentPermission.IsTile ? Vector2.Zero : (Vector2) pManager.PlacementOffset;
        MouseCoords = new EntityCoordinates(MouseCoords.EntityId,
            new Vector2(CurrentTile.X + grid.TileSize / 2f, CurrentTile.Y + grid.TileSize / 2f) + offset);
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        var entities = pManager.EntityManager;
        var system = entities.System<MassFabricatorClientSystem>();
        return _players.LocalEntity is { } user &&
               system.TryGetPlacementCoordinates(user,
                   pManager.CurrentPermission?.IsTile == true
                       ? MassFabricatorBlueprintKind.Tile
                       : MassFabricatorBlueprintKind.Entity,
                   position, out var coordinates) &&
               system.CanSeePlanPlacement(user, coordinates) &&
               (pManager.CurrentPermission?.IsTile != true
                   ? system.CanPlacePlanOnTile(coordinates)
                   : system.CanPlaceTile(coordinates));
    }
}
