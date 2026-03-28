namespace Content.Shared._NF.Power.Components;

[RegisterComponent]
public sealed partial class GaslockPowerBridgeComponent : Component
{
    [DataField("hvInternalNode")]
    public string HvInternalNode = "hvInternal";

    [DataField("mvInternalNode")]
    public string MvInternalNode = "mvInternal";

    [DataField("lvInternalNode")]
    public string LvInternalNode = "lvInternal";

    [DataField("hvDockNode")]
    public string HvDockNode = "hvDock";

    [DataField("mvDockNode")]
    public string MvDockNode = "mvDock";

    [DataField("lvDockNode")]
    public string LvDockNode = "lvDock";
}
