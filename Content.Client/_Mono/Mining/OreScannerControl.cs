using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._Mono.Mining;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Mono.Mining;

public sealed class OreScannerControl : ShuttleNavControl
{
    private OreDepositScan[] _deposits = Array.Empty<OreDepositScan>();
    private EntityUid? _surface;
    private readonly Dictionary<Vector2i, int> _cells = new();
    private readonly List<(Box2 Bounds, int Deposit)> _spans = new();
    private readonly List<(Vector2 Start, Vector2 End, int Deposit)> _edges = new();
    private readonly Vector2[] _quad = new Vector2[4];
    private readonly SharedTransformSystem _transform;
    private bool _rasterDirty = true;
    private float _tileSize;
    private Matrix3x2 _viewToTerrain;
    private bool _hasView;
    public Action<OreDepositScan?>? OnHover;

    public OreScannerControl() : base(OreScannerComponent.ScanRange, OreScannerComponent.ScanRange, OreScannerComponent.ScanRange)
    {
        _transform = EntManager.System<SharedTransformSystem>();
        _angleFollow = false;
        Draggable = false;
    }

    public void UpdateScan(OreScannerState state)
    {
        var surface = EntManager.GetEntity(state.Surface);
        _rasterDirty |= _surface != surface || !SameGeometry(state.Deposits);
        _deposits = state.Deposits;
        _surface = surface;
        SetMatrix(EntManager.GetCoordinates(state.Navigation.Coordinates), Angle.Zero);
        WorldMinRange = WorldMaxRange = ActualRadarRange = WorldRange = state.Navigation.MaxRange;
    }

    private bool SameGeometry(OreDepositScan[] deposits)
    {
        if (deposits.Length != _deposits.Length)
            return false;
        for (var i = 0; i < deposits.Length; i++)
        {
            if (!deposits[i].Boundary.AsSpan().SequenceEqual(_deposits[i].Boundary))
                return false;
        }
        return true;
    }

    protected override void FrameUpdate(FrameEventArgs args) { }
    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
            args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
            args.Handle();
    }

    private void Rasterize(float tileSize)
    {
        _tileSize = tileSize;
        _rasterDirty = false;
        _cells.Clear();
        _spans.Clear();
        _edges.Clear();
        for (var i = 0; i < _deposits.Length; i++)
        {
            var boundary = _deposits[i].Boundary;
            if (boundary.Length == 0)
                continue;
            var min = boundary[0] / tileSize;
            var max = min;
            foreach (var point in boundary)
            {
                min = Vector2.Min(min, point / tileSize);
                max = Vector2.Max(max, point / tileSize);
            }
            var left = (int)MathF.Floor(min.X);
            var right = (int)MathF.Ceiling(max.X);
            for (var y = (int)MathF.Floor(min.Y); y < (int)MathF.Ceiling(max.Y); y++)
            {
                var start = left;
                var inSpan = false;
                for (var x = left; x <= right; x++)
                {
                    var inside = x < right && OreDepositGeometry.Contains(boundary, new Vector2(x + 0.5f, y + 0.5f) * tileSize);
                    if (inside)
                    {
                        _cells[new Vector2i(x, y)] = i;
                        if (!inSpan)
                            start = x;
                    }
                    else if (inSpan)
                        _spans.Add((new Box2(new Vector2(start, y) * tileSize, new Vector2(x, y + 1) * tileSize), i));
                    inSpan = inside;
                }
            }
        }
        foreach (var (cell, deposit) in _cells)
        {
            AddEdge(cell, deposit, new Vector2i(-1, 0), Vector2.Zero, Vector2.UnitY);
            AddEdge(cell, deposit, new Vector2i(1, 0), Vector2.UnitX, Vector2.One);
            AddEdge(cell, deposit, new Vector2i(0, -1), Vector2.Zero, Vector2.UnitX);
            AddEdge(cell, deposit, new Vector2i(0, 1), Vector2.UnitY, Vector2.One);
        }
    }

    private void AddEdge(Vector2i cell, int deposit, Vector2i neighbor, Vector2 start, Vector2 end)
    {
        if (_cells.TryGetValue(cell + neighbor, out var adjacent) && adjacent == deposit)
            return;
        _edges.Add((((Vector2)cell + start) * _tileSize, ((Vector2)cell + end) * _tileSize, deposit));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawBacking(handle);
        _hasView = false;
        if (_coordinates is not { } coordinates || _surface is not { } surface ||
            !EntManager.TryGetComponent<MapGridComponent>(surface, out var grid))
        {
            OnHover?.Invoke(null);
            return;
        }
        if (_rasterDirty || _tileSize != grid.TileSize)
            Rasterize(grid.TileSize);
        var position = _transform.ToMapCoordinates(coordinates).Position;
        var worldToView = Matrix3x2.CreateTranslation(-position) *
            Matrix3x2.CreateScale(MinimapScale, -MinimapScale) * Matrix3x2.CreateTranslation(MidPointVector);
        DrawTerrain(handle, worldToView, position, surface);
        var terrainToView = _transform.GetWorldMatrix(surface) * worldToView;
        if (!Matrix3x2.Invert(terrainToView, out var viewToTerrain))
            return;
        _viewToTerrain = viewToTerrain;
        _hasView = true;
        var mouse = Vector2.Transform(_lastMousePos * UIScale, viewToTerrain) / _tileSize;
        var cell = new Vector2i((int)MathF.Floor(mouse.X), (int)MathF.Floor(mouse.Y));
        var hovered = _isMouseInside && _cells.TryGetValue(cell, out var index) ? index : -1;
        foreach (var (bounds, deposit) in _spans)
        {
            var scan = _deposits[deposit];
            var alpha = scan.Color.A * (scan.Remaining == 0 ? 0.2f : 1f);
            _quad[0] = Vector2.Transform(bounds.BottomLeft, terrainToView);
            _quad[1] = Vector2.Transform(bounds.BottomRight, terrainToView);
            _quad[2] = Vector2.Transform(bounds.TopRight, terrainToView);
            _quad[3] = Vector2.Transform(bounds.TopLeft, terrainToView);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _quad, scan.Color.WithAlpha(alpha));
        }
        if (hovered >= 0)
        {
            foreach (var (start, end, deposit) in _edges)
            {
                if (deposit == hovered)
                    handle.DrawLine(Vector2.Transform(start, terrainToView), Vector2.Transform(end, terrainToView), Color.White);
            }
        }
        handle.DrawCircle(MidPointVector, 3f * UIScale, Color.Lime);
        OnHover?.Invoke(hovered >= 0 ? _deposits[hovered] : null);
    }
}
