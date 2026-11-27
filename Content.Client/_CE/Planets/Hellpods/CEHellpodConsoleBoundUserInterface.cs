using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._CE.Planets.Hellpods;

public sealed class CEHellpodConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CEHellpodConsoleWindow? _window;

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not CEHellpodConsoleState drop)
            return;

        if (_window == null)
        {
            _window = this.CreateWindow<CEHellpodConsoleWindow>();
            _window.Chart.SetConsole(Owner);
            _window.Confirm = (center, radius, action) => SendMessage(new CEHellpodConfirmMessage(center, radius, action));
        }

        _window.Apply(drop);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is CEHellpodConfirmRejectedMessage)
            _window?.RejectConfirm();
    }
}

public sealed class CEHellpodChart : ShuttleNavControl
{
    public CEHellpodConsoleState State { get; private set; } = new();
    public Vector2 Center { get; private set; }
    public float Radius { get; private set; } = 24f;
    public int Depth { get; private set; }
    public int EstimatedIntegrity { get; set; } = CEHellpodComponent.FullIntegrity;
    public bool AimLockedLocally { get; set; }
    public bool CanAim => !AimLockedLocally && State.Planet != null && State.ConfirmAt == null;
    private static readonly Color HullColor = Color.Orange;
    private static readonly Color ShieldColor = Color.Cyan;
    private static readonly Color SafeTargetColor = Color.FromHex("#33e633");
    private static readonly Color UnsafeTargetColor = Color.FromHex("#ff3c35");
    private readonly HashSet<BoundKeyFunction> _pan = new();

    private float ChartRadius => MathF.Max(1f, Math.Max(PixelSize.X, PixelSize.Y) / 2f);
    private float ChartScale => ChartRadius / WorldRange;

    private Matrix3x2 WorldToChart => Matrix3x2.CreateTranslation(-Offset) *
                                      Matrix3x2.CreateScale(ChartScale, -ChartScale) *
                                      Matrix3x2.CreateTranslation((Vector2) PixelSize / 2f);

    public CEHellpodChart() : base(24f, 160f, 160f)
    {
        // The shuttle radar fixes its size; this chart fills the space assigned by the console.
        SetSize = new Vector2(float.NaN);
        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        RectClipContent = true;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    public void Apply(CEHellpodConsoleState state)
    {
        if (state.Planet != State.Planet || state.Terrain != State.Terrain)
        {
            Depth = state.GroundDepth;
            Center = Offset = Vector2.Zero;
            WorldRange = 160f;
        }
        State = state;
        if (state.Planet != null)
            WorldRange = MathF.Min(WorldRange, state.Limit);
        Depth = Math.Clamp(Depth, state.MinDepth, state.MaxDepth);
        if (state.ConfirmAt != null)
        {
            Center = state.Center;
            Radius = state.Radius;
        }
        ClampTarget();
        ClampOffset();
    }

    private void ClampTarget()
    {
        var limit = MathF.Max(0f, State.Limit - Radius);
        Center = Vector2.Clamp(Center, new Vector2(-limit), new Vector2(limit));
    }

    private void ClampOffset()
    {
        var viewportHalfSize = (Vector2) PixelSize / (2f * ChartScale);
        var limit = Vector2.Max(Vector2.Zero, new Vector2(State.Limit) - viewportHalfSize);
        Offset = Vector2.Clamp(Offset, -limit, limit);
    }

    public void ChangeDepth(int delta) => Depth = Math.Clamp(Depth + delta, State.MinDepth, State.MaxDepth);

    public void ChangeRadius(int delta)
    {
        if (!CanAim)
            return;
        Radius = Math.Clamp(Radius + delta, CEHellpodConsoleComponent.MinRadius, CEHellpodConsoleComponent.MaxRadius);
        ClampTarget();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        if (State.Planet == null || args.Delta.Y == 0f)
            return;

        if (IoCManager.Resolve<IInputManager>().IsKeyDown(Keyboard.Key.Control))
            ChangeRadius(Math.Sign(args.Delta.Y) * 4);
        else
        {
            var oldRange = WorldRange;
            WorldRange = Math.Clamp(oldRange * MathF.Pow(1.25f, -args.Delta.Y),
                MathF.Min(WorldMinRange, State.Limit), State.Limit);
            var cursor = args.RelativePixelPosition - (Vector2) PixelSize / 2f;
            Offset += new Vector2(cursor.X, -cursor.Y) * ((oldRange - WorldRange) / ChartRadius);
            ClampOffset();
        }

        args.Handle();
    }

    private static bool IsPan(BoundKeyFunction function) =>
        function == EngineKeyFunctions.MoveUp || function == EngineKeyFunctions.MoveDown ||
        function == EngineKeyFunctions.MoveLeft || function == EngineKeyFunctions.MoveRight;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (IsPan(args.Function))
            _pan.Add(args.Function);
        else if (args.Function == ContentKeyFunctions.ShuttleAscend)
            ChangeDepth(1);
        else if (args.Function == ContentKeyFunctions.ShuttleDescend)
            ChangeDepth(-1);
        else if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (CanAim && Matrix3x2.Invert(WorldToChart, out var chartToWorld))
            {
                Center = Vector2.Transform(args.RelativePixelPosition, chartToWorld);
                ClampTarget();
            }
        }
        else
        {
            base.KeyBindDown(args);
            return;
        }
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (IsPan(args.Function) || args.Function == ContentKeyFunctions.ShuttleAscend ||
            args.Function == ContentKeyFunctions.ShuttleDescend || args.Function == EngineKeyFunctions.UIClick)
        {
            _pan.Remove(args.Function);
            args.Handle();
        }
        else
            base.KeyBindUp(args);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!HasKeyboardFocus())
            _pan.Clear();

        var direction = new Vector2(
            (_pan.Contains(EngineKeyFunctions.MoveRight) ? 1 : 0) - (_pan.Contains(EngineKeyFunctions.MoveLeft) ? 1 : 0),
            (_pan.Contains(EngineKeyFunctions.MoveUp) ? 1 : 0) - (_pan.Contains(EngineKeyFunctions.MoveDown) ? 1 : 0));
        Offset += direction * WorldRange * args.DeltaSeconds;
        ClampOffset();
    }

    public (int Shields, bool Hull) GetWarnings()
    {
        var shields = 0;
        var hull = false;
        var start = State.MaxDepth + 1f;
        var ground = State.GroundDepth;
        foreach (var contact in State.Contacts)
        {
            if (contact.Depth + contact.HalfHeight < ground || contact.Depth - contact.HalfHeight > start)
                continue;

            var local = Vector2.Abs((-contact.Rotation).RotateVec(Center - contact.Center));
            if (!contact.Shield)
            {
                var nearest = Vector2.Min(local, contact.Radii);
                hull |= Vector2.DistanceSquared(local, nearest) <= Radius * Radius;
                continue;
            }

            if (contact.HalfHeight <= 0 || contact.Depth > start)
                continue;

            var height = Math.Max(0, ground - contact.Depth) / contact.HalfHeight;
            var radii = contact.Radii * MathF.Sqrt(Math.Max(0, 1 - height * height));
            if (IntersectsEllipse(local, radii, Radius))
                shields++;
        }
        return (shields, hull);
    }

    private static bool IntersectsEllipse(Vector2 center, Vector2 radii, float radius)
    {
        if (radii.X <= 0 || radii.Y <= 0)
            return false;
        if ((center / radii).LengthSquared() <= 1)
            return true;
        if ((center - Vector2.Min(center, radii)).LengthSquared() > radius * radius)
            return false;

        var squares = radii * radii;
        var scaled = radii * center;
        var low = 0f;
        var high = scaled.Length();
        // Find the closest point on the ellipse to the drop circle's center.
        for (var i = 0; i < 16; i++)
        {
            var mid = (low + high) / 2;
            if ((scaled / (squares + new Vector2(mid))).LengthSquared() > 1)
                low = mid;
            else
                high = mid;
        }

        var nearest = squares * center / (squares + new Vector2(high));
        return Vector2.DistanceSquared(center, nearest) <= radius * radius;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawBacking(handle);
        if (State.Planet == null)
            return;

        var worldToView = WorldToChart;
        if (EntManager.TryGetEntity(State.Terrain, out var terrain) &&
            EntManager.TryGetComponent<MapGridComponent>(terrain, out var grid))
            DrawTerrainPreview(handle, worldToView, (terrain.Value, grid), _consoleEntity);

        foreach (var contact in State.Contacts)
        {
            var delta = MathF.Abs(contact.Depth - Depth);
            if (delta > contact.HalfHeight || contact.HalfHeight <= 0f)
                continue;

            var matrix = Matrix3x2.CreateRotation((float) contact.Rotation.Theta) *
                         Matrix3x2.CreateTranslation(contact.Center) * worldToView;
            var radii = contact.Radii;
            if (contact.Shield)
            {
                var height = delta / contact.HalfHeight;
                radii *= MathF.Sqrt(1f - height * height);
                var previous = Vector2.Transform(new Vector2(radii.X, 0f), matrix);
                for (var i = 1; i <= 32; i++)
                {
                    var (sin, cos) = MathF.SinCos(i * MathF.Tau / 32);
                    var next = Vector2.Transform(new Vector2(cos, sin) * radii, matrix);
                    handle.DrawLine(previous, next, ShieldColor);
                    previous = next;
                }
            }
            else
            {
                var a = Vector2.Transform(-radii, matrix);
                var b = Vector2.Transform(new Vector2(radii.X, -radii.Y), matrix);
                var c = Vector2.Transform(radii, matrix);
                var d = Vector2.Transform(new Vector2(-radii.X, radii.Y), matrix);
                handle.DrawLine(a, b, HullColor);
                handle.DrawLine(b, c, HullColor);
                handle.DrawLine(c, d, HullColor);
                handle.DrawLine(d, a, HullColor);
            }
        }
        var target = Vector2.Transform(Center, worldToView);
        var integrity = (float) EstimatedIntegrity / CEHellpodComponent.FullIntegrity;
        var color = integrity < 0.5f
            ? Color.InterpolateBetween(UnsafeTargetColor, Color.Gold, integrity * 2f)
            : Color.InterpolateBetween(Color.Gold, SafeTargetColor, integrity * 2f - 1f);
        handle.DrawCircle(target, Radius * worldToView.M11, color, false);
        var x = MathF.Floor(target.X);
        var y = MathF.Floor(target.Y);
        handle.DrawRect(new UIBox2(x - 4, y, x + 5, y + 1), color);
        handle.DrawRect(new UIBox2(x, y - 4, x + 1, y + 5), color);
    }
}
