using System.Numerics;
using Content.Shared._Mono.MassFabricator;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.MassFabricator;

public sealed class MassFabricatorEyeOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MassFabricatorEyeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypes.Index<ShaderPrototype>("MonoMassFabricatorHologram").InstanceUnique();
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var transform = _entities.System<SharedTransformSystem>();
        var handle = args.WorldHandle;
        var client = _entities.System<MassFabricatorClientSystem>();
        var viewport = args.Viewport;
        var pixelsPerTile = viewport.RenderScale * 32f;
        var query = _entities.EntityQueryEnumerator<MassFabricatorEyeComponent, TransformComponent>();
        while (query.MoveNext(out var eye, out var visual, out var xform))
        {
            if (xform.MapID != args.MapId || visual.BodyMap != xform.MapUid ||
                !visual.User.IsValid() || client.IsEyeHidden(visual.User))
                continue;

            var center = transform.GetWorldPosition(eye);
            var body = visual.BodyPosition;
            if (_entities.TryGetComponent(visual.User, out TransformComponent? bodyXform) &&
                bodyXform.MapID == args.MapId)
                body = transform.GetWorldPosition(visual.User);

            var eyePixel = viewport.WorldToLocal(center);
            eyePixel.Y = viewport.Size.Y - eyePixel.Y;
            var bodyPixel = viewport.WorldToLocal(body);
            bodyPixel.Y = viewport.Size.Y - bodyPixel.Y;

            _shader.SetParameter("EYE_POSITION", eyePixel);
            _shader.SetParameter("BODY_POSITION", bodyPixel);
            _shader.SetParameter("PIXELS_PER_TILE", pixelsPerTile);
            _shader.SetParameter("HOLOGRAM_COLOR", visual.Color);
            handle.UseShader(_shader);
            handle.DrawRect(new Box2(Vector2.Min(center, body) - new Vector2(1.1f),
                Vector2.Max(center, body) + new Vector2(1.1f)), Color.White);
        }

        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
