using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Spider;

public abstract partial class SharedSpiderSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<SpiderWebObjectComponent, ComponentStartup>(OnWebStartup);
    }

    private void OnInit(EntityUid uid, SpiderComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.Action, component.WebAction, uid);
    }

    private void OnWebStartup(EntityUid uid, SpiderWebObjectComponent component, ComponentStartup args)
    {
        // TODO dont use this. use some general random appearance system
        _appearance.SetData(uid, SpiderWebVisuals.Variant, _robustRandom.Next(1, 3));
    }
}
