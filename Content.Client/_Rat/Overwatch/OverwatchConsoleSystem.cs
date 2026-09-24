using Content.Shared._Rat.Overwatch;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Rat.Overwatch;

/// <summary>
/// Клиентская система для ретрансляции звуков при наблюдении через камеру Overwatch.
/// </summary>
public sealed class OverwatchConsoleSystem : EntitySystem
{
    /// <summary>
    /// Максимальное расстояние для проверки звуков (оптимизация производительности).
    /// </summary>
    private const float MaxSoundRelayDistance = 50f;

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Кэш ретранслируемых звуков для отслеживания активных ретрансляций.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _relayedSounds = new();

    /// <summary>
    /// Временный список звуков для ретрансляции в текущем кадре.
    /// </summary>
    private readonly List<(EntityUid Uid, AudioComponent Audio, RatOverwatchRelayedSoundComponent? Relay, EntityCoordinates Position)> _toRelay = new();

    private OverwatchAnnouncementOverlay _announcementOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OverwatchAnnouncementEvent>(OnAnnouncement);

        SubscribeLocalEvent<RatOverwatchRelayedSoundComponent, ComponentRemove>(OnRelayedRemove);
        SubscribeLocalEvent<RatOverwatchRelayedSoundComponent, EntityTerminatingEvent>(OnRelayedRemove);
        SubscribeLocalEvent<RatOverwatchWatchingComponent, ComponentInit>(OnLocalWatchingInit);
        SubscribeLocalEvent<RatOverwatchWatchingComponent, ComponentRemove>(OnLocalWatchingRemoved);

        _announcementOverlay = new(_cache, _timing);
        _overlay.AddOverlay(_announcementOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay(_announcementOverlay);
    }

    /// <summary>
    /// Обработчик события объявления Overwatch.
    /// </summary>
    private void OnAnnouncement(OverwatchAnnouncementEvent ev)
    {
        var title = Loc.GetString("overwatch-announcement-title",
            ("overwatchTitle", ev.OverwatchTitle),
            ("targetName", ev.TargetName));
        _announcementOverlay.SetText(title, ev.Message, ev.Color);
    }

    private void OnLocalWatchingInit(Entity<RatOverwatchWatchingComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity != ent.Owner || !ent.Comp.Watching.HasValue)
            return;

        var watchingNet = GetNetEntity(ent.Comp.Watching.Value);
        _announcementOverlay?.Reset();
    }

    private void OnLocalWatchingRemoved(Entity<RatOverwatchWatchingComponent> ent, ref ComponentRemove args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _announcementOverlay.Reset();
        CleanupAllRelayedSounds();
    }

    /// <summary>
    /// Обработчик удаления компонента ретрансляции звука.
    /// </summary>
    private void OnRelayedRemove<T>(Entity<RatOverwatchRelayedSoundComponent> ent, ref T args)
    {
        TryDeleteRelayed(ent.Comp.Relay);
    }

    /// <summary>
    /// Удаляет ретранслируемую сущность звука если она клиентская.
    /// </summary>
    private void TryDeleteRelayed(EntityUid? relay)
    {
        if (relay == null)
            return;

        if (IsClientSide(relay.Value))
            QueueDel(relay);
    }

    /// <summary>
    /// Удаляет ретранслируемый звук из кэша и очищает сущность.
    /// </summary>
    private void RemoveRelayedSound(EntityUid soundUid)
    {
        if (_relayedSounds.Remove(soundUid, out var relayedUid) && relayedUid.Valid)
        {
            if (IsClientSide(relayedUid))
                QueueDel(relayedUid);
        }
    }

    /// <summary>
    /// Очищает все ретранслируемые звуки при выходе из режима наблюдения.
    /// </summary>
    private void CleanupAllRelayedSounds()
    {
        foreach (var relayedUid in _relayedSounds.Values)
        {
            if (relayedUid.Valid && IsClientSide(relayedUid))
                QueueDel(relayedUid);
        }
        _relayedSounds.Clear();

        var relayQuery = AllEntityQuery<RatOverwatchRelayedSoundComponent>();
        while (relayQuery.MoveNext(out var uid, out var relay))
        {
            TryDeleteRelayed(relay.Relay);
            RemCompDeferred<RatOverwatchRelayedSoundComponent>(uid);
        }
    }

    /// <summary>
    /// Очищает устаревшие ретранслируемые звуки которые больше не слышны.
    /// </summary>
    private void CleanupStaleRelayedSounds(HashSet<EntityUid> activeSounds)
    {
        var toRemove = new List<EntityUid>();
        foreach (var soundUid in _relayedSounds.Keys)
        {
            if (!activeSounds.Contains(soundUid))
                toRemove.Add(soundUid);
        }

        foreach (var soundUid in toRemove)
        {
            RemoveRelayedSound(soundUid);
        }
    }

    /// <summary>
    /// Создаёт или обновляет ретранслируемый звук на новой позиции.
    /// </summary>
    private void UpdateOrCreateRelayedSound(
        EntityUid uid,
        AudioComponent audio,
        MapCoordinates eyePosition,
        Vector2 delta,
        EntityUid player)
    {
        var position = eyePosition.Offset(delta);

        if (_relayedSounds.TryGetValue(uid, out var relayedUid) && relayedUid.Valid)
        {
            _transform.SetMapCoordinates(relayedUid, position);
            return;
        }

        var entityPosition = _transform.ToCoordinates(position);
        _toRelay.Add((uid, audio, null, entityPosition));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out RatOverwatchWatchingComponent? watching) ||
            !watching.Watching.HasValue ||
            !TryComp(player, out TransformComponent? playerTransform))
        {
            CleanupAllRelayedSounds();
            return;
        }

        _toRelay.Clear();

        var eyePosition = _eye.CurrentEye.Position;
        var listenerCoords = _transform.ToCoordinates(eyePosition);
        var maxDistanceSquared = MaxSoundRelayDistance * MaxSoundRelayDistance;

        var activeSounds = new HashSet<EntityUid>();

        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (IsClientSide(uid))
                continue;

            if (eyePosition.MapId != xform.MapID)
                continue;

            var audioCoords = xform.Coordinates;
            if (!audioCoords.TryDelta(EntityManager, _transform, listenerCoords, out var delta))
                continue;

            var distanceSquared = delta.LengthSquared();

            if (distanceSquared <= audio.MaxDistance * audio.MaxDistance)
            {
                RemoveRelayedSound(uid);
                continue;
            }

            if (distanceSquared > maxDistanceSquared)
                continue;

            activeSounds.Add(uid);
            UpdateOrCreateRelayedSound(uid, audio, eyePosition, delta, player);
        }

        foreach (var (uid, audio, _, coordinates) in _toRelay)
        {
            var relayedAudio = _audio.PlayStatic(
                new SoundPathSpecifier(audio.FileName),
                player,
                coordinates,
                audio.Params
            );

            if (relayedAudio is not { Entity: var relayedAudioEnt })
                continue;

            _audio.SetPlaybackPosition(relayedAudioEnt, audio.PlaybackPosition);

            _relayedSounds[uid] = relayedAudioEnt;

            if (TryComp<RatOverwatchRelayedSoundComponent>(uid, out var relayedComp))
            {
                relayedComp.Relay = relayedAudioEnt;
            }
        }

        CleanupStaleRelayedSounds(activeSounds);
    }
}
