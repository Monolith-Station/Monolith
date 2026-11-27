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

            DrawTether(args, _shader, center, body, visual.Color);
        }

        handle.UseShader(null);
    }

    private static void DrawTether(in OverlayDrawArgs args, ShaderInstance shader, Vector2 start, Vector2 end, Color color)
    {
        var viewport = args.Viewport;
        var startPixel = viewport.WorldToLocal(start);
        startPixel.Y = viewport.Size.Y - startPixel.Y;
        var endPixel = viewport.WorldToLocal(end);
        endPixel.Y = viewport.Size.Y - endPixel.Y;

        shader.SetParameter("EYE_POSITION", startPixel);
        shader.SetParameter("BODY_POSITION", endPixel);
        shader.SetParameter("PIXELS_PER_TILE", viewport.RenderScale * EyeManager.PixelsPerMeter);
        shader.SetParameter("HOLOGRAM_COLOR", color);
        var padding = new Vector2(1.1f);
        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(new Box2(Vector2.Min(start, end) - padding, Vector2.Max(start, end) + padding), Color.White);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }
}
