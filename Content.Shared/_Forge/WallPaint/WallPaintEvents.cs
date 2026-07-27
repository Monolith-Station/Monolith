using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.WallPaint;

[Serializable, NetSerializable]
public sealed class WallPaintRequestEvent : EntityEventArgs
{
    public NetEntity Target;
    public Color Color;
    public bool Remove;

    public WallPaintRequestEvent(NetEntity target, Color color, bool remove)
    {
        Target = target;
        Color = color;
        Remove = remove;
    }
}
