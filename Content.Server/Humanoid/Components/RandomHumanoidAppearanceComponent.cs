using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Server.CharacterAppearance.Components;

[RegisterComponent]
public sealed partial class RandomHumanoidAppearanceComponent : Component
{
    [DataField("randomizeName")] public bool RandomizeName = true;

    [DataField] public Sex? Sex = null;  /// Forge-Change Begin

    [DataField] public Gender? Gender = null;

    [DataField] public int? Age = null;

    [DataField] public Color? SkinColor = null;  /// Forge-Change End

    [DataField("randomizeHair")] public bool RandomizeHair = true;
    /// <summary>
    /// After randomizing, sets the hair style to this, if possible
    /// </summary>
    [DataField] public string? Hair = null;
}
