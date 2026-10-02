using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Deathrattle.Components;

[RegisterComponent]
public sealed partial class JamDeathrattlesComponent : Component
{
    /// <summary>
    /// A list of implant prototypes that will be unaffected by this component.
    /// </summary>
    [DataField]
    public List<EntProtoId> Exclusions = [];
}