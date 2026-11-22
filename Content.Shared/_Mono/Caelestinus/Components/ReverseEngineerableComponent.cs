using Content.Shared._Mono.Company;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Caelestinus.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReverseEngineerableComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<ReverseEngineeringCategoryPrototype> Category;

    /// <summary>
    /// If defined, printed items will be of no value unless originating from this company.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype>? OriginCompany;

    /// <summary>
    /// How much is this item worth in its own category?
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Value = 1;

    /// <summary>
    /// How much is this item worth to total progress? Set to 0 for massproducible-by-anyone items like MS-250 or MS-500 shields, and as a rule of thumb keep it similar to DC prices for said item
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ProgressValue = 15;
}