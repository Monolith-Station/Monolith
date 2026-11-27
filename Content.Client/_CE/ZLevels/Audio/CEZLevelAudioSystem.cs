/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Client._CE.ZLevels.Audio;

public sealed partial class CEZLevelAudioSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _xformSys = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    private float _perLevelAttenuationDb;
    private float _floorOcclusion;

    public override void Initialize()
    {
        base.Initialize();

        _audioSystem.ProcessStreamOverride += ProcessStreamZAware;
        Subs.CVar(_cfg, CCVars.CEZLevelsAudioPerLevelAttenuation, value => _perLevelAttenuationDb = value, true);
        Subs.CVar(_cfg, CCVars.CEZLevelsAudioFloorOcclusion, value => _floorOcclusion = value, true);
    }

    public override void Shutdown()
    {
        _audioSystem.ProcessStreamOverride -= ProcessStreamZAware;
        base.Shutdown();
    }

    // This replaces Robust's stream processor; keep same-map behavior in sync with it.
    private void ProcessStreamZAware(EntityUid entity, AudioComponent component, TransformComponent xform, MapCoordinates listener)
    {
        if (!component.Started)
        {
            component.Started = true;
            component.StartPlaying();
        }

        var sameMap = listener.MapId == xform.MapID;
        if (component.Global)
        {
            if (!sameMap && xform.MapID != MapId.Nullspace)
                component.Gain = 0f;
            else
                component.Volume = component.Params.Volume;

            return;
        }

        var levelOffset = 0f;
        var upperAltitude = float.PositiveInfinity;
        EntityUid? listenerMap = null;
        if (!sameMap &&
            (xform.MapUid is not { } sourceMap ||
             !_maps.TryGetMap(listener.MapId, out listenerMap) ||
             !TryGetAudioOffset(listenerMap.Value, sourceMap, out levelOffset, out upperAltitude)))
        {
            component.Gain = 0f;
            return;
        }

        var parent = xform.ParentUid;
        component.Volume = component.Params.Volume - MathF.Abs(levelOffset) * _perLevelAttenuationDb;
        var worldPos = (component.Flags & AudioFlags.GridAudio) != 0
            ? _maps.GetGridPosition(parent)
            : _xformSys.GetWorldPosition(entity);

        var delta = worldPos - listener.Position;
        var distance = delta.Length();
        if (_audioSystem.GetAudioDistance(distance) > component.MaxDistance)
        {
            component.Gain = 0f;
            return;
        }

        if (distance is > 0f and < 0.01f)
        {
            worldPos = listener.Position;
            delta = default;
            distance = 0f;
        }

        if ((component.Flags & AudioFlags.NoOcclusion) != 0)
            component.Occlusion = 0f;
        else if (sameMap)
            component.Occlusion = _audioSystem.GetOcclusion(listener, delta, distance, parent);
        else
        {
            // Raycasts can't cross maps, so check the floor above the lower side.
            var opaque = levelOffset switch
            {
                > 0f => _zLevels.HasOpaqueAbove(listener.Position, listenerMap!.Value, upperAltitude),
                < 0f => _zLevels.HasOpaqueAbove(worldPos, xform.MapUid!.Value, upperAltitude),
                _ => false,
            };
            component.Occlusion = opaque ? _floorOcclusion : 0f;
        }

        component.Position = worldPos;
        if (_physicsQuery.TryComp(parent, out var physics))
            component.Velocity = _physics.GetMapLinearVelocity(parent, physics);
    }

    private bool TryGetAudioOffset(EntityUid listenerMap, EntityUid sourceMap, out float offset, out float upperAltitude)
    {
        offset = 0f;
        upperAltitude = float.PositiveInfinity;

        if (_zLevels.TryGetZLevelOffset(listenerMap, sourceMap, out var fixedOffset))
        {
            offset = fixedOffset;
            return fixedOffset <= CESharedZLevelsSystem.MaxZLevelsAboveRendering &&
                   fixedOffset >= -CESharedZLevelsSystem.MaxZLevelsBelowRendering;
        }

        if (!TryGetAudioAltitude(listenerMap, out var listenerNetwork, out var listenerAltitude) ||
            !TryGetAudioAltitude(sourceMap, out var sourceNetwork, out var sourceAltitude) ||
            listenerNetwork != sourceNetwork)
            return false;

        // Transit PVS follows the anchors. Fade by actual height without a floor-count cutoff.
        offset = sourceAltitude - listenerAltitude;
        upperAltitude = MathF.Max(sourceAltitude, listenerAltitude);
        return true;
    }

    private bool TryGetAudioAltitude(EntityUid map, out EntityUid network, out float altitude)
    {
        network = default;
        altitude = 0f;
        var anchor = map;
        if (TryComp<CEZTransitMapComponent>(map, out var transit))
        {
            if (transit.LowerMap is not { } lower)
                return false;

            anchor = lower;
            altitude = _zLevels.GetTransitProgress(transit) * _zLevels.GetTransitSpan(transit);
        }

        if (!TryComp<CEZMapComponent>(anchor, out var level) || !level.NetworkUid.IsValid())
            return false;

        network = level.NetworkUid;
        altitude += level.Depth;
        return true;
    }
}
