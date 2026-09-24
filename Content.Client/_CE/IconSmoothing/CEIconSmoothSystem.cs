using Content.Client.IconSmoothing;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Utility;

namespace Content.Client._CE.IconSmoothing;

/// <summary>
///     Tile-based icon smoothing system.
///     Works like Corners mode but selects the RSI for each corner from the adjacent tile's
///     <see cref="ContentTileDefinition.IconSmoothSprite"/>.
/// </summary>
public sealed partial class CEIconSmoothSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private ITileDefinitionManager _tileDefManager = default!;

    private readonly Queue<EntityUid> _dirtyEntities = new();
    private readonly Queue<EntityUid> _anchorChangedEntities = new();
    private int _generation;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEIconSmoothComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CEIconSmoothComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CEIconSmoothComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MapGridComponent, TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(Entity<MapGridComponent> gridEntity, ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            var pos = change.GridIndices;

            // Dirty all CE smooth entities in a 3×3 area around the changed tile.
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(
                        gridEntity.Owner, gridEntity.Comp, pos + new Vector2i(dx, dy)));
                }
            }
        }
    }

    private void OnStartup(EntityUid uid, CEIconSmoothComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        if (xform.Anchored)
        {
            component.LastPosition = TryComp<MapGridComponent>(xform.GridUid, out var grid)
                ? (xform.GridUid.Value, _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates))
                : (null, new Vector2i(0, 0));

            DirtyNeighbours(uid, component);
        }

        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        SetCornerLayers(new Entity<SpriteComponent>(uid, sprite));
    }

    private void SetCornerLayers(Entity<SpriteComponent> sprite)
    {
        var nullable = sprite.AsNullable();

        _sprite.LayerMapRemove(nullable, CECornerLayers.SE);
        _sprite.LayerMapRemove(nullable, CECornerLayers.NE);
        _sprite.LayerMapRemove(nullable, CECornerLayers.NW);
        _sprite.LayerMapRemove(nullable, CECornerLayers.SW);
        _sprite.LayerMapRemove(nullable, CECornerLayers.SEAlt);
        _sprite.LayerMapRemove(nullable, CECornerLayers.NEAlt);
        _sprite.LayerMapRemove(nullable, CECornerLayers.NWAlt);
        _sprite.LayerMapRemove(nullable, CECornerLayers.SWAlt);

        AddCornerPair(nullable, CECornerLayers.SE, CECornerLayers.SEAlt,
            SpriteComponent.DirectionOffset.None);
        AddCornerPair(nullable, CECornerLayers.NE, CECornerLayers.NEAlt,
            SpriteComponent.DirectionOffset.CounterClockwise);
        AddCornerPair(nullable, CECornerLayers.NW, CECornerLayers.NWAlt,
            SpriteComponent.DirectionOffset.Flip);
        AddCornerPair(nullable, CECornerLayers.SW, CECornerLayers.SWAlt,
            SpriteComponent.DirectionOffset.Clockwise);
    }

    private void AddCornerPair(
        Entity<SpriteComponent?> sprite,
        CECornerLayers primary,
        CECornerLayers alt,
        SpriteComponent.DirectionOffset offset)
    {
        _sprite.LayerMapSet(sprite, primary,
            _sprite.AddRsiLayer(sprite, RSI.StateId.Invalid));
        _sprite.LayerSetDirOffset(sprite, primary, offset);
        _sprite.LayerSetVisible(sprite, primary, false);

        _sprite.LayerMapSet(sprite, alt,
            _sprite.AddRsiLayer(sprite, RSI.StateId.Invalid));
        _sprite.LayerSetDirOffset(sprite, alt, offset);
        _sprite.LayerSetVisible(sprite, alt, false);
    }

    private void OnShutdown(EntityUid uid, CEIconSmoothComponent component, ComponentShutdown args)
    {
        _dirtyEntities.Enqueue(uid);
        DirtyNeighbours(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, CEIconSmoothComponent component, ref AnchorStateChangedEvent args)
    {
        if (!args.Detaching)
            _anchorChangedEntities.Enqueue(uid);
    }

    public void DirtyNeighbours(
        EntityUid uid,
        CEIconSmoothComponent? comp = null,
        TransformComponent? transform = null,
        EntityQuery<CEIconSmoothComponent>? smoothQuery = null)
    {
        smoothQuery ??= GetEntityQuery<CEIconSmoothComponent>();
        if (!smoothQuery.Value.Resolve(uid, ref comp) || !comp.Running)
            return;

        _dirtyEntities.Enqueue(uid);

        if (!Resolve(uid, ref transform))
            return;

        Vector2i pos;
        EntityUid entityUid;

        if (transform.Anchored && TryComp<MapGridComponent>(transform.GridUid, out var grid))
        {
            entityUid = transform.GridUid.Value;
            pos = _mapSystem.CoordinatesToTile(transform.GridUid.Value, grid, transform.Coordinates);
        }
        else
        {
            if (comp.LastPosition is not (EntityUid gridId, Vector2i oldPos))
                return;

            if (!TryComp(gridId, out grid))
                return;

            entityUid = gridId;
            pos = oldPos;
        }

        // Dirty all neighbours including diagonals.
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(1, 0)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(-1, 0)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(0, 1)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(0, -1)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(1, 1)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(-1, -1)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(-1, 1)));
        DirtyEntities(_mapSystem.GetAnchoredEntitiesEnumerator(entityUid, grid, pos + new Vector2i(1, -1)));
    }

    private void DirtyEntities(AnchoredEntitiesEnumerator entities)
    {
        while (entities.MoveNext(out var entity))
        {
            _dirtyEntities.Enqueue(entity.Value);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var xformQuery = GetEntityQuery<TransformComponent>();
        var smoothQuery = GetEntityQuery<CEIconSmoothComponent>();

        while (_anchorChangedEntities.TryDequeue(out var uid))
        {
            if (!xformQuery.TryGetComponent(uid, out var xform))
                continue;

            if (xform.MapID == MapId.Nullspace)
                continue;

            DirtyNeighbours(uid, comp: null, xform, smoothQuery);
        }

        if (_dirtyEntities.Count == 0)
            return;

        _generation += 1;
        var spriteQuery = GetEntityQuery<SpriteComponent>();
        var vanillaQuery = GetEntityQuery<IconSmoothComponent>();

        while (_dirtyEntities.TryDequeue(out var uid))
        {
            CalculateNewSprite(uid, spriteQuery, smoothQuery, xformQuery, vanillaQuery);
        }
    }

    private void CalculateNewSprite(
        EntityUid uid,
        EntityQuery<SpriteComponent> spriteQuery,
        EntityQuery<CEIconSmoothComponent> smoothQuery,
        EntityQuery<TransformComponent> xformQuery,
        EntityQuery<IconSmoothComponent> vanillaQuery,
        CEIconSmoothComponent? smooth = null)
    {
        if (!smoothQuery.Resolve(uid, ref smooth, false)
            || smooth.UpdateGeneration == _generation
            || !smooth.Enabled
            || !smooth.Running)
        {
            return;
        }

        var xform = xformQuery.GetComponent(uid);
        smooth.UpdateGeneration = _generation;

        if (!spriteQuery.TryGetComponent(uid, out var sprite))
        {
            Log.Error($"CE icon-smooth entity without a sprite: {ToPrettyString(uid)}");
            RemCompDeferred(uid, smooth);
            return;
        }

        Entity<MapGridComponent>? gridEntity = null;

        if (xform.Anchored)
        {
            if (TryComp(xform.GridUid, out MapGridComponent? grid))
                gridEntity = (xform.GridUid.Value, grid);
            else
            {
                Log.Error(
                    $"Failed to calculate CEIconSmooth for {uid}: grid {xform.GridUid} missing.");
                return;
            }
        }

        CalculateNewSpriteCorners(gridEntity, smooth, (uid, sprite), xform, smoothQuery, vanillaQuery);
    }

    private void CalculateNewSpriteCorners(
        Entity<MapGridComponent>? gridEntity,
        CEIconSmoothComponent smooth,
        Entity<SpriteComponent> spriteEnt,
        TransformComponent xform,
        EntityQuery<CEIconSmoothComponent> smoothQuery,
        EntityQuery<IconSmoothComponent> vanillaQuery)
    {
        var nullable = spriteEnt.AsNullable();

        if (gridEntity == null)
        {
            _sprite.LayerSetVisible(nullable, CECornerLayers.SE, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.NE, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.NW, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.SW, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.SEAlt, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.NEAlt, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.NWAlt, false);
            _sprite.LayerSetVisible(nullable, CECornerLayers.SWAlt, false);
            return;
        }

        var gridUid = gridEntity.Value.Owner;
        var grid = gridEntity.Value.Comp;
        var pos = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);

        // Calculate corner fills — identical to the original Corners mode.
        var n = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.North)), smoothQuery, vanillaQuery);
        var ne = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.NorthEast)), smoothQuery, vanillaQuery);
        var e = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.East)), smoothQuery, vanillaQuery);
        var se = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.SouthEast)), smoothQuery, vanillaQuery);
        var s = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.South)), smoothQuery, vanillaQuery);
        var sw = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.SouthWest)), smoothQuery, vanillaQuery);
        var w = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.West)), smoothQuery, vanillaQuery);
        var nw = MatchingEntity(smooth,
            _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, pos.Offset(Direction.NorthWest)), smoothQuery, vanillaQuery);

        var cornerNE = CornerFill.None;
        var cornerSE = CornerFill.None;
        var cornerSW = CornerFill.None;
        var cornerNW = CornerFill.None;

        if (n)
        {
            cornerNE |= CornerFill.CounterClockwise;
            cornerNW |= CornerFill.Clockwise;
        }

        if (ne)
            cornerNE |= CornerFill.Diagonal;

        if (e)
        {
            cornerNE |= CornerFill.Clockwise;
            cornerSE |= CornerFill.CounterClockwise;
        }

        if (se)
            cornerSE |= CornerFill.Diagonal;

        if (s)
        {
            cornerSE |= CornerFill.Clockwise;
            cornerSW |= CornerFill.CounterClockwise;
        }

        if (sw)
            cornerSW |= CornerFill.Diagonal;

        if (w)
        {
            cornerSW |= CornerFill.Clockwise;
            cornerNW |= CornerFill.CounterClockwise;
        }

        if (nw)
            cornerNW |= CornerFill.Diagonal;

        // Resolve tile RSI data for each world-space corner.
        var dataNE = GetCornerData(gridUid, grid, pos, cornerNE,
            Direction.NorthEast, Direction.North, Direction.East);
        var dataSE = GetCornerData(gridUid, grid, pos, cornerSE,
            Direction.SouthEast, Direction.East, Direction.South);
        var dataSW = GetCornerData(gridUid, grid, pos, cornerSW,
            Direction.SouthWest, Direction.South, Direction.West);
        var dataNW = GetCornerData(gridUid, grid, pos, cornerNW,
            Direction.NorthWest, Direction.West, Direction.North);

        // Apply rotation mapping (same as original Corners mode).
        CornerData visNE, visNW, visSW, visSE;

        switch (xform.LocalRotation.GetCardinalDir())
        {
            case Direction.North:
                (visNE, visNW, visSW, visSE) = (dataSW, dataSE, dataNE, dataNW);
                break;
            case Direction.West:
                (visNE, visNW, visSW, visSE) = (dataSE, dataNE, dataNW, dataSW);
                break;
            case Direction.South:
                (visNE, visNW, visSW, visSE) = (dataNE, dataNW, dataSW, dataSE);
                break;
            default:
                (visNE, visNW, visSW, visSE) = (dataNW, dataSW, dataSE, dataNE);
                break;
        }

        ApplyCorner(nullable, CECornerLayers.NE, CECornerLayers.NEAlt, visNE);
        ApplyCorner(nullable, CECornerLayers.NW, CECornerLayers.NWAlt, visNW);
        ApplyCorner(nullable, CECornerLayers.SW, CECornerLayers.SWAlt, visSW);
        ApplyCorner(nullable, CECornerLayers.SE, CECornerLayers.SEAlt, visSE);
    }

    private ResPath? GetTileRsi(EntityUid gridUid, MapGridComponent grid, Vector2i pos, Direction dir)
    {
        var tileRef = _mapSystem.GetTileRef(gridUid, grid, pos.Offset(dir));
        if (tileRef.Tile.IsEmpty)
            return null;

        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];
        return tileDef.IconSmoothSprite;
    }

    /// <summary>
    ///     Build per-corner data: primary RSI/state and optional secondary overlay.
    /// </summary>
    private CornerData GetCornerData(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i pos,
        CornerFill fill,
        Direction diagonal,
        Direction ccw,
        Direction cw)
    {
        const CornerFill all = CornerFill.CounterClockwise | CornerFill.Diagonal | CornerFill.Clockwise;

        // State 7: never drawn.
        if (fill == all)
            return default;

        // State 0 (no neighbours): look at both CCW and CW cardinal tiles.
        if (fill == CornerFill.None)
        {
            var rsiCcw = GetTileRsi(gridUid, grid, pos, ccw);
            var rsiCw = GetTileRsi(gridUid, grid, pos, cw);

            // Both null -> nothing to draw.
            if (rsiCcw == null && rsiCw == null)
                return default;

            // Same RSI (or one is null) -> single tile_0 layer.
            if (rsiCcw == rsiCw || rsiCcw == null || rsiCw == null)
            {
                return new CornerData
                {
                    PrimaryRsi = rsiCcw ?? rsiCw,
                    PrimaryState = "tile_0",
                };
            }

            // Different RSIs -> overlay: state 4 from CCW tile + state 1 from CW tile.
            return new CornerData
            {
                PrimaryRsi = rsiCcw,
                PrimaryState = "tile_4",
                AltRsi = rsiCw,
                AltState = "tile_1",
            };
        }

        // State 2 (diagonal only): same logic as state 0 but with tile_2/tile_6/tile_3.
        if (fill == CornerFill.Diagonal)
        {
            var rsiCcw = GetTileRsi(gridUid, grid, pos, ccw);
            var rsiCw = GetTileRsi(gridUid, grid, pos, cw);

            if (rsiCcw == null && rsiCw == null)
                return default;

            if (rsiCcw == rsiCw || rsiCcw == null || rsiCw == null)
            {
                return new CornerData
                {
                    PrimaryRsi = rsiCcw ?? rsiCw,
                    PrimaryState = "tile_2",
                };
            }

            // Different RSIs -> overlay: state 6 from CCW tile + state 3 from CW tile.
            return new CornerData
            {
                PrimaryRsi = rsiCcw,
                PrimaryState = "tile_6",
                AltRsi = rsiCw,
                AltState = "tile_3",
            };
        }

        // State 5 (CCW + CW, no diagonal): look at the diagonal tile.
        if (fill == (CornerFill.CounterClockwise | CornerFill.Clockwise))
        {
            var diagRsi = GetTileRsi(gridUid, grid, pos, diagonal);
            if (diagRsi == null)
                return default;

            return new CornerData
            {
                PrimaryRsi = diagRsi,
                PrimaryState = "tile_5",
            };
        }

        // States 1, 3 (CCW filled) -> border faces CW -> CW tile.
        // States 4, 6 (CW filled) -> border faces CCW -> CCW tile.
        Direction? lookDir = fill switch
        {
            CornerFill.CounterClockwise => cw,
            CornerFill.CounterClockwise | CornerFill.Diagonal => cw,
            CornerFill.Clockwise => ccw,
            CornerFill.Diagonal | CornerFill.Clockwise => ccw,
            _ => null,
        };

        if (lookDir == null)
            return default;

        var rsi = GetTileRsi(gridUid, grid, pos, lookDir.Value);
        if (rsi == null)
            return default;

        return new CornerData
        {
            PrimaryRsi = rsi,
            PrimaryState = $"tile_{(int) fill}",
        };
    }

    private void ApplyCorner(
        Entity<SpriteComponent?> sprite,
        CECornerLayers primary,
        CECornerLayers alt,
        CornerData data)
    {
        // Primary layer.
        if (data.PrimaryRsi != null)
        {
            _sprite.LayerSetRsi(sprite, primary, data.PrimaryRsi.Value,
                (RSI.StateId) data.PrimaryState!);
            _sprite.LayerSetVisible(sprite, primary, true);
        }
        else
        {
            _sprite.LayerSetVisible(sprite, primary, false);
        }

        // Alt (overlay) layer.
        if (data.AltRsi != null)
        {
            _sprite.LayerSetRsi(sprite, alt, data.AltRsi.Value,
                (RSI.StateId) data.AltState!);
            _sprite.LayerSetVisible(sprite, alt, true);
        }
        else
        {
            _sprite.LayerSetVisible(sprite, alt, false);
        }
    }

    private struct CornerData
    {
        public ResPath? PrimaryRsi;
        public string? PrimaryState;
        public ResPath? AltRsi;
        public string? AltState;
    }

    private bool MatchingEntity(
        CEIconSmoothComponent smooth,
        AnchoredEntitiesEnumerator candidates,
        EntityQuery<CEIconSmoothComponent> smoothQuery,
        EntityQuery<IconSmoothComponent> vanillaQuery)
    {
        while (candidates.MoveNext(out var entity))
        {
            // Check CE smooth entities.
            if (smoothQuery.TryGetComponent(entity, out var other)
                && other.SmoothKey != null
                && (other.SmoothKey == smooth.SmoothKey
                    || smooth.AdditionalKeys.Contains(other.SmoothKey))
                && other.Enabled)
            {
                return true;
            }

            // Check vanilla IconSmooth entities (cross-matching via AdditionalKeys).
            if (vanillaQuery.TryGetComponent(entity, out var vanilla)
                && vanilla.SmoothKey != null
                && (vanilla.SmoothKey == smooth.SmoothKey
                    || smooth.AdditionalKeys.Contains(vanilla.SmoothKey))
                && vanilla.Enabled)
            {
                return true;
            }
        }

        return false;
    }

    [Flags]
    private enum CornerFill : byte
    {
        None = 0,
        CounterClockwise = 1,
        Diagonal = 2,
        Clockwise = 4,
    }

    private enum CECornerLayers : byte
    {
        SE,
        NE,
        NW,
        SW,
        SEAlt,
        NEAlt,
        NWAlt,
        SWAlt,
    }
}
