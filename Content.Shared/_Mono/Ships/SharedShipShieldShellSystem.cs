using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.Ships;

public sealed partial class SharedShipShieldShellSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private LockSystem _locks = default!;
    private readonly HashSet<EntityUid> _inactiveColliders = new();

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
        SubscribeLocalEvent<ShipShieldProjectorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ShipShieldColliderComponent, PreventCollideEvent>(OnPreventContact);
        SubscribeLocalEvent<ShipShieldColliderComponent, ComponentShutdown>(OnColliderShutdown);
        Subs.BuiEvents<ShipShieldProjectorComponent>(ShipShieldProjectorUiKey.Key, subs =>
            subs.Event<ShipShieldProjectorToggleMessage>(OnToggle));
        InitializeContainment();
    }

    private void OnToggle(Entity<ShipShieldProjectorComponent> ent, ref ShipShieldProjectorToggleMessage args)
    {
        if (_locks.IsLocked(ent.Owner))
            return;

        var projector = ent.Comp;
        var elapsed = _timing.CurTime - projector.StartedAt;
        switch (projector.State)
        {
            case ShipShieldShellState.Active:
            case ShipShieldShellState.Starting when projector.StartupSpoolAt != null:
                BeginShutdown(ent, ShipShieldShellState.PoweringDown);
                break;
            case ShipShieldShellState.Off:
                SetState(ent, _timing.CurTime >= projector.RespoolUntil
                    ? ShipShieldShellState.Starting : ShipShieldShellState.Standby);
                break;
            case ShipShieldShellState.Starting or ShipShieldShellState.Standby:
                SetState(ent, ShipShieldShellState.Off);
                break;
            case ShipShieldShellState.Brownout:
                SetState(ent, ShipShieldShellState.PoweringDown, elapsed);
                break;
            case ShipShieldShellState.PoweringDown:
                SetState(ent, ShipShieldShellState.Brownout, elapsed);
                break;
            case ShipShieldShellState.Shattering:
                SetState(ent, ShipShieldShellState.ShatteringOff, elapsed);
                break;
            case ShipShieldShellState.ShatteringOff:
                SetState(ent, ShipShieldShellState.Shattering, elapsed);
                break;
        }
    }

    public void BeginShutdown(Entity<ShipShieldProjectorComponent> ent, ShipShieldShellState state)
    {
        var phase = ent.Comp.AnimationPhase(_timing.CurTime);
        SetState(ent, state, ent.Comp.AnimationDuration(state) * (1f - phase));
    }

    private void OnInteractUsing(Entity<ShipShieldProjectorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_locks.IsLocked(ent.Owner) || !_idCards.TryGetIdCard(args.Used, out _))
            return;

        args.Handled = true;
        _locks.TryUnlock(ent.Owner, args.User);
    }

    private void OnPreventContact(Entity<ShipShieldColliderComponent> collider, ref PreventCollideEvent args)
    {
        if (!IsShellFixture(args.OurFixture))
            return;

        if (!TryComp(collider.Comp.Shield, out ShipShieldProjectorComponent? shield) || !IsSolid(shield))
        {
            args.Cancelled = true;
            return;
        }

        // Shield pairs use the ellipsoid sweep, not the polygonal proxies' contact skin.
        if (HasComp<ShipShieldColliderComponent>(args.OtherEntity) && IsShellFixture(args.OtherFixture))
        {
            args.Cancelled = true;
            return;
        }

        // Character motion is resolved by the shared sweep, not the server's collider body.
        if (args.OtherBody.BodyType == BodyType.KinematicController || HasComp<MapGridComponent>(args.OtherEntity) ||
            !BlocksBody(shield.Shield, args.OtherEntity) ||
            collider.Comp.Grid is { } grid && Transform(args.OtherEntity) is { Anchored: true } other && other.GridUid == grid)
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShipShieldProjectorComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if ((shield.IsShattering || shield.State is ShipShieldShellState.PoweringDown or ShipShieldShellState.Brownout) &&
                _timing.CurTime - shield.StartedAt >= shield.AnimationDuration(shield.State))
                SetState((uid, shield), shield.RequestsPower ? ShipShieldShellState.Standby : ShipShieldShellState.Off);

            if (shield.State == ShipShieldShellState.Starting)
            {
                if (shield.HasOperatingPower)
                {
                    var elapsed = shield.StartupSpoolAt is { } start
                        ? _timing.CurTime - start
                        : TimeSpan.Zero;
                    SetState((uid, shield), ShipShieldShellState.Active, elapsed);
                }
                else if (!shield.StartupPowerAvailable && shield.StartupSpoolAt.HasValue)
                    BeginShutdown((uid, shield), ShipShieldShellState.Brownout);
            }
            else if (shield.HasOperatingPower)
            {
                if (shield.State == ShipShieldShellState.Standby && _timing.CurTime >= shield.RespoolUntil)
                    SetState((uid, shield), ShipShieldShellState.Active);
            }
            else if (shield.State == ShipShieldShellState.Active)
                BeginShutdown((uid, shield), ShipShieldShellState.Brownout);

            if (shield.PoweredRechargeRate > 0f && shield.CanRecharge(_timing.CurTime))
            {
                shield.Damage = MathF.Max(0f, shield.Damage - shield.PoweredRechargeRate * frameTime);
                Dirty(uid, shield);
            }

            foreach (var (source, contact) in shield.BodyContacts)
            {
                if (_timing.CurTime < contact.PressureUntil || _timing.CurTime < contact.PassiveUntil)
                    continue;

                shield.BodyContacts.Remove(source);
                Dirty(uid, shield);
            }
        }

        if (!_net.IsClient)
            return;

        // Discard cached contacts when prediction changes solidity, without editing the replicated fixtures.
        var colliders = EntityQueryEnumerator<ShipShieldColliderComponent, PhysicsComponent>();
        while (colliders.MoveNext(out var uid, out var collider, out var body))
        {
            var solid = TryComp(collider.Shield, out ShipShieldProjectorComponent? shield) && IsSolid(shield);
            var changed = solid ? _inactiveColliders.Remove(uid) : _inactiveColliders.Add(uid);
            if (changed && body.CanCollide)
                _physics.RegenerateContacts((uid, body));
        }
    }

    private void OnColliderShutdown(Entity<ShipShieldColliderComponent> ent, ref ComponentShutdown args) =>
        _inactiveColliders.Remove(ent.Owner);

    public void SetState(Entity<ShipShieldProjectorComponent> shield, ShipShieldShellState state, TimeSpan elapsed = default)
    {
        shield.Comp.State = state;
        shield.Comp.StartedAt = _timing.CurTime - elapsed;
        // Only preview on Raise. If power has to ramp, the first delivered base load starts the animation.
        shield.Comp.StartupSpoolAt = state == ShipShieldShellState.Starting && shield.Comp.StartupPowerAvailable
            ? _timing.CurTime : null;
        Dirty(shield);
    }
}
