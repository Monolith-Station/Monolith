using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using System.Collections.Generic;
using Content.Shared.Tag;

namespace Content.Shared.BarricadeBlock;

[RegisterComponent]
public sealed partial class BarricadeBlockComponent : Component
{
    /// <summary>
    /// % chance of blocking a projectile passing overhead
    /// </summary>
    [DataField("blocking")]
    public int Blocking = 66;

    /// <summary>
    /// Can it be used bidirectionally (e.g. sandbags) or only from behind (e.g. crenelated walls)?
    /// </summary>
    [DataField("bidirectional")]
    public bool Bidirectional = true;

    /// <summary>
    /// Can it be used omnidirectionally (e.g. vending machines)
    /// </summary>
    [DataField("omnidirectional")]
    public bool Omnidirectional = true;

    /// <summary>
    /// Distance between the shooter and barricade
    /// </summary>
    [DataField("passthroughdistance")]
    public float PassThroughDistance = 1.5f;

}

/// ported from civ14
