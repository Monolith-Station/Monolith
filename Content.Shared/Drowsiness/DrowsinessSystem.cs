using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared.Drowsiness;

public abstract class SharedDrowsinessSystem : EntitySystem
{
    public static readonly ProtoId<StatusEffectPrototype> DrowsinessKey = "Drowsiness";
}
