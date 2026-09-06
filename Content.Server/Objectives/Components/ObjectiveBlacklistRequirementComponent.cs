using Content.Server.Objectives.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.Objectives.Components;

/// <summary>
/// Requires that the objective entity has no blacklisted components.
/// Lets you check for incompatible objectives.
/// </summary>
[RegisterComponent, Access(typeof(ObjectiveBlacklistRequirementSystem))]
public sealed partial class ObjectiveBlacklistRequirementComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist Blacklist = new();

    /// <summary>
    /// Mono - Blacklisted companies.
    /// </summary>
    [DataField("blacklistedCompanies"), ViewVariables(VVAccess.ReadWrite)]
    public List<ProtoId<CompanyPrototype>> BlacklistedCompanies = [];
}
