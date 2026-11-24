namespace Content.Shared.Maps;

public sealed partial class GameMapPrototype
{
    /// <summary>
    /// Allows generated POIs to use the sector origin even when this map's main station occupies it.
    /// </summary>
    [DataField]
    public bool IgnorePoiSeparation { get; private set; }
}
