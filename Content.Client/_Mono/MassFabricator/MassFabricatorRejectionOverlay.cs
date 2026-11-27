using System.Linq;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Mono.MassFabricator;

[RegisterComponent]
public sealed partial class MassFabricatorRejectionComponent : Component
{
    public TimeSpan Started;
    public ShaderInstance Shader = default!;
}

public sealed partial class MassFabricatorRejectionSystem : EntitySystem
{
    private const double Lifetime = 0.95;
    private const int DrawDepth = 110;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MassFabricatorRejectionComponent, ComponentShutdown>(OnShutdown);
    }

    public void Add(EntityCoordinates coordinates, Angle rotation)
    {
        if (_placement is not PlacementManager { CurrentPlacementOverlayEntity: { } preview } ||
            !TryComp<SpriteComponent>(preview, out var original))
            return;

        var uid = SpawnAttachedTo(null, coordinates, rotation: rotation);
        var sprite = EnsureComp<SpriteComponent>(uid);
        _sprites.CopySprite((preview, original), (uid, sprite));
        _sprites.SetColor((uid, sprite), Color.White);
        _sprites.SetDrawDepth((uid, sprite), DrawDepth);

        var shader = _prototypes.Index<ShaderPrototype>("Hologram").InstanceUnique();
        shader.SetParameter("color1", new Vector3(1f, 0.12f, 0.12f));
        shader.SetParameter("color2", new Vector3(1f, 0.22f, 0.22f));
        shader.SetParameter("intensity", 1f);
        shader.SetParameter("alpha", 0.95f);
        shader.SetParameter("texHeight", (float) sprite.AllLayers.Max(layer => layer.PixelSize.Y));
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
            sprite.LayerSetShader(i, "unshaded");
        sprite.PostShader = shader;

        var rejection = AddComp<MassFabricatorRejectionComponent>(uid);
        rejection.Started = _timing.CurTime;
        rejection.Shader = shader;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var query = EntityQueryEnumerator<MassFabricatorRejectionComponent>();
        while (query.MoveNext(out var uid, out var rejection))
        {
            var progress = (float) ((_timing.CurTime - rejection.Started).TotalSeconds / Lifetime);
            if (progress >= 1f)
            {
                QueueDel(uid);
                continue;
            }

            rejection.Shader.SetParameter("alpha", 0.95f * (1f - progress * progress));
            rejection.Shader.SetParameter("t", (float) _timing.CurTime.TotalSeconds * 0.125f);
        }
    }

    public void Clear()
    {
        var query = EntityQueryEnumerator<MassFabricatorRejectionComponent>();
        while (query.MoveNext(out var uid, out _))
            QueueDel(uid);
    }

    private void OnShutdown(Entity<MassFabricatorRejectionComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Shader.Dispose();
    }
}
