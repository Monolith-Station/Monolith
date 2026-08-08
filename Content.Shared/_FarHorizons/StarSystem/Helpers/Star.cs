using System.Numerics;
using System.Text;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class Star
{
    [ViewVariables] public float SolarMass;
    [ViewVariables] public float Luminocity;
    [ViewVariables] public float Radius;
    [ViewVariables] public float Temperature;
    [ViewVariables] public string Shader;
    [ViewVariables(VVAccess.ReadWrite)] public Color Color;
    [ViewVariables] public Vector2 Position;
    [ViewVariables] public string Name = "";
    public const float NAV_PIXEL_SIZE = 500;
    public const float MAP_PIXEL_SIZE = 500;
    public const string STAR_ENTITY = "StarEntity";

    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public Star(float solarMass, Color color, string shader)
    {
        SolarMass = Math.Clamp(solarMass, 0.08f, 8.0f); // Main Sequence stars

        Luminocity = SolarMass < 0.43f
            ? 0.23f * MathF.Pow(SolarMass, 2.3f)
            : SolarMass < 2.0f ? MathF.Pow(SolarMass, 4.0f) : 1.4f * MathF.Pow(SolarMass, 3.5f);

        Radius = SolarMass < 1.66f ? MathF.Pow(SolarMass, 0.9f) : 1.15f * MathF.Pow(SolarMass, 0.6f);

        Temperature = MathF.Pow(Luminocity / MathF.Pow(Radius, 2f), 0.25f) * 5778f;

        Color = color;
        Position = Vector2.Zero;
        Shader = shader;
    }

    public void GenerateName(System.Random rand)
    {
        var letterCount = rand.Next(0, 2) == 0 ? 2 : 3;

        var sb = new StringBuilder();

        for (var i = 0; i < letterCount; i++)
        {
            var letter = UppercaseLetters[rand.Next(UppercaseLetters.Length)];
            sb.Append(letter);
        }

        sb.Append('-');

        var number = rand.Next(100, 999);
        sb.Append(number);

        sb.Append('-');

        var suffix = UppercaseLetters[rand.Next(UppercaseLetters.Length)];
        sb.Append(suffix);

        Name = sb.ToString();
    }

    public string GetPlanetName(int order) => 
        $"{Name} {ToRomanNumeral(order)}";
    
    private string ToRomanNumeral(int order) =>
        order switch
        {
            0 => "I",
            1 => "II",
            2 => "III",
            3 => "IV",
            4 => "V",
            5 => "VI",
            6 => "VII",
            7 => "VIII",
            8 => "IX",
            9 => "X",
            10 => "XI",
            11 => "XII",
            12 => "XIII",
            13 => "XIV",
            14 => "XV",
            15 => "XVI",
            16 => "XVII",
            17 => "XVIII",
            18 => "XIX",
            19 => "XX",
            _ => "??"
        };
}