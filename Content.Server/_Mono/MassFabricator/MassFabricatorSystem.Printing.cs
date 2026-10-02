using Content.Shared._Mono.MassFabricator;

namespace Content.Server._Mono.MassFabricator;

public sealed partial class MassFabricatorSystem
{
    private void DeleteOwnedPlans(Entity<MassFabricatorComponent> gun)
    {
        foreach (var plan in gun.Comp.Plans)
            if (Exists(plan))
                QueueDel(plan);
    }
}
