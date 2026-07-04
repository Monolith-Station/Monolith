namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Makes entities marked this be targeted by ship HTN.
/// </summary>
[RegisterComponent]
public sealed partial class ShipNpcTargetComponent : Component
{
    [DataField]
    public bool NeedPower = false;

    [DataField]
    public NpcTargetGridMode NeedGrid = NpcTargetGridMode.OnGrid;
}

public enum NpcTargetGridMode
{
    OnGrid,
    Either,
    NoGrid
}
