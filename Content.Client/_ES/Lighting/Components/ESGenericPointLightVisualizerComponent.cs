using System.Numerics;
using Robust.Client.GameObjects;

namespace Content.Client._ES.Lighting.Components;

/// <summary>
/// Used to control properties of <see cref="SharedPointLightComponent"/> via appearance data on <see cref="AppearanceComponent"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(ESGenericPointLightVisualizerSystem))]
public sealed partial class ESGenericPointLightVisualizerComponent : Component
{
    /// <summary>
    /// Nested dictionary that maps appearance data keys -> appearance data values -> light data
    /// </summary>
    [DataField(required:true)]
    public Dictionary<Enum, Dictionary<string, ESPointLightData>> Visuals = new();
}

/// <summary>
/// Corresponds with fields on <see cref="SharedPointLightComponent"/>
/// </summary>
[DataDefinition]
public partial record struct ESPointLightData
{
    [DataField] public Color? Color;
    [DataField] public Vector2? Offset;
    [DataField] public float? Energy;
    [DataField] public float? Softness;
    [DataField] public float? Falloff;
    [DataField] public float? CurveFactor;
    [DataField] public bool? CastShadows;
    [DataField] public bool? Enabled;
    [DataField] public float? Radius;
    // BUG: No public setters on current engine version
    // [DataField] public bool? MaskAutoRotate;
    // [DataField] public string? MaskPath;

    public void Apply(Entity<PointLightComponent> ent, SharedPointLightSystem system)
    {
        if (Color.HasValue)
            system.SetColor(ent, Color.Value, ent);
        if (Offset.HasValue)
            ent.Comp.Offset = Offset.Value;
        if (Energy.HasValue)
            system.SetEnergy(ent, Energy.Value, ent);
        if (Softness.HasValue)
            system.SetSoftness(ent, Softness.Value, ent);
        if (Falloff.HasValue)
            system.SetFalloff(ent, Falloff.Value, ent);
        if (CurveFactor.HasValue)
            system.SetCurveFactor(ent, CurveFactor.Value, ent);
        if (CastShadows.HasValue)
            system.SetCastShadows(ent, CastShadows.Value, ent);
        if (Enabled.HasValue)
            system.SetEnabled(ent, Enabled.Value, ent);
        if (Radius.HasValue)
            system.SetRadius(ent, Radius.Value, ent);
    }
}
