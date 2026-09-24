using Content.Client.Overlays;
using Content.Shared._White.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client._White.Overlays;

public sealed partial class WWDPNightVisionSystem : EquipmentHudSystem<WWDPNightVisionComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private ILightManager _lightManager = default!;

    private BaseSwitchableOverlay<WWDPNightVisionComponent> _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WWDPNightVisionComponent, SwitchableOverlayToggledEvent>(OnToggle);

        _overlay = new BaseSwitchableOverlay<WWDPNightVisionComponent>();
    }

    protected override void OnRefreshComponentHud(Entity<WWDPNightVisionComponent> ent,
        ref RefreshEquipmentHudEvent<WWDPNightVisionComponent> args)
    {
        if (!ent.Comp.IsEquipment)
            base.OnRefreshComponentHud(ent, ref args);
    }

    protected override void OnRefreshEquipmentHud(Entity<WWDPNightVisionComponent> ent,
        ref InventoryRelayedEvent<RefreshEquipmentHudEvent<WWDPNightVisionComponent>> args)
    {
        if (ent.Comp.IsEquipment)
            base.OnRefreshEquipmentHud(ent, ref args);
    }

    private void OnToggle(Entity<WWDPNightVisionComponent> ent, ref SwitchableOverlayToggledEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<WWDPNightVisionComponent> args)
    {
        base.UpdateInternal(args);

        var active = false;
        WWDPNightVisionComponent? nvComp = null;
        foreach (var comp in args.Components)
        {
            if (comp.IsActive || comp.PulseTime > 0f && comp.PulseAccumulator < comp.PulseTime)
                active = true;
            else
                continue;

            if (comp.DrawOverlay)
            {
                if (nvComp == null)
                    nvComp = comp;
                else if (nvComp.PulseTime > 0f && comp.PulseTime <= 0f)
                    nvComp = comp;
            }

            if (active && nvComp is { PulseTime: <= 0 })
                break;
        }

        UpdateNightVision(active);
        UpdateOverlay(nvComp);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        UpdateNightVision(false);
        UpdateOverlay(null);
    }

    private void UpdateNightVision(bool active)
    {
        Log.Info($"WWDPNightVisionSystem: Setting DrawLighting to {!active}");

        _lightManager.DrawLighting = !active;
    }

    private void UpdateOverlay(WWDPNightVisionComponent? nvComp)
    {
        _overlay.Comp = nvComp;

        switch (nvComp)
        {
            case not null when !_overlayMan.HasOverlay<BaseSwitchableOverlay<WWDPNightVisionComponent>>():
                _overlayMan.AddOverlay(_overlay);
                break;
            case null:
                _overlayMan.RemoveOverlay(_overlay);
                break;
        }

        if (_overlayMan.TryGetOverlay<BaseSwitchableOverlay<ThermalVisionComponent>>(out var overlay))
            overlay.IsActive = nvComp == null;
    }
}
