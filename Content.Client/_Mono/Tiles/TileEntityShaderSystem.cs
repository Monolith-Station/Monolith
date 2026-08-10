using System.Numerics;
using Content.Shared.Maps;
using Content.Shared._Mono.Tiles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.Tiles;

/// <summary>
/// Applies <see cref="ContentTileDefinition.EntityShader"/> to entities standing on a tile that declares one.
/// </summary>
public sealed partial class TileEntityShaderSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TurfSystem _turf = default!;

    private const float PostShadeScale = 1.25f;
    private readonly Dictionary<EntityUid, (string Id, ShaderInstance Instance)> _instances = new();

    /// <summary>
    /// Shader ids already reported as missing. If this ever has a single entry, laugh at the coder who faulted that hard on their shader.
    /// Seriously, it's a single prototype ID.
    /// </summary>
    private readonly HashSet<string> _missing = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TileShaderTargetComponent, BeforePostShaderRenderEvent>(OnBeforeRender);
        SubscribeLocalEvent<TileShaderTargetComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnBeforeRender(Entity<TileShaderTargetComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (!_instances.TryGetValue(ent.Owner, out var owned) || args.Sprite.PostShader != owned.Instance)
            return;

        var shader = owned.Instance;

        var xform = Transform(ent.Owner);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tileSize = grid.TileSize;

        var local = Vector2.Transform(_xform.GetWorldPosition(xform), _xform.GetInvWorldMatrix(gridUid));
        var tile = new Vector2i(
            (int) MathF.Floor(local.X / tileSize),
            (int) MathF.Floor(local.Y / tileSize));

        if (!_map.TryGetTile(grid, tile, out var ownTile))
            return;

        var shaderId = _turf.GetContentTileDefinition(ownTile).EntityShader;

        var bounds = _sprite.GetLocalBounds((ent.Owner, args.Sprite));
        var half = bounds.Size * 0.5f * PostShadeScale;
        var center = bounds.Center;
        var min = center - half;
        var max = center + half;
        var size = max - min;

        if (size.X <= 0f || size.Y <= 0f)
            return;

        // The tile region. Mostly for shader effects.
        var left = (Matching(grid, tile + new Vector2i(-1, 0), shaderId) ? tile.X - 1 : tile.X) * tileSize;
        var right = (Matching(grid, tile + new Vector2i(1, 0), shaderId) ? tile.X + 2 : tile.X + 1) * tileSize;
        var bottom = (Matching(grid, tile + new Vector2i(0, -1), shaderId) ? tile.Y - 1 : tile.Y) * tileSize;
        var top = (Matching(grid, tile + new Vector2i(0, 1), shaderId) ? tile.Y + 2 : tile.Y + 1) * tileSize;

        var rect = new Vector4(
            (left - local.X - min.X) / size.X,
            (bottom - local.Y - min.Y) / size.Y,
            (right - local.X - min.X) / size.X,
            (top - local.Y - min.Y) / size.Y);

        shader.SetParameter("TILE_RECT", rect);
        shader.SetParameter("UV_PER_TILE", tileSize / size.Y);
        shader.SetParameter("GROUND_UV", (bounds.Bottom - min.Y) / size.Y);

        // Also allow any other system to listen to this event and tweak the incoming shader as needed.
        var ev = new TileShaderParametersEvent(shader, _turf.GetContentTileDefinition(ownTile));
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    /// <summary>
    /// Whether the neighbouring tile carries the same shader.
    /// </summary>
    private bool Matching(MapGridComponent grid, Vector2i indices, string? shaderId)
    {
        if (!_map.TryGetTile(grid, indices, out var tile))
            return false;

        return _turf.GetContentTileDefinition(tile).EntityShader == shaderId;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // This really shouldn't be an EQE but what can you do; there's a whole host of potential edge cases if it's eventbased.
        var query = EntityQueryEnumerator<TileShaderTargetComponent, SpriteComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var wanted = GetTileShader(xform);
            var owned = _instances.TryGetValue(uid, out var existing) ? existing : default;

            if (owned.Instance != null && wanted != owned.Id)
            {
                Release(uid, sprite, owned.Instance);
                owned = default;
            }

            if (wanted == null)
                continue;

            if (!_proto.HasIndex<ShaderPrototype>(wanted))
            {
                if (_missing.Add(wanted))
                    Log.Error($"Tile shader '{wanted}' does not exist."); // Fix your fucking prototype

                continue;
            }

            if (owned.Instance != null && sprite.PostShader == owned.Instance)
                continue;

            // Something else already has a postshader on the entity. Not our problem!
            if (sprite.PostShader != null && sprite.PostShader != owned.Instance)
                continue;

            var instance = owned.Instance ?? _proto.Index<ShaderPrototype>(wanted).InstanceUnique();
            _instances[uid] = (wanted, instance);

            sprite.PostShader = instance;

            sprite.RaiseShaderEvent = true;
        }
    }

    private void OnTerminating(Entity<TileShaderTargetComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_instances.Remove(ent.Owner, out var owned))
            return;

        owned.Instance.Dispose();
    }

    private void Release(EntityUid uid, SpriteComponent sprite, ShaderInstance instance)
    {
        if (sprite.PostShader == instance)
        {
            sprite.PostShader = null;
            sprite.RaiseShaderEvent = false;
        }

        _instances.Remove(uid);
        instance.Dispose();
    }

    private string? GetTileShader(TransformComponent xform)
    {
        if (xform.GridUid == null || !_turf.TryGetTileRef(xform.Coordinates, out var tileRef))
            return null;

        return _turf.GetContentTileDefinition(tileRef.Value).EntityShader;
    }

}
