using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Goobstation.MouseSwarm;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Analyzers;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Goobstation.MouseSwarm;

public sealed partial class MouseSwarmSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string TsfFaction = "TSFMC";
    private const string PirateFaction = "PirateNF";

    private readonly List<(EntProtoId Proto, EntityUid Host, MapCoordinates Coords)> _pendingDrops = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MouseSwarmComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MouseSwarmComponent, MouseSwarmActionEvent>(OnSwarm);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingDrops.Count == 0)
            return;

        foreach (var (proto, host, coords) in _pendingDrops)
        {
            var dropped = Exists(host)
                ? SpawnNextToOrDrop(proto, host)
                : Spawn(proto, coords);

            _mobState.ChangeMobState(dropped, MobState.Dead);
        }

        _pendingDrops.Clear();
    }

    private void OnDamageChanged(Entity<MouseSwarmComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased
            || ent.Comp.Members.Count == 0
            || !_mobState.IsAlive(ent)
            || !TryComp<MobThresholdsComponent>(ent, out var thresholds))
            return;

        if (!_thresholds.TryGetThresholdForState(ent, MobState.Critical, out var limit, thresholds)
            && !_thresholds.TryGetThresholdForState(ent, MobState.Dead, out limit, thresholds))
            return;

        if (args.Damageable.TotalDamage < limit.Value)
            return;

        var member = ent.Comp.Members[^1];
        ent.Comp.Members.RemoveAt(ent.Comp.Members.Count - 1);
        Dirty(ent);

        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            melee.Damage -= ent.Comp.DamagePerMember;
            Dirty(ent, melee);
        }

        if (member.Prototype is { } proto)
            _pendingDrops.Add((proto, ent.Owner, _transform.GetMapCoordinates(ent)));

        _damageable.SetAllDamage(ent.Owner, args.Damageable, 0);
        _popup.PopupEntity(Loc.GetString("mouse-swarm-lost"), ent, ent);
    }

    private void OnSwarm(Entity<MouseSwarmComponent> ent, ref MouseSwarmActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        // Reject invalid targets
        if (target == ent.Owner
            || !TryComp<MouseSwarmComponent>(target, out var targetSwarm)
            || HasComp<ActorComponent>(target)
            || _mind.TryGetMind(target, out _, out _)
            || !_mobState.IsAlive(target)
            || _container.IsEntityInContainer(target))
        {
            _popup.PopupEntity(Loc.GetString("mouse-swarm-invalid"), ent, ent);
            return;
        }

        // Prevent absorbing another existing swarm.
        if (targetSwarm.Members.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("mouse-swarm-invalid-swarm"), ent, ent);
            return;
        }

        // Check the faction of the target against the entire swarm.
        var targetFactions = GetFactions(target);

        if (HasFactionConflict(ent, targetFactions))
        {
            _popup.PopupEntity(Loc.GetString("mouse-swarm-invalid-faction"), ent, ent);
            return;
        }

        if (ent.Comp.Members.Count + 1 > ent.Comp.MaxMembers)
        {
            _popup.PopupEntity(Loc.GetString("mouse-swarm-full"), ent, ent);
            return;
        }

        args.Handled = true;

        // Preserve appearance, optional animated tail and faction membership.
        ent.Comp.Members.Add(RandomMember(
            ent.Comp,
            targetSwarm.Sprite,
            targetSwarm.State,
            MetaData(target).EntityPrototype?.ID,
            targetSwarm.ExtraState,
            targetFactions));

        Dirty(ent);

        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            melee.Damage += ent.Comp.DamagePerMember;
            Dirty(ent, melee);
        }

        _popup.PopupEntity(
            Loc.GetString("mouse-swarm-absorbed", ("target", target)),
            ent,
            ent);

        QueueDel(target);
    }

    private HashSet<ProtoId<NpcFactionPrototype>> GetFactions(EntityUid uid)
    {
        if (!TryComp<NpcFactionMemberComponent>(uid, out var faction))
            return new();

        return new HashSet<ProtoId<NpcFactionPrototype>>(faction.Factions);
    }

    private bool HasFactionConflict(
        Entity<MouseSwarmComponent> swarm,
        HashSet<ProtoId<NpcFactionPrototype>> targetFactions)
    {
        var hasTsf = false;
        var hasPirate = false;

        // Include the living swarm owner.
        var ownerFactions = GetFactions(swarm.Owner);

        hasTsf |= ownerFactions.Contains(TsfFaction);
        hasPirate |= ownerFactions.Contains(PirateFaction);

        // Include every absorbed member.
        foreach (var member in swarm.Comp.Members)
        {
            hasTsf |= member.Factions != null
                && member.Factions.Contains(TsfFaction);

            hasPirate |= member.Factions != null
                && member.Factions.Contains(PirateFaction);
        }

        // Opposing factions cannot be mixed.
        return (hasTsf && targetFactions.Contains(PirateFaction))
            || (hasPirate && targetFactions.Contains(TsfFaction));
    }

    private MouseSwarmMember RandomMember(
        MouseSwarmComponent comp,
        ResPath sprite,
        string state,
        EntProtoId? prototype,
        string? extraState = null,
        HashSet<ProtoId<NpcFactionPrototype>>? factions = null)
    {
        var offset = new Vector2(
            _random.NextFloat(-comp.MaxOffset, comp.MaxOffset),
            _random.NextFloat(-comp.MaxOffset, comp.MaxOffset));

        return new MouseSwarmMember(
            sprite,
            state,
            offset,
            prototype,
            extraState,
            factions);
    }
}