using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Toggleable;

namespace Content.Shared._Goobstation.Toggle;

public sealed partial class ItemToggleColorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemToggleColorComponent, ItemToggledEvent>(OnLightToggled);
    }

    private void OnLightToggled(Entity<ItemToggleColorComponent> ent, ref ItemToggledEvent args)
    {
        _appearance.SetData(ent, ToggleableVisuals.Enabled, args.Activated);
    }
}
