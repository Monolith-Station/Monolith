using Content.Shared._Forge.Silicons.StationAi;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Forge.Silicons.StationAi;

public sealed class StationAiScreenSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAiScreenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StationAiScreenComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<StationAiScreenComponent> ent, ref ComponentStartup args)
    {
        UpdateScreen(ent);
    }

    private void OnState(Entity<StationAiScreenComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateScreen(ent);
    }

    private void UpdateScreen(Entity<StationAiScreenComponent> ent)
    {
        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        if (!sprite.LayerMapTryGet(ent.Comp.ScreenLayer, out var layer))
            return;

        var path = ent.Comp.EmptySprite;
        var state = ent.Comp.EmptyState;
        if (ent.Comp.Occupied &&
            (_prototypes.TryIndex(ent.Comp.Screen, out var prototype) ||
             _prototypes.TryIndex(ent.Comp.DefaultScreen, out prototype)))
        {
            path = prototype.Sprite;
            state = prototype.State;
        }

        if (!_resources.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / path, out var resource))
            return;

        _sprites.LayerSetRsi((ent.Owner, sprite), layer, resource.RSI, state);
        _sprites.LayerSetColor((ent.Owner, sprite), layer, ent.Comp.Occupied ? ent.Comp.Color : Color.White);
    }
}
