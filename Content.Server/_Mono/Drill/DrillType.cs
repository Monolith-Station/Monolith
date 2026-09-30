using JetBrains.Annotations;

namespace Content.Server._Mono.Drill;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class DrillType
{
    protected string _id => GetType().Name;
    public abstract void Drill(EntityUid drilled, EntityUid drill, ShipDrillSystem shipDrill, EntityManager manager);
}
