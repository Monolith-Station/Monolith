using Content.Shared.Body.Components;
using Content.Shared.DragDrop;

namespace Content.Shared._CE.Planets.Hellpods;

public sealed partial class CEHellpodHatchSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEHellpodHatchComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<CEHellpodHatchComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        args.CanDrop = args.User == args.Dragged && HasComp<BodyComponent>(args.User) &&
                       _appearance.TryGetData<bool>(ent, CEHellpodVisuals.Open, out var open) && open;
    }
}
