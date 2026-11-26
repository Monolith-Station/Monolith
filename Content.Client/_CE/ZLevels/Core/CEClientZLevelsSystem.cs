/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core.Overlays;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.StatusEffect;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client._CE.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class CEClientZLevelsSystem : CESharedZLevelsSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IEyeManager _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CEZLevelBlurOverlay());
        _overlay.AddOverlay(new CEZLevelShadowOverlay(EntityManager));

        SubscribeLocalEvent<CEZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEZPhysicsComponent, AfterAutoHandleStateEvent>(OnZPhysicsState);
        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
    }

    private void OnZPhysicsState(Entity<CEZPhysicsComponent> ent, ref AfterAutoHandleStateEvent args) =>
        QueueGroundRefresh(ent);

    internal EntityUid GetCameraAltitudeAnchor(TransformComponent xform) =>
        CompOrNull<CEHellpodComponent>(xform.ParentUid) is { InFlight: true } ? xform.ParentUid : xform.Owner;

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var xform = Transform(ent);
        var height = CompOrNull<CEZPhysicsComponent>(GetCameraAltitudeAnchor(xform))?.LocalPosition ?? ent.Comp.LocalPosition;
        var localPosition = HasComp<CEZMapComponent>(xform.MapUid) ? Math.Clamp(height, -1f, 1f) : 0f;
        var offset = rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
        args.Offset += offset;
    }

    private void OnStartup(Entity<CEZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay.TryGetOverlay<CEZLevelBlurOverlay>(out var blur))
        {
            _overlay.RemoveOverlay(blur);
            blur.Dispose();
        }
        _overlay.RemoveOverlay<CEZLevelShadowOverlay>();
    }
}

/// <summary>Removes the previous Z offset before sprite animation runs.</summary>
internal sealed partial class CEClientZLevelsPreAnimSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<CEZPhysicsComponent> _zPhysQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AnimationPlayerSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CEZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out var sprite))
        {
            var localPosition = zPhys.LocalPosition;
            _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault);
            _sprite.SetDrawDepth((uid, sprite), localPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
        }

        var syncQuery = EntityQueryEnumerator<StatusEffectsComponent, SpriteComponent, TransformComponent>();
        while (syncQuery.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var parent = xform.ParentUid;
            if (_mapGridQuery.HasComp(parent))
                continue;
            if (!_zPhysQuery.TryComp(parent, out var parentZPhys))
                continue;
            var zOffset = new Vector2(0, parentZPhys.LocalPosition * CESharedZLevelsSystem.ZLevelOffset);
            _sprite.SetOffset((uid, sprite), zOffset);
        }
    }
}

/// <summary>Applies Z height after sprite animation runs.</summary>
internal sealed partial class CEClientZLevelsPostAnimSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        // Counter-rotate so the lift always points world-up.
        var query = EntityQueryEnumerator<CEZPhysicsComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out var sprite, out var xform))
        {
            var rawZ = new Vector2(0, zPhys.LocalPosition * CESharedZLevelsSystem.ZLevelOffset);
            Vector2 zOffset;
            if (sprite.NoRotation)
                zOffset = rawZ;
            else
                zOffset = new Angle(-_xform.GetWorldRotation(xform)).RotateVec(rawZ);
            _sprite.SetOffset((uid, sprite), sprite.Offset + zOffset);
        }
    }
}
