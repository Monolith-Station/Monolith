using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Component for the Ironman trait.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IronmanComponent : Component
{
    [DataField]
    public bool BlockWithdraw = true;

    [DataField]
    public bool BlockDeposit = false;

    /// <summary>
    /// If not null, what stack type can bypass our deposit block.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype>? BlockBypassStack = null;
}
