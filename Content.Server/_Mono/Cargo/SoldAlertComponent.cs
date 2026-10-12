using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Cargo;

/// <summary>
///  Snitches over defined radio channel when an entity with this comp is sold.
/// </summary>
[RegisterComponent]
public sealed partial class SoldAlertComponent : Component
{
    /// <summary>
    ///  This is a list.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> Channels = new() { "Common" };

    [DataField]
    public LocId Message = "cargo-sold-alert-default";

    /// <summary>How many items it takes selling at once to trigger.</summary>
    [DataField]
    public int Threshold = 1;

    /// <summary>
    /// Items are counted individually if undefined.
    /// Define a GroupId to count any entities with the same GroupId.
    /// Just be sure to set the same settings accross them.
    /// </summary>
    [DataField]
    public string? GroupId;

    /// <summary>If true, a stack of 5 counts as 5 instead of 1.</summary>
    [DataField]
    public bool CountStacks;
}
