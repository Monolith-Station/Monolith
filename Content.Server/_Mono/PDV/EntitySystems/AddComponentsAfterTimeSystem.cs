using Content.Server._Mono.PDV.Components;
using Content.Shared._Mono.Company;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mono.PDV.EntitySystems;

public sealed partial class AddComponentsAfterTimeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddComponentsAfterTimeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<AddComponentsAfterTimeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.SelectionTime = _timing.CurTime + TimeSpan.FromMinutes(_random.Next(ent.Comp.MinSelectionTime.Minutes, ent.Comp.MaxSelectionTime.Minutes));
        if (!_random.Prob(ent.Comp.SelectionProbability) || TryComp<CompanyComponent>(ent, out var company) && ent.Comp.ExcludedCompanies.Contains(company.CompanyName))
            RemComp<AddComponentsAfterTimeComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<AddComponentsAfterTimeComponent>();
        while (query.MoveNext(out var ent, out var comp))
        {
            if (comp.SelectionTime > curTime)
                continue;

            EntityManager.AddComponents(ent, comp.AddComponentsOnSelection, false);

            RemComp<AddComponentsAfterTimeComponent>(ent);
        }
    }
}