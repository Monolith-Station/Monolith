/// Forge-Change-Start
using Content.Shared._NF.Storage;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._NF.Storage;

/// <summary>
/// Applies anchorable storage world-sprite visibility and draw depth from appearance state.
/// </summary>
public sealed class AnchorableStorageVisualSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AppearanceComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<AppearanceComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateVisuals(ent, args.Sprite);
    }

    private void UpdateVisuals(Entity<AppearanceComponent> ent, SpriteComponent sprite)
    {
        if (!_appearance.TryGetData<bool>(ent.Owner, AnchorableStorageVisuals.Anchored, out var anchored, ent.Comp))
            return;

        _sprite.SetDrawDepth((ent.Owner, sprite), anchored ? (int) DrawDepth.ThinWire : (int) DrawDepth.Items);
        _sprite.SetVisible((ent.Owner, sprite), !anchored);
    }
}
/// Forge-Change-End
