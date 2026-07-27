using Content.Shared.Storage.Components;
using Content.Shared.Whitelist;

namespace Content.Shared._RMC14.Storage;

public sealed partial class RMCStorageSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BlockEntityStorageComponent, InsertIntoEntityStorageAttemptEvent>(OnBlockInsertIntoEntityStorageAttempt);
    }

    private void OnBlockInsertIntoEntityStorageAttempt(Entity<BlockEntityStorageComponent> ent, ref InsertIntoEntityStorageAttemptEvent args)
    {
        if (_entityWhitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, args.Container))
            args.Cancelled = true;
    }
}
