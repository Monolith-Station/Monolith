using Content.Server._NF.SectorServices;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;

namespace Content.Server._Mono.AlertLevel;

public sealed partial class WarLevelSystem : EntitySystem
{
    [Dependency] private SectorServiceSystem _sectorService = default!;
    [Dependency] private ChatSystem _chatSystem = default!;

    public bool GetWarLevel(EntityUid station, WarLevelComponent? alert = null)
    {
        if (!TryComp(_sectorService.GetServiceEntity(), out alert))
            return false;

        return alert.PostWar;
    }

    public void SetLevel(bool level, WarLevelComponent? component = null)
    {
        EntityUid sectorEnt = _sectorService.GetServiceEntity();
        if (!TryComp<WarLevelComponent>(sectorEnt, out component))
        {
            Log.Error($"Unable to find WarLevelComponent for entity {sectorEnt}");
            return;
        }

        component.PostWar = level;
        Log.Info($"Setting WarLevelComponent for entity {sectorEnt} to {component.PostWar}. Input value {level}");
            _chatSystem.DispatchGlobalAnnouncement(
                level ? Loc.GetString("war-level-announcement-post") :  Loc.GetString("war-level-announcement-pre"),
                sender: Loc.GetString("war-level-announcement-sender"),
                playSound: level,
                announcementSound: level ? new SoundPathSpecifier("/Audio/Misc/gamma.ogg") : new SoundPathSpecifier("/Audio/Announcements/notice2.ogg"),
                colorOverride: level ? Color.Crimson : Color.CornflowerBlue);

        RaiseLocalEvent(new WarLevelChangedEvent(level)); // Frontier: pass invalid, we have no station
    }
}

public sealed class WarLevelChangedEvent : EntityEventArgs
{
    public bool WarLevel { get; }

    public WarLevelChangedEvent(bool alertLevel)
    {
        WarLevel = alertLevel;
    }
}
