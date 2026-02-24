using JetBrains.Annotations;

namespace Content.Server._Mono.Grid.Modifiers;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class GridModifier
{
    protected string _id => GetType().Name;

    [DataField]
    public string Comp = "Transform";

    public abstract void Modify(EntityUid uid, MetaDataComponent meta, TransformComponent xform, EntityManager system);

}

