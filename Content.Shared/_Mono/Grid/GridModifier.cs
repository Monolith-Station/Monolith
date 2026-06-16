using JetBrains.Annotations;

namespace Content.Shared._Mono.Grid;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class GridModifier
{
    protected string _id => GetType().Name;

    [DataField]
    public string Comp = "Transform";

    public abstract void Modify(EntityUid gridUid, EntityManager system, IComponentFactory? factory = null);

}

