using Content.Shared.Traits;
using Robust.Shared.Prototypes;
using Content.Shared._NF.Cloning;

namespace Content.Server._Mono.Traits;

/// <summary>
/// APPARENTLY, trait components aren't tracked on bodies at all. This is our little tag to know what traits they had throughout the round after spawning.
/// </summary>
[RegisterComponent]
public sealed partial class AppliedTraitsComponent : Component, ITransferredByCloning
{
    [DataField]
    public List<ProtoId<TraitPrototype>> Traits = new();
}