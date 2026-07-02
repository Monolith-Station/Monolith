/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client._CE.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class CEClientZLevelsSystem : CESharedZLevelsSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    public static float ZLevelOffset = 0.7f;

    private EntityQuery<MapGridComponent> _clientGridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _clientGridQuery = GetEntityQuery<MapGridComponent>();
        _overlay.AddOverlay(new CEZLevelBlurOverlay());

        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
    }

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var localPosition = GetVisualsLocalPosition((ent, ent), Transform(ent));
        var offset = rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
        args.Offset += offset;
    }

    protected override void ZPhysicsStartup(Entity<CEZPhysicsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (sprite.SnapCardinals)
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var transitQuery = EntityQueryEnumerator<CEZTransitMapComponent, MapLightComponent>();
        while (transitQuery.MoveNext(out _, out var transit, out var light))
        {
            if (transit.LowerMap is not { } lowerMap || transit.UpperMap is not { } upperMap)
                continue;

            var lowerColor = TryComp<MapLightComponent>(lowerMap, out var lowerLight)
                ? lowerLight.AmbientLightColor
                : MapLightComponent.DefaultColor;
            var upperColor = TryComp<MapLightComponent>(upperMap, out var upperLight)
                ? upperLight.AmbientLightColor
                : MapLightComponent.DefaultColor;

            var progress = 0f;
            if (transit.PrimaryGrid is { } grid && ZPhyzQuery.TryComp(grid, out var zPhys))
                progress = Math.Clamp(zPhys.LocalPosition, 0f, 1f);

            light.AmbientLightColor = Color.InterpolateBetween(lowerColor, upperColor, progress);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEZPhysicsComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out var sprite, out var xform))
        {
            var localPosition = GetVisualsLocalPosition((uid, zPhys), xform);

            sprite.NoRotation = localPosition != 0 || zPhys.NoRotDefault;

            _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset));
            _sprite.SetDrawDepth((uid, sprite), localPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
        }
    }


    public float GetVisualsLocalPosition(Entity<CEZPhysicsComponent?> ent, TransformComponent? xform = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;
        if (!Resolve(ent, ref xform, false))
            return 0;

        var pos = ent.Comp.LocalPosition;

        // Grid parents do NOT propagate: a grid's altitude is rendered by the transit
        // map viewport pass, which already moves everything aboard (tiles included).
        // Adding a sprite offset on top double-shifts passengers, and the forced
        // NoRotation breaks rotated multi-tile sprites. Non-grid z-movers still propagate.
        if (xform.ParentUid != xform.MapUid &&
            !_clientGridQuery.HasComp(xform.ParentUid) &&
            ZPhyzQuery.TryComp(xform.ParentUid, out var parentZPhys))
        {
            pos = parentZPhys.LocalPosition;
        }

        return pos;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEZLevelBlurOverlay>();
    }
}
