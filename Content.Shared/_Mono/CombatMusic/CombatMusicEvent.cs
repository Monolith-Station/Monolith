using Robust.Shared.Serialization;

namespace Content.Shared._Mono.CombatMusic;

/// <summary>
/// Sent by the server to clients to start combat music locally.
/// </summary>
[Serializable, NetSerializable]
public sealed class CombatMusicStartEvent : EntityEventArgs
{
    public string SoundPath { get; }
    public float VolumeDb { get; }
    public bool Loop { get; }

    public CombatMusicStartEvent(string soundPath, float volumeDb, bool loop)
    {
        SoundPath = soundPath;
        VolumeDb = volumeDb;
        Loop = loop;
    }
}

/// <summary>
/// Sent by the server to clients to stop combat music locally.
/// </summary>
[Serializable, NetSerializable]
public sealed class CombatMusicStopEvent : EntityEventArgs
{
    /// <summary>
    /// How long to fade out the music before stopping.
    /// </summary>
    public float FadeOutDuration { get; }

    public CombatMusicStopEvent(float fadeOutDuration = 0f)
    {
        FadeOutDuration = fadeOutDuration;
    }
}


