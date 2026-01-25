using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Traits.Conditions;

/// <summary>
/// Condition that checks if the player is a member of a specific faction.
/// Use Invert = true to check if the player is NOT in the faction.
/// </summary>
public sealed partial class InFactionCondition : BaseTraitCondition
{
    /// <summary>
    /// The faction prototype ID to check for.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction = string.Empty;

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.JobId))
            return false;

        if (!ctx.Proto.HasIndex(Faction))
            return false;

        if (!ctx.EntMan.TryGetComponent<NpcFactionMemberComponent>(ctx.Player, out var factionMemberComponent))
            return false;

        var playerFactions = factionMemberComponent.Factions;
        return playerFactions.Contains(Faction);
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc, int depth)
    {
        // TODO: Add player-friendly names to factions
        var tooltip = Invert
            ? loc.GetString("trait-condition-faction-not", ("faction", Faction))
            : loc.GetString("trait-condition-faction-is", ("faction", Faction));

        return new string(' ', depth * 2) + "- " + tooltip + Environment.NewLine;
    }
}
