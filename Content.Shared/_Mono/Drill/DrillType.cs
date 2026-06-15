using JetBrains.Annotations;

namespace Content.Shared._Mono.Drill;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class DrillType
{
    protected string _id => GetType().Name;
    public abstract void Drill(EntityUid gridUid, EntityManager system, IComponentFactory? factory = null);
}
