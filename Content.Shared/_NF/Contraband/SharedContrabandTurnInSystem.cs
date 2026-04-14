using Content.Shared._Mono.Company; // Mono
using Content.Shared.Contraband;
using Content.Shared.Store; // Mono
using Robust.Shared.Containers;
using Robust.Shared.Prototypes; // Mono
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Contraband;

[NetSerializable, Serializable]
public enum ContrabandPalletConsoleUiKey : byte
{
    Contraband
}

public abstract class SharedContrabandTurnInSystem : EntitySystem
{
    // Mono Begin
    [Dependency] private readonly IPrototypeManager _prot = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("Contraband");
    }
    // Mono End

    public void ClearContrabandValue(EntityUid item)
    {
        // Clear contraband value for printed items
        if (TryComp<ContrabandComponent>(item, out var contraband))
        {
            foreach (var valueKey in contraband.TurnInValues.Keys)
            {
                contraband.TurnInValues[valueKey] = 0;
            }
        }

        // Recurse into contained entities
        if (TryComp<ContainerManagerComponent>(item, out var containers))
        {
            foreach (var container in containers.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    ClearContrabandValue(ent);
                }
            }
        }
    }

    // Mono: Remove Contraband currencies selectively
    public void HandleContrabandValueByCompany(EntityUid item, EntityUid? actor)
    {
        // Get the company of the person who queued the item. Checks for valid company prototype, as well as an uplink currency attached to the company.
        if (!TryComp<CompanyComponent>(actor, out var company)
            || !_prot.Resolve(company.CompanyName, out var companyProto)
            || companyProto.CompanyUplinkCurrency is not { } currency)
        {
            _sawmill.Debug($"Clearing all Contraband for {item}");
            ClearContrabandValue(item);
            return;
        }

        CleanContrabandValueByCompany(item, currency);
    }

    private void CleanContrabandValueByCompany(EntityUid item, ProtoId<CurrencyPrototype> currency)
    {
        // Clear contraband value for printed items
        if (TryComp<ContrabandComponent>(item, out var contraband))
        {
            foreach (var valueKey in contraband.TurnInValues.Keys)
            {
                // For faction members, if the faction currency matches the contraband value, keep its value.
                if (valueKey.Id != currency.Id)
                {
                    _sawmill.Debug($"Ignoring contraband removal for {item} of faction currency {valueKey} from company {currency}");
                    continue;
                }
                contraband.TurnInValues[valueKey] = 0;
            }
        }

        // Recurse into contained entities
        if (TryComp<ContainerManagerComponent>(item, out var containers))
        {
            foreach (var container in containers.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    CleanContrabandValueByCompany(ent, currency);
                }
            }
        }
    }
}
