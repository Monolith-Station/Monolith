using Content.Shared._Scp.ScpCCVars; // Mono
using Robust.Client.Graphics; // Mono
using Robust.Shared.Configuration; // Mono
using Robust.Shared.Player;

namespace Content.Client._Scp.Grain;

// TODO: Коммон оверлей систем
public sealed class GrainOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Mono

    private GrainOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, ScpCCVars.GrainToggleOverlay, OnGrainCvarChanged); // Mono
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    #region Pulic API

    public void ToggleOverlay()
    {
        if (_overlayManager.HasOverlay<GrainOverlay>())
            _overlayManager.RemoveOverlay(_overlay);
        else if (_cfg.GetCVar(ScpCCVars.GrainToggleOverlay)) // Mono
            _overlayManager.AddOverlay(_overlay);
    }

    public void AddOverlay()
    {
        if (!_overlayManager.HasOverlay<GrainOverlay>() && _cfg.GetCVar(ScpCCVars.GrainToggleOverlay)) // Mono
            _overlayManager.AddOverlay(_overlay);
    }

    public void RemoveOverlay()
    {
        if (_overlayManager.HasOverlay<GrainOverlay>())
            _overlayManager.RemoveOverlay(_overlay); // Mono
    }
    // Mono start
    private void OnGrainCvarChanged(bool enabled)
    {
        if (enabled)
        {
            _overlayManager.AddOverlay(_overlay);
        }
        else
        {
            _overlayManager.RemoveOverlay(_overlay);
        }
        // Mono end
    }

    #endregion
}
