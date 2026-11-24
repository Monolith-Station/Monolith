using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Overwatch.Components;

[RegisterComponent]
public sealed partial class JamOverwatchComponent : Component
{
    /// <summary>
    /// The list of factions whose overwatch consoles are not affected by the jamming.
    /// </summary>
    [DataField]
    public List<ProtoId<CompanyPrototype>> ExcludedFactions = [];
}