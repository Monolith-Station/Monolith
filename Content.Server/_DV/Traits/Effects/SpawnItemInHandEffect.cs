using Content.Server.Hands.Systems;
using Content.Shared._DV.Traits.Effects;
using Content.Shared.Hands.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._DV.Traits.Effects;

/// <summary>
/// Effect that spawns an item and attempts to place it in the player's hand.
/// If the player cannot hold the item, it is spawned at their feet.
/// </summary>
public sealed partial class SpawnItemInHandEffect : BaseTraitEffect
{
    /// <summary>
    /// The entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item = string.Empty;

    public override void Apply(TraitEffectContext ctx)
    {
        var sawmill = ctx.LogMan.GetSawmill("traits");

        if (!ctx.EntMan.TrySystem<HandsSystem>(out var handsSys))
            return;

        if (!ctx.EntMan.TryGetComponent<HandsComponent>(ctx.Player, out var hands))
        {
            sawmill.Warning("Cannot spawn trait item: player has no hands component");
            return;
        }

        var coords = ctx.Transform.Coordinates;
        var item = ctx.EntMan.SpawnEntity(Item, coords);

        if (!handsSys.TryPickup(ctx.Player, item, checkActionBlocker: false, handsComp: hands))
            sawmill.Debug($"Could not pick up trait item {Item}, leaving at feet");
    }
}
