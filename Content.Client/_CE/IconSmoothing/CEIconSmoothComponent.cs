using Robust.Shared.GameObjects;

namespace Content.Client._CE.IconSmoothing;

/// <summary>
///     Tile-based icon smoothing: corners take their RSI from the adjacent tile's CEiconSmoothSprite.
/// </summary>
[RegisterComponent]
public sealed partial class CEIconSmoothComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("enabled")]
    public bool Enabled = true;

    public (EntityUid?, Vector2i)? LastPosition;

    /// <summary>
    ///     We will smooth with other entities that share the same key.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("key")]
    public string? SmoothKey { get; private set; }

    /// <summary>
    ///     Additional keys to smooth with.
    /// </summary>
    [DataField]
    public List<string> AdditionalKeys = new();

    /// <summary>
    ///     Used by <see cref="CEIconSmoothSystem"/> to reduce redundant updates.
    /// </summary>
    internal int UpdateGeneration { get; set; }
}
