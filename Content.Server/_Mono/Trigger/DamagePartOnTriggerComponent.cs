using Content.Shared.Body.Part;
using Content.Shared.Damage;

namespace Content.Server.Damage.Components;

[RegisterComponent]
public sealed partial class DamagePartOnTriggerComponent : Component
{
    [DataField("ignoreResistances")] public bool IgnoreResistances;

    [DataField("damage", required: true)]
    public DamageSpecifier Damage = default!;
    public BodyPartType Part = BodyPartType.Torso;
    public bool RequireClothingInSlot = false;
}
