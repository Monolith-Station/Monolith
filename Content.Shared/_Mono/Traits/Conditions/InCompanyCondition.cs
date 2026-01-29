using Content.Shared._DV.Traits.Conditions;
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Traits.Conditions;

/// <summary>
/// Condition that checks if the player is a member of a specific company.
/// Use Invert = true to check if the player is NOT in the company.
/// </summary>
public sealed partial class InCompanyCondition : BaseTraitCondition
{
    /// <summary>
    /// The company name to check for.
    /// </summary>
    [DataField(required: true)]
    public string CompanyName = string.Empty; // What do you MEAN you can't check the ID of a player's company :sob:

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (!ctx.EntMan.TryGetComponent<CompanyComponent>(ctx.Player, out var company))
            return false;

        return string.Equals(company.CompanyName, CompanyName);
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc, int depth)
    {
        var tooltip = Invert
            ? loc.GetString("trait-condition-company-not", ("company", CompanyName))
            : loc.GetString("trait-condition-company-is", ("company", CompanyName));

        return new string(' ', depth * 2) + "- " + tooltip + Environment.NewLine;
    }
}
