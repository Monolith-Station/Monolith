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

    public AsteroidBelt(Vector2 position, Vector2 radialSize, string shader, ProtoId<PlanetPalettePrototype> palette)
    {
        Position = position;
        RadialSize = radialSize;
        Shader = shader;
        Palette = palette;
    }

    public void Expand(Vector2 size) => 
        RadialSize = new Vector2(MathF.Min(RadialSize.X, size.X), MathF.Max(RadialSize.Y, size.Y));
}