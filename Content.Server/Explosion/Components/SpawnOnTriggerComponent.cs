using Content.Server.Explosion.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.Explosion.Components;

[RegisterComponent, Access(typeof(TriggerSystem))]
public sealed partial class SpawnOnTriggerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public EntProtoId Proto = string.Empty;
}
