using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Xenoborgs.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Xenoborgs;

public sealed class XenoborgCoreSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private TimeSpan? _soundTime;
    private TimeSpan? _wipeTime;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothershipCoreComponent, DestructionEventArgs>(OnCoreDestroyed);
    }

    private void OnCoreDestroyed(EntityUid ent, MothershipCoreComponent comp, DestructionEventArgs args)
    {
        // Only trigger when LAST core is destroyed
        var coreQuery = AllEntityQuery<MothershipCoreComponent>();

        while (coreQuery.MoveNext(out var otherCore, out _))
        {
            if (otherCore != ent)
                return;
        }

        var now = _timing.CurTime;

        // Announcement: cores are gone
        _chat.DispatchGlobalAnnouncement(
            "All Mothership Cores have been destroyed. Xenoborg systems destabilizing...",
            colorOverride: Color.DarkRed);

        _soundTime = now + TimeSpan.FromSeconds(10);
        _wipeTime = now + TimeSpan.FromSeconds(15);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // warning sound at 10s
        if (_soundTime != null && now >= _soundTime)
        {
            _soundTime = null;

            _audio.PlayGlobal(
                "/Audio/Machines/warning_buzzer_xenoborg.ogg",
                Filter.Broadcast(),
                false);
        }

        // wipe at 15s
        if (_wipeTime != null && now >= _wipeTime)
        {
            _wipeTime = null;

            ExplodeAllXenoborgs();

            // final announcement AFTER wipe
            _chat.DispatchGlobalAnnouncement(
                "Xenoborg systems terminated. All units destroyed.",
                colorOverride: Color.DarkRed);
        }
    }

    private void ExplodeAllXenoborgs()
    {
        var query = AllEntityQuery<XenoborgComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<MothershipCoreComponent>(uid))
                continue;

            _explosion.QueueExplosion(
                uid,
                "Default",
                50f,
                5f,
                20f);

            EntityManager.DeleteEntity(uid);
        }
    }
}