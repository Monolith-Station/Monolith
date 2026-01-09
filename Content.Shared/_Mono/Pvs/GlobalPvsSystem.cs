using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Pvs;

public sealed partial class GlobalPvsSystem : EntitySystem
{
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GlobalPvsComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<GlobalPvsComponent> ent, ref ComponentInit args)
    {
        _pvs.AddGlobalOverride(ent);
    }
}
