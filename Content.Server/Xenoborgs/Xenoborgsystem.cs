using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Pinpointer;
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
    private TimeSpan? _pinpointerWarningTime;
    private TimeSpan? _pinpointerWipeTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MothershipCoreComponent, DestructionEventArgs>(OnCoreDestroyed);
    }

    private void OnCoreDestroyed(EntityUid ent, MothershipCoreComponent comp, DestructionEventArgs args)
    {
        /// Announcement every time a core destruction
        _chat.DispatchGlobalAnnouncement(
            "A Mothership Core has been destroyed. Xenoborg systems destabilizing... Please discard any Mothership Pinpointers or Pieces before rapid disassembly in 15 seconds",
            colorOverride: Color.OrangeRed);

        /// Restart pinpointer collapse EVERY core destruction
        StartPinpointerCollapse();

        /// If this was the final core, trigger full Xenoborg collapse
        if (IsLastCore())
            TriggerCollapse();
    }

    private void StartPinpointerCollapse()
    {
        var now = _timing.CurTime;

        _pinpointerWarningTime = now + TimeSpan.FromSeconds(10);
        _pinpointerWipeTime = now + TimeSpan.FromSeconds(15);
    }

    private bool IsLastCore()
    {
        var query = AllEntityQuery<MothershipCoreComponent>();
        var count = 0;

        while (query.MoveNext(out _, out _))
        {
            count++;

            if (count > 1)
                return false;
        }

        return count == 1;
    }

    private void CleanupPinpointerPieces()
    {
        var pieceQuery = EntityQueryEnumerator<MothershipPinpointerPieceComponent>();

        while (pieceQuery.MoveNext(out var uid, out _))
        {
            /// Small explosion?
            _explosion.QueueExplosion(
                uid,
                "Default",
                2f,
                1f,
                2f);

            QueueDel(uid);
        }
    }

    private void TriggerCollapse()
    {
        var now = _timing.CurTime;

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

        /// FINAL XENOBORG COLLAPSE

        /// Warning buzzer
        if (_soundTime != null && now >= _soundTime)
        {
            _soundTime = null;

            _audio.PlayGlobal(
                "/Audio/Machines/warning_buzzer_xenoborg.ogg",
                Filter.Broadcast(),
                false);
        }

        /// Full Xenoborg wipe
        if (_wipeTime != null && now >= _wipeTime)
        {
            _wipeTime = null;

            ExplodeAllXenoborgs();

            _chat.DispatchGlobalAnnouncement(
                "All Xenoborg and Motherships Cores have been destroyed, No further active Xenoborg presence detected in the sector.",
                colorOverride: Color.DarkRed);

            _chat.DispatchGlobalAnnouncement(
                "Have a pleasant day.",
                colorOverride: Color.LimeGreen);
        }

        /// PINPOINTER COLLAPSE

        /// Warning buzzer
        if (_pinpointerWarningTime != null && now >= _pinpointerWarningTime)
        {
            _pinpointerWarningTime = null;

            _audio.PlayGlobal(
                "/Audio/Machines/warning_buzzer_xenoborg.ogg",
                Filter.Broadcast(),
                false);
        }

        /// Destroy all Xenoborg pinpointer pieces
        if (_pinpointerWipeTime != null && now >= _pinpointerWipeTime)
        {
            _pinpointerWipeTime = null;

            CleanupPinpointerPieces();
        }
    }

    private void ExplodeAllXenoborgs()
    {
        var query = AllEntityQuery<XenoborgComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            /// Don't explode mothership cores themselves failsafe incase new spawned
            if (HasComp<MothershipCoreComponent>(uid))
                continue;

            _explosion.QueueExplosion(
                uid,
                "Default",
                50f,
                5f,
                20f);

            QueueDel(uid);
        }
    }
}