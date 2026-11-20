using System.Linq;
using System.Numerics;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Content.Client.IconSmoothing;
using Content.Client.Clickable;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorHologramSystem : EntitySystem
{
    private const float IdleOpacity = 0.72f;

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SpriteSystem _sprites = default!;
    private MassFabricatorClientSystem _fabricator = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sprites = EntityManager.System<SpriteSystem>();
        _fabricator = EntityManager.System<MassFabricatorClientSystem>();
        SubscribeLocalEvent<MassFabricatorHologramComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<MassFabricatorHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    public override void FrameUpdate(float frameTime)
    {
        var plans = EntityQueryEnumerator<MassFabricatorHologramComponent, SpriteComponent>();
        while (plans.MoveNext(out var uid, out var plan, out var sprite))
        {
            if (plan.Kind == MassFabricatorBlueprintKind.Entity)
                UpdateEntityFlashDepth(uid, plan, sprite);

            if (plan.BuildGrid is not { } frame || !TryComp<MapGridComponent>(frame, out var grid))
                continue;

            var visible = plan.PrintState == MassFabricatorPrintState.Fading ||
                !_fabricator.OverlapsForeignGrid((frame, grid), plan.Cell);
            if (visible != sprite.Visible)
                _sprites.SetVisible((uid, sprite), visible);
        }
    }

    private void UpdateEntityFlashDepth(EntityUid uid, MassFabricatorHologramComponent plan, SpriteComponent sprite)
    {
        if (plan.PrintState == MassFabricatorPrintState.Fading)
        {
            if (plan.PreFlashDrawDepth != null)
                return;

            plan.PreFlashDrawDepth = sprite.DrawDepth;
            _sprites.SetDrawDepth((uid, sprite),
                Math.Max((int) Content.Shared.DrawDepth.DrawDepth.Effects, sprite.DrawDepth + 1));
        }
        else if (plan.PreFlashDrawDepth is { } originalDepth)
        {
            _sprites.SetDrawDepth((uid, sprite), originalDepth);
            plan.PreFlashDrawDepth = null;
        }
    }

    private void OnState(Entity<MassFabricatorHologramComponent> ent, ref AfterAutoHandleStateEvent args) =>
        ShowPlan(ent.Owner);

    public void ShowPlan(EntityUid uid)
    {
        if (HasComp<SpriteComponent>(uid) || !TryComp<MassFabricatorHologramComponent>(uid, out var plan) ||
            plan.Target.Length == 0)
            return;

        var target = plan.Target;
        if (plan.Kind == MassFabricatorBlueprintKind.Entity &&
            _prototypes.TryIndex<EntityPrototype>(target, out var prototype))
        {
            SpriteComponent sprite;
            prototype.TryGetComponent<SpriteComponent>(out var template, Factory);
            if (prototype.TryGetComponent<IconSmoothComponent>(out _, Factory))
            {
                sprite = AddComp<SpriteComponent>(uid);
                if (template != null)
                    _sprites.SetDrawDepth((uid, sprite), template.DrawDepth);
                _sprites.AddTextureLayer((uid, sprite), _sprites.GetPrototypeIcon(prototype).Default);
                Comp<ClickableComponent>(uid).Bounds = new ClickableComponent.DirBoundData
                {
                    All = sprite.Bounds,
                };
            }
            else if (template != null)
            {
                sprite = _serialization.CreateCopy(template, notNullableOverride: true);
                AddComp(uid, sprite);
                var originalOwner = template.Owner;
                template.Owner = uid;
                _sprites.CopySprite((uid, template), (uid, sprite));
                template.Owner = originalOwner;
            }
            else
                sprite = AddComp<SpriteComponent>(uid);

            if (!sprite.AllLayers.Any(layer => layer.Visible && (layer.Texture != null || layer.RsiState.IsValid)))
                _sprites.AddTextureLayer((uid, sprite), _sprites.GetPrototypeIcon(prototype).Default);

            ApplyShader(uid, sprite);
        }
        else if (plan.Kind == MassFabricatorBlueprintKind.Tile &&
                 _prototypes.TryIndex<ContentTileDefinition>(target, out var tile) && tile.Sprite is { } path)
        {
            var sprite = AddComp<SpriteComponent>(uid);
            _sprites.SetDrawDepth((uid, sprite), (int) Content.Shared.DrawDepth.DrawDepth.FloorTiles);
            var texture = _resources.GetResource<TextureResource>(path).Texture;
            _sprites.AddTextureLayer((uid, sprite), texture);
            ApplyShader(uid, sprite);
        }
    }

    private void ApplyShader(EntityUid uid, SpriteComponent sprite)
    {
        var shader = _prototypes.Index<ShaderPrototype>("Hologram").InstanceUnique();
        shader.SetParameter("color1", new Vector3(0.12f, 0.38f, 0.85f));
        shader.SetParameter("color2", new Vector3(0.15f, 0.43f, 0.9f));
        shader.SetParameter("alpha", IdleOpacity);
        shader.SetParameter("intensity", 1f);
        shader.SetParameter("texHeight", (float) sprite.AllLayers.Max(layer => layer.PixelSize.Y));
        shader.SetParameter("t", (float) _timing.CurTime.TotalSeconds * 0.125f +
            (float) (GetNetEntity(uid).Id * 0.61803398875 % 1.0));
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
            sprite.LayerSetShader(i, "unshaded");
        sprite.PostShader = shader;
        sprite.RaiseShaderEvent = true;
        _sprites.SetVisible((uid, sprite), true);
    }

    private void OnShaderRender(Entity<MassFabricatorHologramComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        var erase = EntityManager.System<MassFabricatorEraseSystem>().IsHighlighted(ent.Owner);
        var blue = new Vector3(0.12f, 0.38f, 0.85f);
        var brightBlue = new Vector3(0.15f, 0.43f, 0.9f);
        var first = erase ? new Vector3(1f, 0.08f, 0.08f) : blue;
        var second = erase ? new Vector3(1f, 0.16f, 0.16f) : brightBlue;
        var removed = TryComp<MassFabricatorComponent>(ent.Comp.Fabricator, out var fabricator) &&
            !fabricator.HasBlueprint(ent.Comp.Blueprint);
        if (!erase && (removed || ent.Comp.Unsupported || ent.Comp.NoWaveStart))
        {
            first = new Vector3(1f, 0.32f, 0.04f);
            second = new Vector3(1f, 0.65f, 0.15f);
        }
        else if (!erase && _fabricator.NeedsWave(ent.Comp))
        {
            first = new Vector3(1f, 0.82f, 0.04f);
            second = new Vector3(1f, 1f, 0.38f);
        }
        var alpha = IdleOpacity;
        var intensity = 1f;
        var elapsed = (float) (_timing.CurTime - ent.Comp.PrintStarted).TotalSeconds;
        if (ent.Comp.PrintState == MassFabricatorPrintState.Printing)
        {
            var progress = Math.Clamp(elapsed / Math.Max(ent.Comp.PrintDuration, float.Epsilon), 0f, 1f);
            if (!removed)
            {
                first = blue;
                second = brightBlue;
            }
            alpha = IdleOpacity + (1f - IdleOpacity) * progress;
        }
        else if (ent.Comp.PrintState == MassFabricatorPrintState.Fading)
        {
            var fade = Math.Clamp(elapsed / MassFabricatorHologramComponent.FadeSeconds, 0f, 1f);
            first = second = Vector3.One;
            alpha = 1f - fade;
            intensity += 3f * (1f - fade);
        }

        args.Sprite.PostShader?.SetParameter("color1", first);
        args.Sprite.PostShader?.SetParameter("color2", second);
        args.Sprite.PostShader?.SetParameter("alpha", alpha);
        args.Sprite.PostShader?.SetParameter("intensity", intensity);
        var phase = (float) (GetNetEntity(ent.Owner).Id * 0.61803398875 % 1.0);
        args.Sprite.PostShader?.SetParameter("t", (float) _timing.CurTime.TotalSeconds * 0.125f + phase);
    }
}
