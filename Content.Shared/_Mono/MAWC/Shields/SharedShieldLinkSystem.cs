using Content.Shared._Mono.PersonalShield;

namespace Content.Shared._Mono.MAWC.Shields;

public sealed class SharedShieldLinkSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShieldLinkSourceComponent, GetPersonalShieldStatsEvent>(OnGetSourceShieldStats);
        SubscribeLocalEvent<ShieldLinkReceiverComponent, GetPersonalShieldStatsEvent>(OnGetReceiverShieldStats);
    }

    private void OnGetSourceShieldStats(Entity<ShieldLinkSourceComponent> ent, ref GetPersonalShieldStatsEvent args)
    {
        args.MaxCharge += GetMaxChargeBonus(ent.Comp);
    }

    private void OnGetReceiverShieldStats(Entity<ShieldLinkReceiverComponent> ent, ref GetPersonalShieldStatsEvent args)
    {
        foreach (var sourceUid in ent.Comp.LinkedSources)
        {
            if (TryComp<ShieldLinkSourceComponent>(sourceUid, out var source) &&
                source.LinkedShields.Contains(ent.Owner))
            {
                args.MaxCharge += GetMaxChargeBonus(source);
            }
        }
    }

    private static float GetMaxChargeBonus(ShieldLinkSourceComponent source)
    {
        return source.ShieldBonusPerLink * source.LinkedShields.Count;
    }
}
