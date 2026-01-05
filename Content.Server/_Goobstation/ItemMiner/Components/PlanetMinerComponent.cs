namespace Content.Shared._Goobstation.ItemMiner;

/// <summary>
/// Makes an ItemMinerComponent require being on a planet to work.
/// </summary>
[RegisterComponent]
public sealed partial class PlanetMinerComponent : Component
{
    /// <summary>
    /// Whether to also require the planet to be an expedition planet.
    /// </summary>
    [DataField]
    public bool RequireExpedition = false;

    /// <summary>
    /// Whether to require to be exposed to the planet's ground.
    /// If false, also supports being on lattice on grids.
    /// </summary>
    [DataField]
    public bool RequireGround = true;
}
