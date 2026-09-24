namespace Content.Server._Mono.StationEvents;

[RegisterComponent, Access(typeof(WarLevelRule))]
public sealed partial class WarLevelRuleComponent : Component
{
    /// <summary>
    /// War level to set it to. True for HOT, false for COLD.
    /// </summary>
    [DataField]
    public bool WarLevel = false;
}
