using Content.Shared._Mono.MAWC.Scrap;
using Robust.Client.GameObjects;

namespace Content.Client._Mono.MAWC.Scrap;

public sealed partial class HotScrapVisualsSystem : EntitySystem
{
    [Dependency] private HotScrapSystem _scrap = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HotScrapComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<HotScrapComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.LayerSetVisible((ent, sprite), HotScrapVisualLayers.Warm, false);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<HotScrapComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var scrap, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), HotScrapVisualLayers.Warm, out var layer, false))
                continue;

            var heat = _scrap.GetHeat((uid, scrap));
            _sprite.LayerSetVisible((uid, sprite), layer, heat > 0f);
            _sprite.LayerSetColor((uid, sprite), layer, Color.White.WithAlpha(heat));
        }
    }
}
