using Content.Client.ContextMenu.UI;
using Content.Client.Gameplay;
using Content.Client.Graphics;
using Content.Client.Interactable.Components;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Outline;

public sealed partial class InteractionOutlineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderInRange = "SelectionOutlineInrange";
    private static readonly ProtoId<ShaderPrototype> ShaderOutOfRange = "SelectionOutline";
    private readonly Dictionary<(bool InRange, int RenderScale), ShaderInstance> _shaderCache = new();

    /// <summary>
    ///     Whether to currently draw the outline. The outline may be temporarily disabled by other systems
    /// </summary>
    private bool _enabled = true;

    /// <summary>
    ///     Whether to draw the outline at all. Overrides <see cref="_enabled"/>.
    /// </summary>
    private bool _cvarEnabled = true;

    private EntityUid? _lastHoveredEntity;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configManager, CCVars.OutlineEnabled, SetCvarEnabled);
        SubscribeLocalEvent<InteractionOutlineComponent, ComponentShutdown>(OnOutlineShutdown);
        UpdatesAfter.Add(typeof(SharedEyeSystem));
    }

    public override void Shutdown()
    {
        ClearHover();
        foreach (var shader in _shaderCache.Values)
            shader.Dispose();

        _shaderCache.Clear();
        base.Shutdown();
    }

    private void OnOutlineShutdown(Entity<InteractionOutlineComponent> ent, ref ComponentShutdown args)
    {
        if (_lastHoveredEntity == ent.Owner)
            ClearHover();
    }

    private void ClearHover()
    {
        if (TryComp<SpriteComponent>(_lastHoveredEntity, out var sprite))
            _sprite.RemovePostShader(sprite, ContentPostShaderIds.InteractionOutline);

        _lastHoveredEntity = null;
    }

    public void SetCvarEnabled(bool cvarEnabled)
    {
        _cvarEnabled = cvarEnabled;

        if (!cvarEnabled)
            ClearHover();
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _enabled)
            return;

        _enabled = enabled;

        if (!enabled)
            ClearHover();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled || !_cvarEnabled)
            return;

        // If there is no local player, there is no session, and therefore nothing to do here.
        var localSession = _playerManager.LocalSession;
        if (localSession == null)
            return;

        // TODO InteractionOutlineComponent
        // BUG: The logic that gets the renderScale here assumes that the entity is only visible in a single
        // viewport. The entity will be highlighted in ALL viewport where it is visible, regardless of which
        // viewport is being used to hover over it. If these Viewports have very different render scales, this may
        // lead to extremely thick outlines in the other viewports. Fixing this probably requires changing how the
        // hover outline works, so that it only highlights the entity in a single viewport.

        // GameScreen is still in charge of what entities are visible under a specific cursor position.
        // Potentially change someday? who knows.
        var currentState = _stateManager.CurrentState;

        if (currentState is not GameplayStateBase screen) return;

        EntityUid? entityToClick = null;
        var renderScale = 1;
        if (_uiManager.CurrentlyHovered is IViewportControl vp
            && _inputManager.MouseScreenPosition.IsValid)
        {
            var mousePosWorld = vp.PixelToMap(_inputManager.MouseScreenPosition.Position);

            if (vp is ScalingViewport svp)
            {
                renderScale = svp.CurrentRenderScale;
                entityToClick = screen.GetClickedEntity(mousePosWorld, svp.Eye);
            }
            else
            {
                entityToClick = screen.GetClickedEntity(mousePosWorld);
            }
        }
        else if (_uiManager.CurrentlyHovered is EntityMenuElement element)
        {
            entityToClick = element.Entity;
            // TODO InteractionOutlineComponent
            // Currently we just take the renderscale from the main viewport. In the future, when the bug mentioned
            // above is fixed, the viewport should probably be the one that was clicked on to open the entity menu
            // in the first place.
            renderScale = _eyeManager.MainViewport.GetRenderScale();
        }

        if (entityToClick != _lastHoveredEntity || !HasComp<InteractionOutlineComponent>(entityToClick))
            ClearHover();

        if (entityToClick is not { } uid || !HasComp<InteractionOutlineComponent>(uid)
            || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var inRange = localSession.AttachedEntity is { } player && _interactionSystem.InRangeUnobstructed(player, uid);
        var key = (inRange, renderScale);
        if (!_shaderCache.TryGetValue(key, out var shader))
        {
            shader = ProtoMan.Index(inRange ? ShaderInRange : ShaderOutOfRange).InstanceUnique();
            shader.SetParameter("outline_width", (float) renderScale);
            _shaderCache.Add(key, shader);
        }

        _lastHoveredEntity = uid;
        if (_sprite.TryGetPostShader((uid, sprite), ContentPostShaderIds.InteractionOutline, out var entry) && entry.Shader == shader)
            return;

        _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.InteractionOutline, shader)
        {
            After = ContentPostShaderIds.AfterBaseEffects,
        });
    }
}
