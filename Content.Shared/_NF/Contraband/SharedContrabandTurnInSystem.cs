using Content.Shared._Mono.Company;
using Content.Shared.Contraband;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Contraband;

[NetSerializable, Serializable]
public enum ContrabandPalletConsoleUiKey : byte
{
    Contraband
}

public abstract class SharedContrabandTurnInSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prot = default!;
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

    // Mono: Remove Contraband currencies
    public void ClearContrabandValueByCompany(EntityUid item, EntityUid actor)
    {
        // Get the company of the person who queued the item. Checks for valid company prototype, as well as an uplink currency.
        if (!TryComp<CompanyComponent>(actor, out var company)
            || !_prot.Resolve<CompanyPrototype>(company.CompanyName, out var companyProto)
            || companyProto.CompanyUplinkCurrency is not { } currency)
        {
            ClearContrabandValue(item);
            return;
        }


        // Clear contraband value for printed items
        if (TryComp<ContrabandComponent>(item, out var contraband))
        {
            foreach (var valueKey in contraband.TurnInValues.Keys)
            {
                if (valueKey == currency)
                    continue;
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
}
