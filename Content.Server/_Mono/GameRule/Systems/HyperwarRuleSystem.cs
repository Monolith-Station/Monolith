using Content.Server._Mono.GameRule.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Lathe;
using Content.Server.StoreDiscount.Systems;
using Content.Server.Worldgen.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Lathe;
using Content.Shared.Store.Components;
using Robust.Shared.Configuration;

namespace Content.Server._Mono.GameRule.Systems;

public sealed partial class HyperwarRuleSystem : GameRuleSystem<HyperwarRuleComponent>
{
    [Dependency] private LatheSystem _lathe = default!;
    [Dependency] private IConfigurationManager _confMan = default!;
    [Dependency] private WorldgenConfigSystem _worldgen = default!;

    private float _latheMaterialUseMultiplier = 1f;
    private float _latheTimeMultiplier = 1f;

    public bool HyperwarActive;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, ComponentStartup>(OnLatheInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    protected override void Added(EntityUid uid, HyperwarRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        _confMan.SetCVar("worldgen.worldgen_config", component.Worldgen.Id);

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, HyperwarRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _latheMaterialUseMultiplier = component.LatheMaterialUseMultiplier;
        _latheTimeMultiplier = component.LatheTimeMultiplier;

        HyperwarActive = true;

        base.Started(uid, component, gameRule, args);

        var lathes = AllEntityQuery<LatheComponent>();

        while (lathes.MoveNext(out var ent, out _))
        {
            _lathe.MultiplyLatheMultipliers(ent, _latheMaterialUseMultiplier, _latheTimeMultiplier);
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _confMan.SetCVar("worldgen.worldgen_config", "MonoDefault");
        HyperwarActive = false;
    }

    private void OnLatheInit(Entity<LatheComponent> ent, ref ComponentStartup args)
    {
        if (!HyperwarActive)
            return;

        _lathe.MultiplyLatheMultipliers(ent.Owner, _latheMaterialUseMultiplier, _latheTimeMultiplier);
    }
}
