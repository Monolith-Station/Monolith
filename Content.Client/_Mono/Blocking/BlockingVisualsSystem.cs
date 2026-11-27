using Content.Client._Mono.Blocking.Components;
using Content.Client.Graphics;
using Content.Shared._Mono.Blocking;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Mono.Blocking;

public sealed partial class BlockingVisualsSystem : SharedBlockingSystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _protoMan.Index<ShaderPrototype>("ShieldingOutline").InstanceUnique();

        SubscribeLocalEvent<BlockingVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlockingVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    public override void SetEnabled(EntityUid uid, bool value, BlockingVisualsComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Enabled == value)
            return;

        base.SetEnabled(uid, value, component);
        SetShader(uid, value, component);
    }

    private void SetShader(EntityUid uid, bool enabled, BlockingVisualsComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false))
            return;

        if (enabled)
        {
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.Shielding, _shader)
            {
                After = ContentPostShaderIds.AfterBaseEffects,
            });
        }
        else
        {
            _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.Shielding);
        }
    }
    private void OnStartup(EntityUid uid, BlockingVisualsComponent component, ComponentStartup args)
    {
        SetShader(uid, component.Enabled, component);
    }

    private void OnShutdown(EntityUid uid, BlockingVisualsComponent component, ComponentShutdown args)
    {
        if (!Terminating(uid))
            SetShader(uid, false, component);
    }
}
