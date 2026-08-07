using Content.Client.Hands;
using Content.Shared._Mono.DualWield;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Mono.DualWield;

/// <summary>
/// Shows and hides the dual-wield cursor overlay for the local player.
/// </summary>
public sealed partial class DualWieldSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private ISharedPlayerManager _playerMan = default!;

    private DualWieldCursorOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DualWieldComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DualWieldComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DualWieldComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<DualWieldComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnInit(Entity<DualWieldComponent> ent, ref ComponentInit args)
    {
        if (ent.Owner == _playerMan.LocalEntity)
            EnableOverlay();
    }

    private void OnShutdown(Entity<DualWieldComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _playerMan.LocalEntity)
            DisableOverlay();
    }

    private void OnPlayerAttached(Entity<DualWieldComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        EnableOverlay();
    }

    private void OnPlayerDetached(Entity<DualWieldComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        DisableOverlay();
    }

    private void EnableOverlay()
    {
        if (!_overlayMan.HasOverlay<DualWieldCursorOverlay>())
            _overlayMan.AddOverlay(_overlay);

        // Otherwise the active hand's weapon would be drawn twice, once by each overlay.
        SetHandItemOverlayHidden(true);
    }

    private void DisableOverlay()
    {
        _overlayMan.RemoveOverlay(_overlay);
        SetHandItemOverlayHidden(false);
    }

    private void SetHandItemOverlayHidden(bool hidden)
    {
        if (_overlayMan.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
            handOverlay.Hidden = hidden;
    }
}
