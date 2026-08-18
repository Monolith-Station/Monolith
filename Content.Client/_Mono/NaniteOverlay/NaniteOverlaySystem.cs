

using Content.Client.Hands;
using Content.Client.NetworkConfigurator.Systems;
using Content.Shared._Mono.NaniteOverlay;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Mono.NaniteOverlay;

public sealed partial class NaniteOverlaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IMapManager _mapMan = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private float _updateTimer = 0.0f;
    private bool _active = false;

    private Dictionary<EntityUid, Color> _modifiedEntities = new();
    private List<NetEntity> _entities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NaniteOverlayMessage>(OnNaniteOverlayMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var player = _player.LocalEntity;
        if (player == null || !TryComp<NaniteOverlayEyeComponent>(player, out var eye))
        {
            ClearOverlays();
            _active = false;
            _updateTimer = 0.0f;
            return;
        }

        _active = true;

        if (_updateTimer > 0.0f)
        {
            _updateTimer -= frameTime;
            return;
        }

        _updateTimer = 1.0f;

        // 0. Remove entities that went out of range
        List<EntityUid> toRemove = new();
        foreach (var entity in _modifiedEntities)
        {
            if ((_transform.GetWorldPosition(Transform(entity.Key)) - _transform.GetWorldPosition(Transform(player.Value))).LengthSquared() > (eye.Range * eye.Range + 1))
                toRemove.Add(entity.Key);
        }

        foreach (var entry in toRemove)
        {
            _sprite.SetColor(entry, _modifiedEntities[entry]);
            _modifiedEntities.Remove(entry);
        }

        // 1. Find entities that have Repairable, Damageable (SpriteComponent too but everything has that)
        var candidates = _lookup.GetEntitiesInRange<RepairableComponent>(Transform(player.Value).Coordinates, eye.Range, LookupFlags.Uncontained);
        _entities.Clear();

        foreach (var entity in candidates)
        {
            if (!TerminatingOrDeleted(entity) && TryComp<DamageableComponent>(entity, out var damageable) && damageable.TotalDamage > 0)
            {
                _entities.Add(GetNetEntity(entity));
            }
        }

        // 2. Ask the server for their damage threshold
        RaiseNetworkEvent(new NaniteOverlayMessage(_entities.ToArray()));

        // 3. Once server replies, draw them by changing the color of the entity based on the threshold
        // 4. If we lose the overlay eye (tool put away, etc.) change the color of the entities back to what it was previously
    }

    public void OnNaniteOverlayMessage(NaniteOverlayMessage message, EntitySessionEventArgs eventArgs)
    {
        if (message.Responses == null || !_active)
            return;

        for (int i = 0; i < message.Responses.Length; i++)
        {
            var response = message.Responses[i];

            if (response > 0) // if entity is damaged
                ShowOverlay(GetEntity(message.Targets[i]), response);
            else if (response == 0) // entity was repaired and is at full health
            {
                var uid = GetEntity(message.Targets[i]);
                if (!uid.Valid || TerminatingOrDeleted(uid) && _modifiedEntities.ContainsKey(uid))
                {
                    _sprite.SetColor(uid, _modifiedEntities[uid]);
                    _modifiedEntities.Remove(uid);
                }
            }
        }
    }

    private void ShowOverlay(EntityUid uid, FixedPoint2 threshold)
    {
        if (!uid.Valid || TerminatingOrDeleted(uid))
            return;

        if(!_modifiedEntities.ContainsKey(uid))
        {
            SpriteComponent sc = Comp<SpriteComponent>(uid);
            _modifiedEntities.Add(uid, sc.Color);
        }

        DamageableComponent dc = Comp<DamageableComponent>(uid);
        float health = 1.0f - (dc.TotalDamage / threshold).Float();
        _sprite.SetColor(uid, GetColor(health));
    }

    private Color GetColor(float health)
    {
        health = Math.Clamp(health, 0.0f, 1.0f);

        float hue = 0.0f;
        float sat = 1.0f;
        float val = 1.0f;

        if (health >= 0.85f) // Green to Yellow (Fast 15% window)
        {
            float t = (health - 0.85f) / 0.15f;
            hue = 0.1666f + (t * (0.2200f - 0.1666f));
            sat = 0.8000f + (t * (0.9000f - 0.8000f));
            val = 1.0000f - (t * (1.0000f - 0.9500f));
        }
        else if (health >= 0.50f) // Yellow to Gold-Orange (35% window)
        {
            float t = (health - 0.50f) / 0.35f;
            hue = 0.1000f + (t * (0.1666f - 0.1000f)); // Ends at 0.1000 hue (a rich neon gold-orange)
            sat = 1.0000f - (t * (1.0000f - 0.8000f));
            val = 1.0f;
        }
        else if (health >= 0.25f) // Gold-Orange to Red (25% window)
        {
            float t = (health - 0.25f) / 0.25f;
            hue = 0.0000f + (t * 0.1000f);
            sat = 1.0f;
            val = 1.0f;
        }
        else // Red to Near Black-Red (Only the final 25%)
        {
            float t = health / 0.25f;
            hue = 0.0f;
            sat = 1.0f;
            val = 0.08f + (t * 0.92f); // Floor dropped to 0.08f so your final square looks nearly dead
        }

        return Color.FromHsv(new Vector4(hue, sat, val, 1.0f));
    }

    private void ClearOverlays()
    {
        foreach (var ent in _modifiedEntities)
        {
            _sprite.SetColor(ent.Key, ent.Value);
        }

        _modifiedEntities.Clear();
    }
}
