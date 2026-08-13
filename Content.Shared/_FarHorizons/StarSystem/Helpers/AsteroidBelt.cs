using System.Numerics;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class AsteroidBelt
{
    [ViewVariables] public Vector2 Position;
    [ViewVariables] public Vector2 RadialSize;
    [ViewVariables] public string Shader;
    [ViewVariables] public ProtoId<PlanetPalettePrototype> Palette;

    public AsteroidBelt(StarSystemAsteroidBelt belt, AsteroidBeltTypePrototype proto, Vector2 position)
    {
        Position = position;
        RadialSize = new Vector2(belt.RadiusInner, belt.RadiusOuter);
        Shader = proto.Shader;
        Palette = proto.Palette;
    }
}
