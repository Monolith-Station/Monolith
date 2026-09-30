using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;

namespace Content.Shared.NightVision;

/// <summary>
/// Shows/hides the <see cref="NightVisionOverlay"/> based on whether the observed
/// entity has a <see cref="NightVisionComponent"/> equipped.
/// </summary>
public abstract partial class SharedNightVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<NightVisionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NightVisionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<NightVisionComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<NightVisionComponent, GotUnequippedEvent>(OnCompUnequip);
        SubscribeLocalEvent<NightVisionComponent, InventoryRelayedEvent<RefreshNightVisionEvent>>(OnRefreshEquipmentHud);
        SubscribeLocalEvent<NightVisionComponent, RefreshNightVisionEvent>(OnRefreshComponentHud);
        SubscribeLocalEvent<ToggleNightVisionEvent>(OnToggleNightVisionEvent);
    }

    private void OnStartup(Entity<NightVisionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(ent);
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnRemove(Entity<NightVisionComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(ent);
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnCompEquip(Entity<NightVisionComponent> ent, ref GotEquippedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(args.Equipee);
        _actions.AddAction(args.Equipee, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }
    private void OnCompUnequip(Entity<NightVisionComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        ent.Comp.Enabled = false; // mono
        RefreshOverlay(ent);
    }
    protected virtual void OnRefreshEquipmentHud(Entity<NightVisionComponent> ent, ref InventoryRelayedEvent<RefreshNightVisionEvent> args)
    {
        OnRefreshComponentHud(ent, ref args.Args);
    }
    protected virtual void OnRefreshComponentHud(Entity<NightVisionComponent> ent, ref RefreshNightVisionEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        args.Entities.Add(ent);
    }

    private void OnToggleNightVisionEvent(ToggleNightVisionEvent args)
    {
        var ent = args.Action.Comp.Container;

        if (!TryComp<NightVisionComponent>(ent, out var nightVisionComp))
            return;

        SetEnabled(ent.Value, !nightVisionComp.Enabled, args.Performer);
        args.Handled = true;
    }

    /// <param name="ent">The night vision to toggle.</param>
    /// <param name="enabled">Whether to enable or disable.</param>
    /// <param name="viewer">Viewer of the night vision, used to refresh their overlay. If null, assumes the night vision entity is the viewer.</param>
    public void SetEnabled(Entity<NightVisionComponent?> ent, bool enabled, EntityUid? viewer = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);

        RefreshOverlay(viewer ?? ent);
    }

    protected virtual void RefreshOverlay(EntityUid entity) { }
}

[ByRefEvent]
public record struct RefreshNightVisionEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
    public List<Entity<NightVisionComponent>> Entities = new();
}
