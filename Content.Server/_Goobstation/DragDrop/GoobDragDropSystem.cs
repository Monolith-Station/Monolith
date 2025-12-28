using Content.Server.Construction.Components;
using Content.Shared._Goobstation.DragDrop;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;

namespace Content.Server._Goobstation.DragDrop;

public sealed partial class GoobDragDropSystem : SharedGoobDragDropSystem
{
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConstructionComponent, DragDropTargetEvent>(OnDragDropConstruction);
        SubscribeLocalEvent<DragDropTargetableComponent, DragDropTargetEvent>(OnDragDropTargetable);
    }

    // this is cursed but making construction system code handle DragDropTargetEvent would be even more cursed
    // if it works it works
    private void OnDragDropConstruction(Entity<ConstructionComponent> ent, ref DragDropTargetEvent args)
    {
        OnDragDrop(ent, ref args);
    }

    private void OnDragDropTargetable(Entity<DragDropTargetableComponent> ent, ref DragDropTargetEvent args)
    {
        OnDragDrop(ent, ref args);
    }
}
