using Content.Client._Scp.Grain;
using Content.Client._Scp.Vignette;
using Content.Shared._DV.CCVars; // Mono
using Content.Shared._Scp.RetroMonitor;
using Robust.Client.Graphics;
using Robust.Shared.Configuration; // Mono
using Robust.Shared.Player;

namespace Content.Client._Scp.RetroMonitor;

public sealed class RetroMonitorOverlaySystem : EntitySystem
{
    [Dependency] private readonly GrainOverlaySystem _grain = default!;
    [Dependency] private readonly VignetteOverlaySystem _vignette = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Mono

    private readonly RetroMonitorOverlay _overlay = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RetroMonitorViewComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RetroMonitorViewComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, DCCVars.NoVisionFilters, OnNoVisionFiltersChanged); // Mono
    }

    private void OnPlayerAttached(Entity<RetroMonitorViewComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        // Mono - check for cvar
        if(!_cfg.GetCVar(DCCVars.NoVisionFilters)){
            _overlayManager.AddOverlay(_overlay);

            _grain.RemoveOverlay();
            _vignette.RemoveOverlay();}
    }

    private void OnPlayerDetached(Entity<RetroMonitorViewComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);

        _grain.AddOverlay();
        _vignette.AddOverlay();
    }
    // Mono start
    private void OnNoVisionFiltersChanged(bool enabled)
    {
        if (enabled)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _grain.AddOverlay();
            _vignette.AddOverlay();
        }
        else
        {
            _overlayManager.AddOverlay(_overlay);
            _grain.RemoveOverlay();
            _vignette.RemoveOverlay();
        }
        // Mono end
    }
}
