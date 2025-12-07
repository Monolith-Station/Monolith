using Content.Shared._Goobstation.ItemMiner;
using Content.Shared.Stacks;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Goobstation.ItemMiner;

public sealed class ItemMinerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemMinerComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(Entity<ItemMinerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextAt = _timing.CurTime + ent.Comp.Interval;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ItemMinerComponent>();

        while (query.MoveNext(out var uid, out var miner))
        {
            // check if other components want us to stop or spawn something else
            var checkEv = new ItemMinerCheckEvent(miner.Proto);
            RaiseLocalEvent(uid, ref checkEv);
            var proto = checkEv.Proto;

            var xform = Transform(uid);

            if (checkEv.Cancelled
                || miner.NeedApcPower && !_power.IsPowered(uid)
                || miner.NeedsAnchored && !xform.Anchored)
            {
                miner.NextAt += TimeSpan.FromSeconds(frameTime);
                QueueDel(miner.AudioUid);
                miner.AudioUid = null;
                // make us have depowered visuals if not working
                if (miner.NeedApcPower)
                    _power.SetPowerDisabled(uid, checkEv.Cancelled);

                continue;
            }

            // make us not have depowered visuals anymore
            if (miner.NeedApcPower)
                _power.SetPowerDisabled(uid, false);

            // play/restart audio if needed
            if (TerminatingOrDeleted(miner.AudioUid) && miner.MiningSound != null)
                miner.AudioUid = _audio.PlayPvs(miner.MiningSound, uid)?.Entity;

            if (miner.NextAt > _timing.CurTime)
                continue;
            miner.NextAt += miner.Interval;

            // mine
            var minedUid = Spawn(proto, xform.Coordinates);
            var ev = new ItemMinedEvent(minedUid);
            RaiseLocalEvent(uid, ref ev);

            // if it's a stack also merge it to whatever's on top unless we've been told not to
            if (!ev.NoStack)
                _stack.TryMergeToContacts(minedUid);

            if (miner.MinedSound != null)
                _audio.PlayPvs(miner.MinedSound, uid);
        }
    }
}
