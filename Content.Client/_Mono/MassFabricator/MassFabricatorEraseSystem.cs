using System.Linq;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
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
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private MassFabricatorClientSystem _fabricator = default!;
    [Dependency] private IOverlayManager _overlays = default!;

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
            _start = _transform.ToCoordinates(_eyes.PixelToMap(_input.MouseScreenPosition));
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
        if (!Active || _states.CurrentState is not MassFabricatorState state || _players.LocalEntity is not { } user)
        {
            Clear();
            return;
        }

        if (_ui.CurrentlyHovered != state.Viewport.Viewport)
            return;

        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return;

        var map = _eyes.PixelToMap(mouse);
        var range = _config.GetCVar(CVars.NetMaxUpdateRange) * 0.5f;
        if (_start is { } start && start.IsValid(EntityManager))
        {
            var first = _eyes.WorldToScreen(_transform.ToMapCoordinates(start).Position);
            Selection = new UIBox2(System.Numerics.Vector2.Min(first, mouse.Position), System.Numerics.Vector2.Max(first, mouse.Position));
            var query = EntityQueryEnumerator<MassFabricatorHologramComponent, SpriteComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
            {
                if (xform.MapID != map.MapId || !sprite.Visible ||
                    !_examine.InRangeUnOccluded(user, xform.Coordinates, range))
                    continue;

                var (position, rotation) = _transform.GetWorldPositionRotation(xform);
                var bounds = sprite.CalculateRotatedBoundingBox(position, rotation, _eyes.CurrentEye.Rotation).CalcBoundingBox();
                var firstCorner = _eyes.WorldToScreen(bounds.BottomLeft);
                var secondCorner = _eyes.WorldToScreen(bounds.TopRight);
                var screenBounds = new UIBox2(System.Numerics.Vector2.Min(firstCorner, secondCorner),
                    System.Numerics.Vector2.Max(firstCorner, secondCorner));
                if (Selection.Value.Intersects(screenBounds))
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
                if (xform.MapID != map.MapId || !sprite.Visible)
                    continue;

                var (position, rotation) = _transform.GetWorldPositionRotation(xform);
                var bounds = sprite.CalculateRotatedBoundingBox(position, rotation, _eyes.CurrentEye.Rotation);
                if (!bounds.Contains(map.Position) || !_examine.InRangeUnOccluded(user, xform.Coordinates, range))
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
}
