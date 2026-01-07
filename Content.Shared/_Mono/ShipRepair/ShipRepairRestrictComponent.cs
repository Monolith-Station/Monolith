using Content.Shared.Whitelist;

namespace Content.Shared._Mono.ShipRepair;

/// <summary>
/// Add to grid to restrict tools that can repair it.
/// </summary>
[RegisterComponent]
public sealed partial class ShipRepairRestrictComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist ToolWhitelist = new();
}
