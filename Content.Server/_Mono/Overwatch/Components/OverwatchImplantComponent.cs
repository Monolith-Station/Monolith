using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Overwatch.Components;

[RegisterComponent]
public sealed partial class OverwatchImplantComponent : Component
{
    /// <summary>
    /// Overwatch consoles are associated with a certain company. This implant will make the overwatch console consider you a member of that company.
    /// </summary>
    [DataField]
    public ProtoId<CompanyPrototype> Company = "MD";
}