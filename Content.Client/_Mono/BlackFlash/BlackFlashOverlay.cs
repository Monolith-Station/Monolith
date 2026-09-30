using System.Linq;
using System.Numerics;
using Content.Shared._Mono.BlackFlash;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Mono.BlackFlash;

public sealed partial class BlackFlashOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SharedTransformSystem? _transform;
    private readonly ShaderInstance _baseShader;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BlackFlashOverlay()
    {
        IoCManager.InjectDependencies(this);
        _baseShader = _proto.Index<ShaderPrototype>("BlackFlash").Instance().Duplicate();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _transform ??= _entity.System<SharedTransformSystem>();

        var handle = args.WorldHandle;
        var viewport = args.Viewport;
        var seen = new ValueList<EntityUid>();

        var query = _entity.EntityQueryEnumerator<BlackFlashEffectComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != args.MapId || comp.Duration <= 0f)
                continue;

            var life = (float)((_timing.CurTime - comp.Start).TotalSeconds / comp.Duration);
            if (life is < 0f or > 1f)
                continue;

            seen.Add(uid);

            if (!_shaders.TryGetValue(uid, out var shader))
            {
                shader = _baseShader.Duplicate();
                _shaders[uid] = shader;
            }

            var world = _transform.GetWorldPosition(uid);
            var local = viewport.WorldToLocal(world);
            local.Y = viewport.Size.Y - local.Y;

            shader.SetParameter("renderScale", viewport.RenderScale);
            shader.SetParameter("positionInput", local);
            var facing = _transform.GetWorldRotation(uid) + (viewport.Eye?.Rotation ?? Angle.Zero);
            shader.SetParameter("angle", (float)facing.Theta);
            shader.SetParameter("life", life);
            shader.SetParameter("scale", comp.Scale);
            shader.SetParameter("intensity", comp.Intensity);
            shader.SetParameter("seed", comp.Seed);

            handle.UseShader(shader);
            handle.DrawRect(Box2.CenteredAround(world, new Vector2(comp.Scale * 8f)), Color.White);
        }

        handle.UseShader(null);

        if (_shaders.Count == seen.Count)
            return;

        foreach (var uid in _shaders.Keys.ToArray())
        {
            if (seen.Contains(uid))
                continue;

            _shaders[uid].Dispose();
            _shaders.Remove(uid);
        }
    }
}
