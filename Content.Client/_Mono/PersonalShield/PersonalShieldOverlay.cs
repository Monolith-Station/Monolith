using System.Numerics;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.PersonalShield;

public sealed partial class PersonalShieldOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = null!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "PersonalShieldSkin";

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly InventorySystem _inventory;
    private readonly ShaderPrototype _shaderProto;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private readonly List<EntityUid> _stale = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public PersonalShieldOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _inventory = _entManager.System<InventorySystem>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        _shaderProto = protoMan.Index(ShaderId);
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _shaders.Clear();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;

        // Cancel the eye rotation so the shield is always "upright".
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var counterRot = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entManager.EntityQueryEnumerator<PersonalShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (shield.Runtime.Form <= 0f && shield.Runtime.Shatter <= 0f)
                continue;

            if (!_inventory.TryGetContainingEntity(uid, out var wearer))
                continue;

            if (!_entManager.TryGetComponent(wearer, out SpriteComponent? sprite) || !sprite.Visible)
                continue;

            if (!_entManager.TryGetComponent(wearer, out TransformComponent? xform) || xform.MapID != args.MapId)
                continue;

            if (!TryGetHitboxSize(wearer.Value, sprite, out var extents))
                continue;

            var size = extents * shield.Scale;

            var shader = GetShader(uid);

            shader.SetParameter("progress", GetProgress(shield));
            shader.SetParameter("skin_color", shield.Color);
            shader.SetParameter("brightness", shield.Brightness);
            shader.SetParameter("pixel_grid", shield.PixelGrid);
            shader.SetParameter("hex_density", shield.HexDensity);
            shader.SetParameter("form_origin", shield.FormOrigin);
            shader.SetParameter("fill_level", shield.FillLevel);
            shader.SetParameter("line_level", shield.LineLevel);
            shader.SetParameter("rim_level", shield.RimLevel);
            shader.SetParameter("core_fade", shield.CoreFade);
            shader.SetParameter("shard_scale", shield.ShardScale);
            shader.SetParameter("alpha_bands", shield.AlphaBands);
            shader.SetParameter("breath_depth", shield.BreathDepth);

            handle.UseShader(shader);

            var worldPos = _transform.GetWorldPosition(xform);
            handle.SetTransform(Matrix3x2.Multiply(counterRot, Matrix3Helpers.CreateTranslation(worldPos)));
            handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, size));
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);

        PruneShaders();
    }

    private void PruneShaders()
    {
        foreach (var (uid, shader) in _shaders)
        {
            if (_entManager.HasComponent<PersonalShieldComponent>(uid))
                continue;

            shader.Dispose();
            _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _shaders.Remove(uid);
        }

        _stale.Clear();
    }

    private ShaderInstance GetShader(EntityUid uid)
    {
        if (!_shaders.TryGetValue(uid, out var shader))
        {
            shader = _shaderProto.InstanceUnique();
            _shaders[uid] = shader;
        }

        return shader;
    }

    private bool TryGetHitboxSize(EntityUid uid, SpriteComponent sprite, out Vector2 extents)
    {
        extents = Vector2.Zero;

        if (_entManager.TryGetComponent(uid, out FixturesComponent? fixtures) && fixtures.FixtureCount > 0)
        {
            var identity = new Transform(Vector2.Zero, 0f);
            Box2? union = null;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                var aabb = fixture.Shape.ComputeAABB(identity, 0);
                union = union?.Union(aabb) ?? aabb;
            }

            if (union is { } box && box.Width > 0f && box.Height > 0f)
            {
                extents = box.Size;
                return true;
            }
        }

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        extents = bounds.Size;
        return extents is { X: > 0f, Y: > 0f };
    }

    private static float GetProgress(PersonalShieldComponent shield)
    {
        return shield.Runtime.Shatter > 0f
            ? 1f + MathF.Min(shield.Runtime.Shatter, 1f)
            : shield.Runtime.Form;
    }
}
