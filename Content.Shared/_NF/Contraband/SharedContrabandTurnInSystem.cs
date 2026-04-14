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

    // Mono: Remove Contraband currencies selectively
    public void HandleContrabandValueByCompany(EntityUid item, EntityUid? actor)
    {
        // Get the company of the person who queued the item. Checks for valid company prototype, as well as an uplink currency attached to the company.
        if (!TryComp<CompanyComponent>(actor, out var company)
            || !_prot.Resolve(company.CompanyName, out var companyProto)
            || companyProto.CompanyUplinkCurrency is not { } currency)
        {
            // Otherwise just remove all contraband rewards.
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
                // For faction members, remove the contra reward of your faction and keep contra rewards of other factions.
                if (valueKey.Id != currency.Id)
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
                    CleanContrabandValueByCompany(ent, currency);
                }
            }
        }
    }
}
