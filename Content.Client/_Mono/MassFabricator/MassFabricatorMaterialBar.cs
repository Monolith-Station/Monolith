using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.MassFabricator;

public sealed class MassFabricatorMaterialBar : Control
{
    public readonly List<(Color Color, int Amount)> Segments = new();
    public int Capacity;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Capacity <= 0)
            return;

        var left = 0f;
        var total = 0L;
        foreach (var (color, amount) in Segments)
        {
            total += amount;
            var right = MathF.Round(PixelSize.X * Math.Clamp((float) total / Capacity, 0f, 1f));
            handle.DrawRect(new UIBox2(left, 0, right, PixelSize.Y), color);
            left = right;
        }
    }
}
