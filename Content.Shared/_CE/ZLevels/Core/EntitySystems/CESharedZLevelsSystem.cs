/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem : EntitySystem
{
    [Dependency] private INetManager _net = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IConfigurationManager _config = null!;
    [Dependency] private IMapManager _mapManager = null!;

    [Dependency] private SharedPhysicsSystem _physicsSystem = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private ActionBlockerSystem _blocker = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedMapSystem _map = null!;
    [Dependency] private SharedPopupSystem _popup = null!;

    protected EntityQuery<CEZPhysicsComponent> ZPhysicsQuery = default!;

    private EntityQuery<MapComponent> _mapQuery = default!;
    private EntityQuery<MapGridComponent> _gridQuery = default!;
    private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    private EntityQuery<TransformComponent> _transformQuery = default!;

    private EntityQuery<CEZMapComponent> _zMapQuery = default!;
    private EntityQuery<CEZMapNetworkComponent> _zNetworkQuery = default!;
    private EntityQuery<CEZLevelHighGroundComponent> _zHighGroundQuery = default!;

    private bool _clientSimulation;
    private TimeSpan _fixedTimestep;

    public override void Initialize()
    {
        base.Initialize();

        ZPhysicsQuery = GetEntityQuery<CEZPhysicsComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _zMapQuery = GetEntityQuery<CEZMapComponent>();
        _zNetworkQuery = GetEntityQuery<CEZMapNetworkComponent>();
        _zHighGroundQuery = GetEntityQuery<CEZLevelHighGroundComponent>();

        _config.OnValueChanged(CCVars.ZLevelsPhysicsClientSimulation, i => _clientSimulation = i, true);
        _config.OnValueChanged(CCVars.ZLevelsPhysicsTickRate, i => _fixedTimestep = TimeSpan.FromSeconds(1d / i), true);

        InitializeActivation();
        InitializeCacheHooks();
        InitializeMovement();
        InitializeView();
    }
}
