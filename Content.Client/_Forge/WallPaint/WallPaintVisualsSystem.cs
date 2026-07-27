using System.Linq;
using Content.Shared._Forge.WallPaint;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Forge.WallPaint;

public sealed partial class WallPaintVisualsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private const string ShaderPrototypeId = "WallPaintDarken";
    private const float OpaqueAlphaThreshold = 0.95f;

    private readonly Dictionary<EntityUid, PaintShaderState> _shaderStates = new();
    private readonly Dictionary<PaintShaderKey, CachedPaintShader> _shaderCache = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WallPaintComponent, ComponentStartup>(OnPaintStartup);
        SubscribeLocalEvent<WallPaintComponent, AfterAutoHandleStateEvent>(OnPaintHandleState);
        SubscribeLocalEvent<WallPaintComponent, ComponentShutdown>(OnPaintShutdown);
        SubscribeLocalEvent<PaintableWallComponent, ComponentStartup>(OnPaintableStartup);
    }

    public override void Shutdown()
    {
        foreach (var uid in _shaderStates.Keys.ToArray())
        {
            ClearShader(uid);
        }

        foreach (var cached in _shaderCache.Values)
        {
            cached.Shader.Dispose();
        }

        _shaderCache.Clear();
        base.Shutdown();
    }

    private void OnPaintStartup(EntityUid uid, WallPaintComponent component, ComponentStartup args)
    {
        UpdateShader(uid, component);
    }

    private void OnPaintHandleState(EntityUid uid, WallPaintComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateShader(uid, component);
    }

    private void OnPaintShutdown(EntityUid uid, WallPaintComponent component, ComponentShutdown args)
    {
        ClearShader(uid);
    }

    private void OnPaintableStartup(EntityUid uid, PaintableWallComponent component, ComponentStartup args)
    {
        if (TryComp(uid, out WallPaintComponent? paint))
            UpdateShader(uid, paint);
    }

    private void UpdateShader(EntityUid uid, WallPaintComponent paint, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref sprite, false))
            return;

        var key = new PaintShaderKey(paint.Color, paint.ProtectTransparent);
        PaintShaderKey? previousKey = null;

        if (!_shaderStates.TryGetValue(uid, out var state))
        {
            state = new PaintShaderState(key, AcquireShader(key));
            _shaderStates.Add(uid, state);
        }
        else if (state.Key != key)
        {
            previousKey = state.Key;
            state.Key = key;
            state.Shader = AcquireShader(key);
        }

        var layerIndex = 0;
        foreach (var layer in sprite.AllLayers.Cast<SpriteComponent.Layer>())
        {
            if (layerIndex >= state.PreviousLayers.Count)
                state.PreviousLayers.Add(new LayerShaderState(layer.Shader, layer.ShaderPrototype));

            sprite.LayerSetShader(layerIndex, state.Shader, ShaderPrototypeId);
            layerIndex++;
        }

        if (previousKey is { } oldKey)
            ReleaseShader(oldKey);
    }

    private ShaderInstance AcquireShader(PaintShaderKey key)
    {
        if (!_shaderCache.TryGetValue(key, out var cached))
        {
            var shader = _prototype.Index<ShaderPrototype>(ShaderPrototypeId).InstanceUnique();
            shader.SetParameter("paintColor", key.Color);
            shader.SetParameter("protectTransparency", key.ProtectTransparent);
            shader.SetParameter("opaqueAlphaThreshold", OpaqueAlphaThreshold);
            shader.MakeImmutable();

            cached = new CachedPaintShader(shader);
            _shaderCache.Add(key, cached);
        }

        cached.ReferenceCount++;
        return cached.Shader;
    }

    private void ReleaseShader(PaintShaderKey key)
    {
        if (!_shaderCache.TryGetValue(key, out var cached))
            return;

        cached.ReferenceCount--;
        if (cached.ReferenceCount > 0)
            return;

        _shaderCache.Remove(key);
        cached.Shader.Dispose();
    }

    private void ClearShader(EntityUid uid)
    {
        if (!_shaderStates.Remove(uid, out var state))
            return;

        if (!TerminatingOrDeleted(uid) && TryComp(uid, out SpriteComponent? sprite))
        {
            var count = Math.Min(state.PreviousLayers.Count, sprite.AllLayers.Count());
            for (var i = 0; i < count; i++)
            {
                var previous = state.PreviousLayers[i];
                if (previous.Shader != null)
                    sprite.LayerSetShader(i, previous.Shader, previous.Prototype?.Id);
                else if (previous.Prototype is { } prototype)
                    sprite.LayerSetShader(i, prototype.Id);
                else
                    sprite.LayerSetShader(i, null, null);
            }
        }

        ReleaseShader(state.Key);
    }

    private readonly record struct PaintShaderKey(Color Color, bool ProtectTransparent);

    private readonly record struct LayerShaderState(
        ShaderInstance? Shader,
        ProtoId<ShaderPrototype>? Prototype);

    private sealed class PaintShaderState
    {
        public PaintShaderKey Key;
        public ShaderInstance Shader;
        public readonly List<LayerShaderState> PreviousLayers = new();

        public PaintShaderState(PaintShaderKey key, ShaderInstance shader)
        {
            Key = key;
            Shader = shader;
        }
    }

    private sealed class CachedPaintShader
    {
        public readonly ShaderInstance Shader;
        public int ReferenceCount;

        public CachedPaintShader(ShaderInstance shader)
        {
            Shader = shader;
        }
    }
}
