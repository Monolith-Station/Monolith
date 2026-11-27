using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// This is used for an event that spawns cargo
/// somewhere random on the station.
/// </summary>
[RegisterComponent, Access(typeof(BluespaceCargoRule))]
public sealed partial class BluespaceCargoRuleComponent : Component
{
    [DataField]
    public EntProtoId SpawnerPrototype = "RandomCargoSpawner";

    [DataField]
    public bool RequireSafeAtmosphere = false;

    [DataField]
    public EntProtoId FlashPrototype = "EffectFlashBluespace";

    [DataField]
    public int MinimumSpawns = 1;

    [DataField]
    public int MaximumSpawns = 3;
}
