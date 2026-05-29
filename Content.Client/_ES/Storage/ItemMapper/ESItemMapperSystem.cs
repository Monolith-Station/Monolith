using Content.Shared._ES.Storage.ItemMapper;
using Content.Shared._ES.Storage.ItemMapper.Components;
using Robust.Client.GameObjects;

namespace Content.Client._ES.Storage.ItemMapper;

public sealed partial class ESItemMapperSystem : ESSharedItemMapperSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ESItemMapperComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<ESItemMapperComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            throw new Exception($"{ToPrettyString(ent)} is missing {nameof(SpriteComponent)}!");

        if (!Appearance.TryGetData(ent, ESItemMapperVisuals.Layers, out Dictionary<string, string?> layers, args.Component))
        {
            //Stopgap, throw replaced with Log.Warning and return. Prevents crash in debug mode when looking at lathe menu.
            //Someone more competent can come up with a more comprehensive fix.
            Log.Warning($"Couldn't find the necessary {nameof(ESItemMapperVisuals.Layers)} layer on {ToPrettyString(ent)}.");
            return;
        }

        foreach (var (key, state) in layers)
        {
            _sprite.LayerSetVisible((ent, sprite), key, state != null);
            _sprite.LayerSetRsiState((ent, sprite), key, state);
        }
    }
}
