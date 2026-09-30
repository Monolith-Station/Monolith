/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core.Overlays;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.StatusEffect;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
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

        SubscribeLocalEvent<CEZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
    }

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var localPosition = ent.Comp.LocalPosition;
        var offset = rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
        args.Offset += offset;
    }

    private void OnStartup(Entity<CEZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (sprite.SnapCardinals)
            return;

        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEZLevelBlurOverlay>();
    }
}

/// <summary>
/// Pre-animation pass for Z-level visuals.
/// Runs its <see cref="FrameUpdate"/> BEFORE <see cref="AnimationPlayerSystem"/> every render frame,
/// resetting <see cref="SpriteComponent.Offset"/> to the entity's clean base (no Z).
/// This prevents Z from accumulating across frames when no animation writes to the offset
/// (e.g. entities that only animate scale, like SlimeIceBig).
/// </summary>
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
        // Phase 1 (per render frame): strip any Z left from last frame so the animation player
        // always starts from a Z-free base, and Phase 2 can add exactly one Z contribution.
        var query = EntityQueryEnumerator<CEZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out var sprite))
        {
            var localPosition = zPhys.LocalPosition;
            _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault);
            _sprite.SetDrawDepth((uid, sprite), localPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
        }

        // Set parent-synced status effect offsets to the parent's current Z value each frame — prevents accumulation.
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

/// <summary>
/// Post-animation pass for Z-level visuals.
/// Runs its <see cref="FrameUpdate"/> AFTER <see cref="AnimationPlayerSystem"/> so that
/// whatever offset the animation player wrote to <see cref="SpriteComponent.Offset"/> this frame
/// (loop animation, one-shot swing, idle bob, etc.) is preserved, and the Z-height contribution
/// is simply added on top.  No animation code needs to know about Z levels.
/// </summary>
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
        // Phase 2: add the Z-height contribution on top of the animation-player's output.
        // At this point sprite.Offset == animationValue (or SpriteOffsetDefault if no anim ran).
        // The offset is counter-rotated by the entity's world angle so it always points world-up,
        // preventing it from orbiting the pivot when the entity has angular velocity (e.g. shurikens).
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
