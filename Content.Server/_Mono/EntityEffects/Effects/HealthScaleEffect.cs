using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Text.Json.Serialization;

namespace Content.Server._Mono.EntityEffects.Effects
{

    public sealed partial class HealthScaleEffect : EntityEffect
    {
        /// <summary>
        /// This is just HealthChange.cs butchered horrifically
        /// It mostly follows the same conventions, including the yaml and seems compatible with conditionals
        /// !!!!DO NOT USE THIS WITH DAMAGE GROUPS!!!! damage group behaviors differ between forks and there is a
        /// nonzero chance someone might suddenly decide to port a different behavior over.
        /// The ENTIRETY of the health system is a rat's nest and the less technical debt I risk for this effect the better
        /// TRUST ME: the yaml for specifying this properly is ugly but it's better than having to fix this
        /// </summary>
        [DataField(required: true)]
        public DamageSpecifier HealthScale;

        [DataField]
        public bool IgnoreResistances = true;

        protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        {
            int CalcMultDirection(float scale) => scale < 1 ? -1 : scale > 1 ? 1 : 0;

            var damages = new List<string>();
            var damageSpec = new DamageSpecifier(HealthScale);

            foreach (var group in prototype.EnumeratePrototypes<DamageGroupPrototype>())
            {
                var relevantTypes = damageSpec.DamageDict
                    .Where(x => x.Value != FixedPoint2.Zero && group.DamageTypes.Contains(x.Key)).ToList();

                //Skip incomplete types involved in group
                if (relevantTypes.Count != group.DamageTypes.Count)
                    continue;

                //Skip unequal types
                var firstValue = relevantTypes.First().Value;
                if (relevantTypes.Any(type => FixedPoint2.Abs(type.Value - firstValue) >= 0.02))
                    continue;

                //Skip 1x multiplier
                var scale = firstValue.Float();
                if (Math.Abs(scale - 1.0f) < 0.001f)
                    continue;

                damages.Add(
                    Loc.GetString("health-scale-display",
                        ("kind", group.LocalizedName),
                        ("amount", scale.ToString("0.###")),
                        ("deltasign", CalcMultDirection(scale)))
                );

                foreach (var type in group.DamageTypes)
                    damageSpec.DamageDict.Remove(type);
            }

            foreach (var (kind, amount) in damageSpec.DamageDict)
            {
                if (Math.Abs(amount.Float() - 1.0f) < 0.001f)
                    continue;

                damages.Add(
                    Loc.GetString("health-scale-display",
                        ("kind", prototype.Index<DamageTypePrototype>(kind).LocalizedName),
                        ("amount", amount.Float().ToString("0.###")),
                        ("deltasign", CalcMultDirection(amount.Float()))
                    )
                );
            }

            return Loc.GetString("reagent-effect-guidebook-health-scale",
                ("chance", Probability),
                ("changes", ContentLocalizationManager.FormatList(damages)));
        }

        public override void Effect(EntityEffectBaseArgs args)
        {
            var deltaSpec = new DamageSpecifier();

            var reagentArgs = args as EntityEffectReagentArgs;
            var damageSystem = args.EntityManager.System<DamageableSystem>();
            var damageable = args.EntityManager.GetComponentOrNull<DamageableComponent>(args.TargetEntity);

            if (damageable == null)
                return;

            var currentDamage = damageable.Damage;

            foreach (var (type, currentAmount) in currentDamage.DamageDict)
            {
                if (HealthScale.DamageDict.TryGetValue(type, out var scaleCoeff))
                {
                    var applyScale = reagentArgs != null ? reagentArgs.Scale : 1.0f;
                    var adjusted = currentAmount * (MathF.Pow(scaleCoeff.Float(), applyScale.Float()) - 1.0f);

                    if (adjusted != FixedPoint2.Zero)
                        deltaSpec.DamageDict[type] = adjusted;

                }
            }

            damageSystem.TryChangeDamage(
                args.TargetEntity,
                deltaSpec,
                IgnoreResistances,
                interruptsDoAfters: false,
                targetPart: TargetBodyPart.All,
                partMultiplier: 0.5f,
                canSever: false);
        }
    }
}
