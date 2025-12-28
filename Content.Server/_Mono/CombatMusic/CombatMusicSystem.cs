using Content.Shared._Mono.CombatMusic;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mono.CombatMusic;

/// <summary>
/// System that manages combat music playback when gunnery control fires.
/// </summary>
public sealed class CombatMusicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = AllEntityQuery<CombatMusicComponent>();

        while (query.MoveNext(out var gridUid, out var comp))
        {
            if (!comp.MusicPlaying)
                continue;

            var timeSinceLastShot = curTime - comp.LastFiringTime;
            var remainingTime = comp.MusicTimeout - timeSinceLastShot.TotalSeconds;

            if (!comp.FadeInitiated && remainingTime <= comp.FadeOutDuration && remainingTime > 0)
            {
                comp.FadeInitiated = true;
                var filter = Filter.Empty().AddInGrid(gridUid, EntityManager);
                RaiseNetworkEvent(new CombatMusicStopEvent(comp.FadeOutDuration), filter);
            }

            if (timeSinceLastShot.TotalSeconds >= comp.MusicTimeout)
            {
                StopCombatMusic(gridUid, comp);
            }
        }
    }

    /// <summary>
    /// Triggers combat music for a grid, starting it if not already playing
    /// </summary>
    public void TriggerCombatMusic(EntityUid gridUid)
    {
        var comp = EnsureComp<CombatMusicComponent>(gridUid);

        comp.LastFiringTime = _timing.CurTime;

        if (!comp.MusicPlaying)
        {
            StartCombatMusic(gridUid, comp);
        }
    }

    /// <summary>
    /// Starts playing combat music for all players on the grid.
    /// </summary>
    private void StartCombatMusic(EntityUid gridUid, CombatMusicComponent comp)
    {
        if (comp.CombatMusicSounds.Count == 0)
        {
            Logger.Warning($"CombatMusicComponent on {gridUid} has no sounds configured!");
            return;
        }

        var selectedSound = comp.CombatMusicSounds[_robustRandom.Next(0, comp.CombatMusicSounds.Count)];

        var filter = Filter.Empty().AddInGrid(gridUid, EntityManager);

        var path = ((SoundPathSpecifier) selectedSound).Path.ToString();
        RaiseNetworkEvent(new CombatMusicStartEvent(path, comp.Volume, true), filter);

        comp.MusicPlaying = true;
        comp.FadeInitiated = false;
    }

    /// <summary>
    /// Stops combat music on the grid.
    /// </summary>
    private void StopCombatMusic(EntityUid gridUid, CombatMusicComponent comp)
    {
        if (comp.MusicStream != null && Exists(comp.MusicStream.Value))
        {
            Del(comp.MusicStream.Value);
        }

        comp.MusicStream = null;
        comp.MusicPlaying = false;

        if (!comp.FadeInitiated)
        {
            RaiseNetworkEvent(new CombatMusicStopEvent());
        }

        RemComp<CombatMusicComponent>(gridUid);
    }
}

