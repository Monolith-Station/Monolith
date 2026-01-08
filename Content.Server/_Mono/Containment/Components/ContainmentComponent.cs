using Robust.Shared.Audio;

namespace Content.Server._Mono.Containment.Components;

[RegisterComponent]
public sealed partial class ContainmentComponent : Component
{
    [DataField]
    public float Radius = 2f;

    [DataField]
    public float HealthPenalty = 0.35f;

    [ViewVariables]
    public List<EntityUid?> ActiveEntities = [];

    public TimeSpan NextUpdate = TimeSpan.Zero;
    public float UpdateCooldown = 1f;

    [DataField]
    public SoundSpecifier? RegisterSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
}
