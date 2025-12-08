using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Interaction;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Climbing.Systems;
using Content.Shared._NF.CryoSleep;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._NF.CCVar;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Verbs;
using Robust.Server.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.Ghost;
using Content.Shared.Roles;
using Content.Server._NF.Shuttles.Components;

namespace Content.Server._NF.CryoSleep;

public sealed partial class CryoSleepSystem : SharedCryoSleepSystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly EuiManager _euiManager = null!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!; // For the FoundOrganics method
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private readonly Dictionary<NetUserId, StoredBody?> _storedBodies = new();
    private EntityUid? _storageMap;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryoSleepComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<CryoSleepComponent, GetVerbsEvent<InteractionVerb>>(AddInsertOtherVerb);
        SubscribeLocalEvent<CryoSleepComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<CryoSleepComponent, SuicideEvent>(OnSuicide);
        SubscribeLocalEvent<CryoSleepComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<CryoSleepComponent, DestructionEventArgs>((e,c,_) => EjectBody(e, c));
        SubscribeLocalEvent<CryoSleepComponent, CryoStoreDoAfterEvent>(OnAutoCryoSleep);
        SubscribeLocalEvent<CryoSleepComponent, DragDropTargetEvent>(OnEntityDragDropped);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);

        InitReturning();
    }

    private Vector2 _cryoCoords = Vector2.Zero; // Mono - Initial cryo body location in the cryo map.
    private readonly Vector2 _cryoDistance = Vector2.Create(0, 1); // Mono - Amount to increment body location after each cryo.
    private EntityUid GetStorageMap()
    {
        if (Deleted(_storageMap))
        {
            var map = _mapManager.CreateMap();
            _storageMap = _mapManager.GetMapEntityId(map);
            _mapManager.SetMapPaused(map, true);
        }

        return _storageMap.Value;
    }

    private void OnInit(EntityUid uid, CryoSleepComponent component, ComponentStartup args)
    {
        component.BodyContainer = _container.EnsureContainer<ContainerSlot>(uid, "body_container");
    }

    private void AddInsertOtherVerb(EntityUid uid, CryoSleepComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // If the user is currently holding/pulling an entity that can be cryo-sleeped, add a verb for that.
        if (args.Using is { Valid: true } @using &&
            !IsOccupied(component) &&
            _interaction.InRangeUnobstructed(@using, args.Target) &&
            _actionBlocker.CanMove(@using) &&
            HasComp<MindContainerComponent>(@using))
        {
            var name = "Unknown";
            if (TryComp<MetaDataComponent>(args.Using.Value, out var metadata))
                name = metadata.EntityName;

            InteractionVerb verb = new()
            {
                Act = () => InsertBody(@using, component, false),
                Category = VerbCategory.Insert,
                Text = name
            };
            args.Verbs.Add(verb);
        }
    }

    private void AddAlternativeVerbs(EntityUid uid, CryoSleepComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Eject verb
        if (IsOccupied(component))
        {
            AlternativeVerb verb = new()
            {
                Act = () => EjectBody(uid, component),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("medical-scanner-verb-noun-occupant")
            };
            args.Verbs.Add(verb);
        }

        // Self-insert verb
        if (!IsOccupied(component) &&
            (_actionBlocker.CanMove(args.User))) // || HasComp<WheelchairBoundComponent>(args.User))) // just get working legs
        {
            AlternativeVerb verb = new()
            {
                Act = () => InsertBody(args.User, component, false),
                Category = VerbCategory.Insert,
                Text = Loc.GetString("medical-scanner-verb-enter")
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnSuicide(EntityUid uid, CryoSleepComponent component, SuicideEvent args)
    {
        if (args.Handled)
            return;

        if (args.Victim != component.BodyContainer.ContainedEntity)
            return;

        QueueDel(args.Victim);
        _audio.PlayPvs(component.LeaveSound, uid);
        args.Handled = true;
    }

    private void OnExamine(EntityUid uid, CryoSleepComponent component, ExaminedEvent args)
    {
        var message = component.BodyContainer.ContainedEntity == null
            ? "cryopod-examine-empty"
            : "cryopod-examine-occupied";

        args.PushMarkup(Loc.GetString(message));
    }

    private void OnAutoCryoSleep(EntityUid uid, CryoSleepComponent component, CryoStoreDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var pod = args.Used;
        var body = args.Target;
        if (body is not { Valid: true } || pod is not { Valid: true })
            return;

        CryoStoreBody(body.Value, pod.Value);
        args.Handled = true;
    }

    private void OnEntityDragDropped(EntityUid uid, CryoSleepComponent component, DragDropTargetEvent args)
    {
        if (InsertBody(args.Dragged, component, false))
        {
            args.Handled = true;
        }
    }

    public bool InsertBody(EntityUid? toInsert, CryoSleepComponent component, bool force)
    {
        var cryopod = component.Owner;
        if (toInsert == null)
            return false;
        if (IsOccupied(component) && !force)
            return false;

        var mobQuery = GetEntityQuery<MobStateComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        // Refuse to accept "passengers" (e.g. pet felinids in bags)
        string? name = _shipyard.FoundOrganics(toInsert.Value, mobQuery, xformQuery);
        if (name is not null)
        {
            _popup.PopupEntity(Loc.GetString("cryopod-refuse-organic", ("cryopod", cryopod), ("name", name)), cryopod, PopupType.SmallCaution);
            return false;
        }

        // Refuse to accept dead or crit bodies, as well as non-mobs
        if (!TryComp<MobStateComponent>(toInsert, out var mob) || !_mobSystem.IsAlive(toInsert.Value, mob))
        {
            _popup.PopupEntity(Loc.GetString("cryopod-refuse-dead", ("cryopod", cryopod)), cryopod, PopupType.SmallCaution);
            return false;
        }

        // If the inserted player has disconnected, it will be stored immediately.
        if (_mind.TryGetMind(toInsert.Value, out var mind, out var mindComp))
        {
            var session = mindComp.Session;
            if (session is not null && session.Status == SessionStatus.Disconnected)
            {
                CryoStoreBody(toInsert.Value, cryopod);
                return true;
            }
        }

        var success = _container.Insert(toInsert.Value, component.BodyContainer);

        if (success && mindComp?.Session != null)
        {
            _euiManager.OpenEui(new CryoSleepEui(toInsert.Value,  cryopod, this), mindComp.Session);
        }

        if (success)
        {
            // Start a do-after event - if the inserted body is still inside and has not decided to sleep/leave, it will be stored.
            // It does not matter whether the entity has a mind or not.
            var ev = new CryoStoreDoAfterEvent();
            var args = new DoAfterArgs(
                _entityManager,
                toInsert.Value,
                TimeSpan.FromSeconds(30),
                ev,
                cryopod,
                toInsert,
                cryopod
            )
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = true
            };

            if (_doAfter.TryStartDoAfter(args))
                component.CryosleepDoAfter = ev.DoAfter.Id;
        }

        return success;
    }

    public void CryoStoreBody(EntityUid bodyId, EntityUid cryopod)
    {
        if (!TryComp<CryoSleepComponent>(cryopod, out var cryo))
            return;

        NetUserId? id = null;
        var characterName = "Unknown";
        string? jobTitle = null;

        if (_mind.TryGetMind(bodyId, out var mindEntity, out var mind) && mind.CurrentEntity is { Valid : true } body)
        {
            var argMind = mind;
            RaiseLocalEvent(bodyId, new CryosleepBeforeMindRemovedEvent(cryopod, argMind?.UserId), true);
            _ghost.OnGhostAttempt(mindEntity, false, true, mind: mind);

            id = mind.UserId;
            if (id != null)
            {
                _storedBodies[id.Value] = new StoredBody() { Body = body, Cryopod = cryopod };

                // Get the player's current job prototype, first from mind, then from component
                string? currentJobPrototype = null;

                // Try to get the job from the mind first
                if (_jobs.MindTryGetJobId(mindEntity, out var jobId) && jobId != null)
                {
                    currentJobPrototype = jobId;
                }
                // If no job in mind (e.g. disconnected player), try to get from the entity component
                else if (TryComp<PlayerJobComponent>(bodyId, out var playerJob) && playerJob.JobPrototype != null)
                {
                    currentJobPrototype = playerJob.JobPrototype;
                }

                // Only reopen the job slot for the player's current job
                if (currentJobPrototype != null)
                {
                    // Get the spawn station from the player job component
                    EntityUid? playerStation = null;
                    if (TryComp<PlayerJobComponent>(bodyId, out var playerJob))
                    {
                        playerStation = playerJob.SpawnStation;
                    }

                    // Check if any of the station's grids have ForceAnchor
                    bool stationHasForceAnchor = false;

                    if (playerStation != null && EntityManager.EntityExists(playerStation.Value) &&
                        _entityManager.TryGetComponent<StationDataComponent>(playerStation.Value, out var stationData))
                    {
                        foreach (var gridUid in stationData.Grids)
                        {
                            if (HasComp<ForceAnchorComponent>(gridUid))
                            {
                                stationHasForceAnchor = true;
                                //Log.Info($"Found ForceAnchor on grid {ToPrettyString(gridUid)} for station {ToPrettyString(playerStation.Value)}");
                                break;
                            }
                        }
                        //Log.Info($"Station {ToPrettyString(playerStation.Value)} has ForceAnchor: {stationHasForceAnchor} (checked {stationData.Grids.Count} grids)");
                    }
                    else
                    {
                        //Log.Info($"Could not get StationDataComponent for station {(playerStation != null ? ToPrettyString(playerStation.Value) : "null")}");
                    }

                    // Only proceed if we found a valid station for this player and it has ForceAnchor
                    if (playerStation != null && EntityManager.EntityExists(playerStation.Value) &&
                        _entityManager.TryGetComponent<StationJobsComponent>(playerStation.Value, out var stationJobs) &&
                        stationHasForceAnchor)
                    {
                        // For connected players, we check their job assignments
                        if (id != null && _stationJobs.TryGetPlayerJobs(playerStation.Value, id.Value, out var jobs, stationJobs))
                        {
                            // Only adjust the slot for their current job - increasing the available slots by 1
                            if (jobs.Contains(currentJobPrototype))
                            {
                                _stationJobs.TryAdjustJobSlot(playerStation.Value, currentJobPrototype, 1, clamp: true);
                                Log.Debug($"Reopened job slot '{currentJobPrototype}' on station {ToPrettyString(playerStation.Value)} after {characterName} entered cryosleep");
                            }

                            // Still need to remove the player from all job assignments
                            _stationJobs.TryRemovePlayerJobs(playerStation.Value, id.Value, stationJobs);
                        }
                        // For disconnected players or other cases, we just try to reopen the job slot directly
                        else
                        {
                            _stationJobs.TryAdjustJobSlot(playerStation.Value, currentJobPrototype, 1, clamp: true, createSlot: true);
                            Log.Debug($"Reopened job slot '{currentJobPrototype}' on station {ToPrettyString(playerStation.Value)} after {characterName} entered cryosleep (direct adjustment)");
                        }
                    }
                }
            }

            if (mind.CharacterName != null)
                characterName = mind.CharacterName;

            // Get the job title if available
            jobTitle = _jobs.MindTryGetJobName(mindEntity);
        }
        else if (TryComp<MetaDataComponent>(bodyId, out var metadata))
        {
            characterName = metadata.EntityName;
        }

        var storage = GetStorageMap();
        var bodyTransform = Transform(bodyId);
        _container.Remove(bodyId, cryo.BodyContainer, reparent: false, force: true);
        bodyTransform.Coordinates = new EntityCoordinates(storage, _cryoCoords); // Mono, replaced Vector.Zero with _cryoCoords. Sets body to this location on cryomap.
        _cryoCoords = Vector2.Add(_cryoCoords, _cryoDistance); // Mono - Increments for next body.

        RaiseLocalEvent(bodyId, new CryosleepEnterEvent(cryopod, mind?.UserId), true);

        if (cryo.CryosleepDoAfter != null && _doAfter.GetStatus(cryo.CryosleepDoAfter) == DoAfterStatus.Running)
            _doAfter.Cancel(cryo.CryosleepDoAfter);

        // Get the pod's location information for the radio message
        string message;
        var podTransform = Transform(cryopod);
        var coordinates = _entityManager.GetComponent<TransformComponent>(cryopod).Coordinates;
        var mapPos = coordinates.ToMap(_entityManager, EntityManager.System<SharedTransformSystem>());

        // Check if it's at a named location (like a station or outpost)
        if (podTransform.GridUid != null && _entityManager.TryGetComponent<MetaDataComponent>(podTransform.GridUid.Value, out var gridMetadata))
        {
            message = Loc.GetString("cryopod-radio-location",
                ("character", characterName),
                ("location", gridMetadata.EntityName)); // Mono: They don't tell coords now
        }
        else
        {
            // If not at a named location, use coordinates
            message = Loc.GetString("cryopod-radio-coordinates",
                ("character", characterName),
                ("x", Math.Round(mapPos.Position.X)),
                ("y", Math.Round(mapPos.Position.Y)));
        }

        // Check if character is a pirate, and if so, use Freelancer radio instead of Common
        bool isPirate = false;
        if (jobTitle != null)
        {
            // Check if job is one of the pirate jobs
            isPirate = jobTitle.Equals(Loc.GetString("job-name-pirate"), StringComparison.OrdinalIgnoreCase) ||
                       jobTitle.Equals(Loc.GetString("job-name-pirate-captain"), StringComparison.OrdinalIgnoreCase) ||
                       jobTitle.Equals(Loc.GetString("job-name-pirate-first-mate"), StringComparison.OrdinalIgnoreCase);
        }

        // Check if character is TSF, and if so, use TSF radio instead of Common
        bool isTSF = false;
        if (jobTitle != null)
        {
            isTSF = jobTitle.Equals(Loc.GetString("job-name-bailiff"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-brigmedic"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-cadet-nf"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-deputy"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-nf-detective"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-sheriff"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-stc"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-sr"), StringComparison.OrdinalIgnoreCase) ||
                    jobTitle.Equals(Loc.GetString("job-name-pal"), StringComparison.OrdinalIgnoreCase);
        }

        // Send radio message on appropriate channel
        if (isPirate)
        {
            // Use Freelancer channel for pirates
            if (_prototypeManager.TryIndex<RadioChannelPrototype>("Freelance", out var freelanceChannel))
            {
                _radioSystem.SendRadioMessage(cryopod, message, freelanceChannel, cryopod);
            }
        }
        else if (isTSF)
        {
            // Use TSF channel for TSF - Mono
            if (_prototypeManager.TryIndex<RadioChannelPrototype>("Nfsd", out var nfsdChannel))
            {
                _radioSystem.SendRadioMessage(cryopod, message, nfsdChannel, cryopod);
            }
        }
        else
        {
            // Use Common channel for everyone else
            if (_prototypeManager.TryIndex<RadioChannelPrototype>(SharedChatSystem.CommonChannel, out var commonChannel))
            {
                _radioSystem.SendRadioMessage(cryopod, message, commonChannel, cryopod);
            }
        }

        // Start a timer. When it ends, the body needs to be deleted.
        Timer.Spawn(TimeSpan.FromSeconds(_configurationManager.GetCVar(NFCCVars.CryoExpirationTime)), () =>
        {
            if (id != null)
                ResetCryosleepState(id.Value);

            if (!Deleted(bodyId) && Transform(bodyId).ParentUid == _storageMap)
                QueueDel(bodyId);
        });
    }

    /// <param name="body">If not null, will not eject if the stored body is different from that parameter.</param>
    public bool EjectBody(EntityUid pod, CryoSleepComponent? component = null, EntityUid? body = null)
    {
        if (!Resolve(pod, ref component))
            return false;

        if (!IsOccupied(component) || (body != null && component.BodyContainer.ContainedEntity != body))
            return false;

        var toEject = component.BodyContainer.ContainedEntity;
        if (toEject == null)
            return false;

        _container.Remove(toEject.Value, component.BodyContainer, force: true);
        //_climb.ForciblySetClimbing(toEject.Value, pod);

        if (component.CryosleepDoAfter != null && _doAfter.GetStatus(component.CryosleepDoAfter) == DoAfterStatus.Running)
            _doAfter.Cancel(component.CryosleepDoAfter);

        return true;
    }

    private bool IsOccupied(CryoSleepComponent component)
    {
        return component.BodyContainer.ContainedEntity != null;
    }

    private void OnRoundEnded(RoundEndedEvent args)
    {
        _storedBodies.Clear();
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // Store the job prototype and spawn station on the player's entity
        if (!string.IsNullOrEmpty(ev.JobId))
        {
            var jobComp = EnsureComp<PlayerJobComponent>(ev.Mob);
            jobComp.JobPrototype = ev.JobId;
            jobComp.SpawnStation = ev.Station;
            Log.Debug($"Stored job '{ev.JobId}' on station {ToPrettyString(ev.Station)} for player {MetaData(ev.Mob).EntityName}");
        }
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        // In this event, we don't have direct access to the role information
        // We need to check if a JobRoleComponent was added to any of the mind's roles

        // Get the entity owned by this mind
        var mindEntity = args.MindId;
        if (!_mind.TryGetSession(mindEntity, out var session) ||
            session.AttachedEntity is not { Valid: true } playerEntity)
            return;

        // Check if this mind has a job role
        if (_jobs.MindTryGetJobId(mindEntity, out var jobId) && jobId != null)
        {
            // Get the existing component if it exists
            EntityUid? spawnStation = null;
            if (TryComp<PlayerJobComponent>(playerEntity, out var existingJob))
            {
                // Preserve the original spawn station
                spawnStation = existingJob.SpawnStation;
            }

            // Update the PlayerJobComponent with the job ID while preserving station
            var jobComp = EnsureComp<PlayerJobComponent>(playerEntity);
            jobComp.JobPrototype = jobId;

            // Only set the station if we didn't have one before
            if (spawnStation != null && jobComp.SpawnStation == null)
            {
                jobComp.SpawnStation = spawnStation;
            }
        }
    }

    private struct StoredBody
    {
        public EntityUid Body;
        public EntityUid Cryopod;
    }
}

