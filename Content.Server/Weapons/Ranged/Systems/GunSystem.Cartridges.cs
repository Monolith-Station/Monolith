using Content.Server.Chemistry.TileReactions;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void InitializeCartridge()
    {
        base.InitializeCartridge();
        SubscribeLocalEvent<CartridgeAmmoComponent, ExaminedEvent>(OnCartridgeExamine);
        SubscribeLocalEvent<CartridgeAmmoComponent, DamageExamineEvent>(OnCartridgeDamageExamine);
    }

    // Mono - modified for handling aP
    private void OnCartridgeDamageExamine(EntityUid uid, CartridgeAmmoComponent component, ref DamageExamineEvent args)
    {
        var damageT = GetProjectileDamage(component.Prototype, out var isHitscan); // mono

        if (damageT == null)
            return;

        var damageSpec = damageT.Value.Item1;
        var ap = damageT.Value.Item2;

        _damageExamine.AddDamageExamine(args.Message,
            Damageable.ApplyUniversalAllModifiers(damageSpec),
            Loc.GetString(
            isHitscan ? "damage-hitscan" : "damage-projectile"),
            ap); // mono
    }

    private (DamageSpecifier, float)? GetProjectileDamage(string proto, out bool isHitscan) // mono - isHitscan
    {
        isHitscan = false; // mono

        if (!ProtoManager.TryIndex<EntityPrototype>(proto, out var entityProto))
            return null;

        if (entityProto.Components
            .TryGetValue(Factory.GetComponentName<ProjectileComponent>(), out var projectile))
        {
            var p = (ProjectileComponent) projectile.Component;

            if (!p.Damage.Empty)
            {
                return (p.Damage * Damageable.UniversalProjectileDamageModifier, p.ArmorPenetration); // Mono
            }
        }
        // mono
        else if (entityProto.Components.TryGetValue(Factory.GetComponentName<HitscanBasicDamageComponent>(), out var hitscan))
        {
            var h = (HitscanBasicDamageComponent) hitscan.Component;

            if (h.Damage.Empty)
                return null;

            isHitscan = true;
            return (h.Damage, h.ArmorPenetration); // Mono
        }

        return null;
    }

    private void OnCartridgeExamine(EntityUid uid, CartridgeAmmoComponent component, ExaminedEvent args)
    {
        if (component.Spent)
        {
            args.PushMarkup(Loc.GetString("gun-cartridge-spent"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("gun-cartridge-unspent"));
        }
    }
}
