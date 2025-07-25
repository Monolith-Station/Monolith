/// Reserve - File heavily edited by PR: Mapping editor.
/// See https://github.com/space-wizards/space-station-14/pull/34302
/// and https://github.com/Reserve-Station/Reserve-Station/pull/82 for more details.

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using static Content.Client.Mapping.MappingState;

namespace Content.Client.Mapping;

public sealed class MappingOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    // 1 off in case something else uses these colors since we use them to compare
    private static readonly Color PickColor = new(1, 255, 0);
    private static readonly Color DeleteColor = new(255, 1, 0);
    private readonly SpriteSystem _sprite;

    private readonly Dictionary<EntityUid, Color> _oldColors = new();

    private readonly MappingState _state;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MappingOverlay(MappingState state)
    {
        IoCManager.InjectDependencies(this);

        _state = state;
        _shader = _prototypes.Index<ShaderPrototype>("unshaded").Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (id, color) in _oldColors)
        {
            if (_entities.TryGetComponent(id, out SpriteComponent? sprite))
                _sprite.SetColor((id, sprite), color);
        }

        _oldColors.Clear();

        var handle = args.WorldHandle;
        handle.UseShader(_shader);

        switch (_state.Meta.State)
        {
            case CursorState.Tile:
                {
                    if (_state.GetHoveredTileBox2() is { } box)
                        args.WorldHandle.DrawRect(box, _state.Meta.Color);

                    break;
                }
            case CursorState.Entity:
                {
                    if (_state.GetHoveredEntity() is { } entity &&
                        _entities.TryGetComponent(entity, out SpriteComponent? sprite))
                    {
                        _oldColors[entity] = sprite.Color;
                        _sprite.SetColor((entity, sprite), _state.Meta.Color);
                    }

                    break;
                }
            case CursorState.EntityOrTile:
                {
                    if (_state.GetHoveredEntity() is { } entity &&
                        _entities.TryGetComponent(entity, out SpriteComponent? sprite))
                    {
                        _oldColors[entity] = sprite.Color;
                        _sprite.SetColor((entity, sprite), _state.Meta.Color);
                    }
                    else if (_state.GetHoveredTileBox2() is { } box)
                    {
                        args.WorldHandle.DrawRect(box, _state.Meta.SecondColor ?? _state.Meta.Color);
                    }

                    break;
                }
        }

        handle.UseShader(null);
    }
}
