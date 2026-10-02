using System.Collections.Generic;
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
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Client._Mono.MassFabricator;

public sealed partial class MassFabricatorHologramSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SpriteSystem _sprites = default!;
    private readonly Dictionary<EntityUid, TimeSpan> _printVisualStarts = new();

    public override void Initialize()
    {
        base.Initialize();
        _sprites = EntityManager.System<SpriteSystem>();
        SubscribeLocalEvent<MassFabricatorHologramComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<MassFabricatorHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    private void OnState(Entity<MassFabricatorHologramComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ShowPlan(ent.Owner);
        if (ent.Comp.PrintState == MassFabricatorPrintState.Printing)
        {
            _printVisualStarts.TryAdd(ent.Owner, _timing.CurTime);
            ConfirmPrint(ent);
        }
        else if (ent.Comp.PrintState == MassFabricatorPrintState.Idle &&
                 _printWaves.TryGetValue(ent.Owner, out var cancelled) && cancelled.Confirmed)
        {
            RollbackPrediction(cancelled, false);
            _printWaves.Remove(ent.Owner);
            _printVisualStarts.Remove(ent.Owner);
        }

        if (TryComp<SpriteComponent>(ent.Owner, out var sprite))
            _sprites.SetDrawDepth((ent.Owner, sprite), PlanDrawDepth(ent.Comp));
    }

    public void OnPlanShutdown(Entity<MassFabricatorHologramComponent> ent)
    {
        _printVisualStarts.Remove(ent.Owner);
        if (_printWaves.Remove(ent.Owner, out var node))
            RollbackPrediction(node, ent.Comp.PrintState == MassFabricatorPrintState.Fading);
    }

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
            if (prototype.TryGetComponent<IconSmoothComponent>(out _, Factory))
            {
                sprite = AddComp<SpriteComponent>(uid);
                _sprites.AddTextureLayer((uid, sprite), _sprites.GetPrototypeIcon(prototype).Default);
                Comp<ClickableComponent>(uid).Bounds = new ClickableComponent.DirBoundData
                {
                    All = sprite.Bounds,
                };
            }
            else if (prototype.TryGetComponent<SpriteComponent>(out var template, Factory))
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
            var texture = _resources.GetResource<TextureResource>(path).Texture;
            _sprites.AddTextureLayer((uid, sprite), texture);
            ApplyShader(uid, sprite);
        }
    }

    private static int PlanDrawDepth(MassFabricatorHologramComponent plan) =>
        plan.Kind == MassFabricatorBlueprintKind.Tile
            ? (int) Content.Shared.DrawDepth.DrawDepth.HighFloorObjects
            : (int) Content.Shared.DrawDepth.DrawDepth.Effects;

    private void ApplyShader(EntityUid uid, SpriteComponent sprite)
    {
        sprite.NoRotation = false;
        _sprites.SetDrawDepth((uid, sprite), PlanDrawDepth(Comp<MassFabricatorHologramComponent>(uid)));
        var shader = _prototypes.Index<ShaderPrototype>("Hologram").InstanceUnique();
        shader.SetParameter("color1", new Vector3(0.12f, 0.38f, 0.85f));
        shader.SetParameter("color2", new Vector3(0.15f, 0.43f, 0.9f));
        shader.SetParameter("alpha", 0.5f);
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
        var alpha = 0.5f;
        var intensity = 1f;
        var hasVisualStart = _printVisualStarts.TryGetValue(ent.Owner, out var visualStart);
        var duration = VisualPrintDuration(ent.Comp);
        var visualEnd = visualStart + TimeSpan.FromSeconds(duration);
        _printWaves.TryGetValue(ent.Owner, out var node);
        var predictedPrint = hasVisualStart && node is { Confirmed: true } or { Ready: true };
        var predictedComplete = predictedPrint && node!.Expanded && _timing.CurTime >= visualEnd;
        if (!predictedComplete && (predictedPrint && _timing.CurTime < visualEnd ||
            ent.Comp.PrintState == MassFabricatorPrintState.Printing ||
            ent.Comp.PrintState == MassFabricatorPrintState.Idle && _printWaves.ContainsKey(ent.Owner)))
        {
            var start = hasVisualStart ? visualStart : ent.Comp.PrintStarted;
            var progress = Math.Clamp((float) (_timing.CurTime - start).TotalSeconds / duration, 0f, 1f);
            first = blue;
            second = brightBlue;
            alpha = 0.5f + 0.5f * progress;
        }
        else if (ent.Comp.PrintState == MassFabricatorPrintState.Fading || predictedComplete)
        {
            var fadeStart = predictedPrint ? visualEnd : ent.Comp.PrintStarted;
            var fade = Math.Clamp((float) (_timing.CurTime - fadeStart).TotalSeconds /
                MassFabricatorHologramComponent.FadeSeconds, 0f, 1f);
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
