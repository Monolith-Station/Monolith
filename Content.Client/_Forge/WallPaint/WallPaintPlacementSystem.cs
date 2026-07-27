using System.Numerics;
using Content.Client.Clickable;
using Content.Client.Decals;
using Content.Shared._Forge.WallPaint;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Client._Forge.WallPaint;

public sealed partial class WallPaintPlacementSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private DecalPlacementSystem _decals = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly ClickableEntityComparer _comparer = new();

    private SpriteTreeSystem _spriteTree = default!;
    private ClickableSystem _clickables = default!;
    private EntityQuery<ClickableComponent> _clickQuery;
    private PlacementSnapshot? _previousPlacement;
    private bool _previousDecalsActive;
    private Color _color = WallPaintColor.Clamp(Color.FromHex("#8B0000CC"));
    private bool _active;
    private bool _erase;

    public Color Color => _color;
    public bool Erase => _erase;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EditorPlaceObject, new PointerInputCmdHandler(HandlePaint, outsidePrediction: true))
            .Register<WallPaintPlacementSystem>();

        _spriteTree = EntityManager.System<SpriteTreeSystem>();
        _clickables = EntityManager.System<ClickableSystem>();
        _clickQuery = GetEntityQuery<ClickableComponent>();
    }

    public override void Shutdown()
    {
        Deactivate();
        CommandBinds.Unregister<WallPaintPlacementSystem>();
        base.Shutdown();
    }

    public void SetActive(bool active)
    {
        if (_active == active)
            return;

        _active = active;

        if (_active)
        {
            CaptureSandboxState();
            _placement.Clear();
            _decals.SetActive(false);
            _input.Contexts.SetActiveContext("editor");
        }
        else
        {
            RestoreSandboxState();
        }
    }

    public void Deactivate()
    {
        SetActive(false);
    }

    public void SetColor(Color color)
    {
        _color = WallPaintColor.Clamp(color);
    }

    public void SetErase(bool erase)
    {
        _erase = erase;
    }

    private bool HandlePaint(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (!_active)
            return false;

        var target = GetPaintTarget(coords, uid);
        if (target == null)
            return true;

        RaiseNetworkEvent(new WallPaintRequestEvent(GetNetEntity(target.Value), _color, _erase));
        return true;
    }

    private EntityUid? GetPaintTarget(EntityCoordinates coords, EntityUid uid)
    {
        if (uid.IsValid() && HasComp<PaintableWallComponent>(uid))
            return uid;

        return GetClickablePaintTarget(_transform.ToMapCoordinates(coords));
    }

    private EntityUid? GetClickablePaintTarget(MapCoordinates coordinates)
    {
        if (_eye.CurrentEye == null)
            return null;

        var entities = _spriteTree.QueryAabb(coordinates.MapId, Box2.CenteredAround(coordinates.Position, new Vector2(1, 1)));
        (EntityUid Uid, int DrawDepth, uint RenderOrder, float Bottom)? best = null;

        foreach (var entity in entities)
        {
            if (!_clickQuery.TryGetComponent(entity.Uid, out var component) ||
                !HasComp<PaintableWallComponent>(entity.Uid) ||
                !_clickables.CheckClick((entity.Uid, component, entity.Component, entity.Transform), coordinates.Position, _eye.CurrentEye, out var drawDepth, out var renderOrder, out var bottom))
            {
                continue;
            }

            var candidate = (entity.Uid, drawDepth, renderOrder, bottom);
            if (best == null || _comparer.Compare(candidate, best.Value) < 0)
                best = candidate;
        }

        return best?.Uid;
    }

    private void CaptureSandboxState()
    {
        _previousPlacement = PlacementSnapshot.From(_placement);
        _previousDecalsActive = _decals.IsActive;
    }

    private void RestoreSandboxState()
    {
        var restoredPlacement = _previousPlacement?.Restore(_placement) == true;
        _previousPlacement = null;

        if (_previousDecalsActive)
            _decals.SetActive(true);

        if (!restoredPlacement && !_previousDecalsActive)
            _inputSystem.SetEntityContextActive();

        _previousDecalsActive = false;
    }

    private sealed class ClickableEntityComparer : IComparer<(EntityUid Uid, int DrawDepth, uint RenderOrder, float Bottom)>
    {
        public int Compare(
            (EntityUid Uid, int DrawDepth, uint RenderOrder, float Bottom) x,
            (EntityUid Uid, int DrawDepth, uint RenderOrder, float Bottom) y)
        {
            var cmp = y.DrawDepth.CompareTo(x.DrawDepth);
            if (cmp != 0)
                return cmp;

            cmp = y.RenderOrder.CompareTo(x.RenderOrder);
            if (cmp != 0)
                return cmp;

            cmp = -y.Bottom.CompareTo(x.Bottom);
            if (cmp != 0)
                return cmp;

            return y.Uid.CompareTo(x.Uid);
        }
    }

    private sealed class PlacementSnapshot
    {
        private readonly PlacementInformation? _permission;
        private readonly PlacementHijack? _hijack;
        private readonly bool _eraser;
        private readonly bool _replacement;
        private readonly Direction _direction;
        private readonly bool _mirrored;

        private PlacementSnapshot(
            PlacementInformation? permission,
            PlacementHijack? hijack,
            bool eraser,
            bool replacement,
            Direction direction,
            bool mirrored)
        {
            _permission = permission;
            _hijack = hijack;
            _eraser = eraser;
            _replacement = replacement;
            _direction = direction;
            _mirrored = mirrored;
        }

        public static PlacementSnapshot? From(IPlacementManager placement)
        {
            if (!placement.IsActive && !placement.Eraser)
                return null;

            return new PlacementSnapshot(
                Clone(placement.CurrentPermission),
                placement is PlacementManager manager ? manager.Hijack : null,
                placement.Eraser,
                placement.Replacement,
                placement.Direction,
                placement.Mirrored);
        }

        public bool Restore(IPlacementManager placement)
        {
            if (_permission != null)
                placement.BeginPlacing(_permission, _hijack);

            if (placement.Eraser != _eraser)
                placement.ToggleEraser();

            placement.Replacement = _replacement;
            placement.Direction = _direction;
            placement.Mirrored = _mirrored;
            return _permission != null || _eraser;
        }

        private static PlacementInformation? Clone(PlacementInformation? source)
        {
            if (source == null)
                return null;

            return new PlacementInformation
            {
                MobUid = source.MobUid,
                EntityType = source.EntityType,
                TileType = source.TileType,
                PlacementOption = source.PlacementOption,
                Range = source.Range,
                IsTile = source.IsTile,
                Uses = source.Uses,
                UseEditorContext = source.UseEditorContext,
            };
        }
    }
}
