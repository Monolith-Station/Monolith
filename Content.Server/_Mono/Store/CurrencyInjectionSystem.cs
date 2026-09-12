using Content.Server._Mono.Store.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Store.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.FixedPoint;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Store;

public sealed partial class CurrencyInjectionSystem : EntitySystem
{
    [Dependency] private StoreSystem _store = default!;
    [Dependency] private RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void InjectCurrency(ProtoId<CompanyPrototype> company, Dictionary<string, FixedPoint2> amount)
    {
        var query = EntityQueryEnumerator<CurrencyInjectionTargetComponent, StoreComponent>();
        while (query.MoveNext(out var uid, out var comp, out var store))
        {
            if (comp.Company != company)
                continue;
            _store.TryAddCurrency(amount, uid, store);
            foreach (var currency in amount)
            {
                _radio.SendRadioMessage(uid, Loc.GetString(comp.InjectionAlert, ("amount", currency.Value)), comp.RadioChannel, uid);
            }
        }
    }
}