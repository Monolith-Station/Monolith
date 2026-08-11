using Content.Server.Chat.Systems;
using Content.Shared._Forge.Silicons.StationAi;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Forge.Silicons.StationAi;

public sealed class StationAiCameraSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _activeByGrid = new();
    private readonly Dictionary<EntityUid, EntityUid> _brainGrids = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiHeldComponent, ResolveLocalSpeechOriginEvent>(OnResolveSpeechOrigin);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<StationAiPersonalityComponent, ComponentStartup>(OnPersonalityStartup);
        SubscribeLocalEvent<StationAiPersonalityComponent, StationAiAvailabilityChangedEvent>(OnAvailabilityChanged);
        SubscribeLocalEvent<StationAiCoreComponent, EntParentChangedMessage>(OnCoreParentChanged);
    }

    private void OnResolveSpeechOrigin(Entity<StationAiHeldComponent> ent, ref ResolveLocalSpeechOriginEvent args)
    {
        if (!_stationAi.TryGetCore(ent.Owner, out var core) ||
            core.Comp is not { Remote: true, RemoteEntity: { } eye } ||
            Transform(core.Owner).GridUid is not { } coreGrid)
        {
            return;
        }

        var eyeCoordinates = _transform.GetMapCoordinates(eye);
        var eyePosition = eyeCoordinates.Position;
        var coreCoordinates = _transform.GetMapCoordinates(core.Owner);
        var closest = core.Owner;
        var closestDistance = coreCoordinates.MapId == eyeCoordinates.MapId
            ? (coreCoordinates.Position - eyePosition).LengthSquared()
            : float.MaxValue;

        foreach (var camera in _lookup.GetEntitiesInRange<StationAiCameraRelayComponent>(
                     eyeCoordinates,
                     StationAiCameraRelayComponent.DefaultRange))
        {
            if (Transform(camera).GridUid != coreGrid || !IsOperational(camera))
                continue;

            var distance = (_transform.GetMapCoordinates(camera).Position - eyePosition).LengthSquared();
            if (distance > camera.Comp.Range * camera.Comp.Range || distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = camera.Owner;
        }

        args.Origin = closest;
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (ev.Channel is not (ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Emotes) ||
            Transform(ev.SpeechOrigin).GridUid is not { } grid ||
            !_activeByGrid.TryGetValue(grid, out var activeBrains) ||
            activeBrains.Count == 0)
        {
            return;
        }

        var sourceCoordinates = _transform.GetMapCoordinates(ev.SpeechOrigin);
        var sourcePosition = sourceCoordinates.Position;
        EntityUid? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var camera in _lookup.GetEntitiesInRange<StationAiCameraRelayComponent>(sourceCoordinates, ev.VoiceRange))
        {
            if (Transform(camera).GridUid != grid || !IsOperational(camera))
                continue;

            var distance = (_transform.GetMapCoordinates(camera).Position - sourcePosition).Length();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = camera.Owner;
        }

        if (closest == null)
            return;

        foreach (var brain in activeBrains)
        {
            if (TryComp(brain, out ActorComponent? actor))
            {
                ev.Recipients.TryAdd(
                    actor.PlayerSession,
                    new ChatSystem.ICChatRecipientData(closestDistance, false, HearingEntity: closest));
            }
        }
    }

    private bool IsOperational(Entity<StationAiCameraRelayComponent> camera)
    {
        return !TerminatingOrDeleted(camera.Owner) &&
               TryComp(camera.Owner, out SurveillanceCameraComponent? surveillance) &&
               surveillance.Active &&
               _power.IsPowered(camera.Owner) &&
               TryComp(camera.Owner, out StationAiVisionComponent? vision) &&
               vision.Enabled;
    }

    private void OnPersonalityStartup(Entity<StationAiPersonalityComponent> ent, ref ComponentStartup args)
    {
        RefreshBrain(ent.Owner);
    }

    private void OnAvailabilityChanged(Entity<StationAiPersonalityComponent> ent, ref StationAiAvailabilityChangedEvent args)
    {
        if (args.Remove)
            RemoveBrain(ent.Owner);
        else
            RefreshBrain(ent.Owner);
    }

    private void OnCoreParentChanged(Entity<StationAiCoreComponent> ent, ref EntParentChangedMessage args)
    {
        if (_stationAi.TryGetHeld(new Entity<StationAiCoreComponent?>(ent.Owner, ent.Comp), out var brain))
            RefreshBrain(brain);
    }

    private void RefreshBrain(EntityUid brain)
    {
        RemoveBrain(brain);
        if (!TryGetActiveGrid(brain, out var grid))
            return;

        if (!_activeByGrid.TryGetValue(grid, out var brains))
        {
            brains = new HashSet<EntityUid>();
            _activeByGrid.Add(grid, brains);
        }

        brains.Add(brain);
        _brainGrids[brain] = grid;
    }

    private bool TryGetActiveGrid(EntityUid brain, out EntityUid grid)
    {
        grid = default;
        if (!TryComp(brain, out MindContainerComponent? container) ||
            container.Mind is not { } mindUid ||
            !TryComp(mindUid, out MindComponent? mind) ||
            mind.OwnedEntity != brain ||
            mind.UserId == null ||
            !_stationAi.TryGetCore(brain, out var core) ||
            !_stationAi.TryGetHeld(core, out var held) ||
            held != brain ||
            Transform(core.Owner).GridUid is not { } coreGrid)
        {
            return false;
        }

        grid = coreGrid;
        return true;
    }

    private void RemoveBrain(EntityUid brain)
    {
        if (!_brainGrids.Remove(brain, out var grid) || !_activeByGrid.TryGetValue(grid, out var brains))
            return;

        brains.Remove(brain);
        if (brains.Count == 0)
            _activeByGrid.Remove(grid);
    }
}
