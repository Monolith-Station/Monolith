using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Shared._Mono.MassFabricator;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorEraseSystem : EntitySystem
{
    [Dependency] private IStateManager _states = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eyes = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MassFabricatorClientSystem _fabricator = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private ClickableSystem _clickable = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    private EntityCoordinates? _start;
    private readonly HashSet<EntityUid> _highlighted = new();
    public UIBox2? Selection { get; private set; }
    public bool Active => _states.CurrentState is MassFabricatorState && !_fabricator.Returning &&
        _fabricator.EraseMode == MassFabricatorEraseMode.Single;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new MassFabricatorEraseOverlay(this));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<MassFabricatorEraseOverlay>();
        base.Shutdown();
    }

    public bool IsHighlighted(EntityUid uid) => _highlighted.Contains(uid);

    public void Clear()
    {
        _start = null;
        Selection = null;
        _highlighted.Clear();
    }

    public void Click(bool area)
    {
        if (!Active)
            return;

        if (area && _start == null)
        {
            var start = _fabricator.GetBuildCursor(_input.MouseScreenPosition, lookUp: false);
            if (start.IsValid(EntityManager))
                _start = start;
            return;
        }

        UpdateSelection();
        if (_highlighted.Count > 0)
            _fabricator.Erase(_highlighted.Take(256).ToArray());
        Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        _highlighted.Clear();
        Selection = null;
        if (!Active || _fabricator.ActiveFabricatorUid is not { } fabricator ||
            _states.CurrentState is not MassFabricatorState state || _players.LocalEntity is not { } user)
        {
            Clear();
            return;
        }

        if (_ui.CurrentlyHovered != state.Viewport.Viewport)
            return;

        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return;

        var cursor = _fabricator.GetBuildCursor(mouse, lookUp: _start == null);
        if (!cursor.IsValid(EntityManager))
            return;

        var map = _transform.ToMapCoordinates(cursor);
        var range = _config.GetCVar(CVars.NetMaxUpdateRange) * 0.5f;
        if (_start is { } start && start.IsValid(EntityManager))
        {
            if (_transform.GetMapId(start) != map.MapId)
                return;

            var first = _eyes.WorldToScreen(_transform.ToMapCoordinates(start).Position);
            Selection = new UIBox2(Vector2.Min(first, mouse.Position), Vector2.Max(first, mouse.Position));
            var projection = state.Viewport.Viewport.GetWorldToScreenMatrix();
            var query = EntityQueryEnumerator<MassFabricatorHologramComponent, SpriteComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var plan, out var sprite, out var xform))
            {
                if (plan.Fabricator != fabricator || xform.MapID != map.MapId || !sprite.Visible ||
                    !_fabricator.CanEraseFromEditor(user, uid, range))
                    continue;

                if (IntersectsSprite(sprite, xform, projection, Selection.Value))
                    _highlighted.Add(uid);
            }
        }
        else
        {
            EntityUid? target = null;
            var priority = int.MinValue;
            var query = EntityQueryEnumerator<MassFabricatorHologramComponent, SpriteComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var plan, out var sprite, out var xform))
            {
                if (plan.Fabricator != fabricator || xform.MapID != map.MapId || !sprite.Visible)
                    continue;

                if (!_clickable.CheckClick((uid, null, sprite, xform), map.Position, _eyes.CurrentEye, out _, out _, out _) ||
                    !_fabricator.CanEraseFromEditor(user, uid, range))
                    continue;

                var candidatePriority = plan.Kind == MassFabricatorBlueprintKind.Entity ? 1 : 0;
                if (candidatePriority <= priority)
                    continue;
                target = uid;
                priority = candidatePriority;
            }

            if (target is { } selected)
                _highlighted.Add(selected);
        }
    }

    private bool IntersectsSprite(SpriteComponent sprite, TransformComponent xform, Matrix3x2 projection, UIBox2 selection)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        var eyeRotation = _eyes.CurrentEye.Rotation;
        var angle = (rotation + eyeRotation).Reduced().FlipPositive();
        var drawRotation = sprite.NoRotation ? -eyeRotation : rotation;
        if (!sprite.NoRotation && sprite.SnapCardinals)
            drawRotation -= angle.RoundToCardinalAngle();

        var matrix = sprite.GetLocalMatrix() * Matrix3Helpers.CreateTransform(position, drawRotation) * projection;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is not SpriteComponent.Layer drawLayer || !_sprites.IsVisible(drawLayer))
                continue;

            var direction = RsiDirection.South;
            if (drawLayer.ActualRsi is { } rsi && rsi.TryGetState(drawLayer.State, out var state))
                direction = SpriteComponent.Layer.GetDirection(state.RsiDirections, angle);

            drawLayer.GetLayerDrawMatrix(direction, out var local);
            var bounds = (local * matrix).TransformBox(
                Box2.CenteredAround(Vector2.Zero, (Vector2) drawLayer.PixelSize / EyeManager.PixelsPerMeter));
            if (selection.Intersects(new UIBox2(new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Top))))
                return true;
        }

        return false;
    }
}
