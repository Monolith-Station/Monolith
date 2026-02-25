using JetBrains.Annotations;

namespace Content.Shared._Mono.Claws.ClawTypes;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class ClawType
{
    protected string _id => GetType().Name;

    [DataField]
    public bool CanBeCut = true;

    [DataField]
    public LocId ClawsExaminationString;
}
