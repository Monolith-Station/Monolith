using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server.Access.Components;

[RegisterComponent]
public sealed partial class PresetIdCardComponent : Component
{
    [DataField("job")]
    public ProtoId<JobPrototype>? JobName;

    [DataField("name")]
    public string? IdName;

    // Mono start
    [DataField, AutoNetworkedField]
    public ProtoId<JobIconPrototype>? IconOverride;

    [DataField, AutoNetworkedField]
    public string? TitleOverride;

    [DataField, AutoNetworkedField]
    public ProtoId<JobPrototype>? ManifestJob;
}
