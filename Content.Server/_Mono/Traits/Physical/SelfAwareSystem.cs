using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Traits.Physical;

/// <summary>
/// Handles the self-examine verb for SelfAwareComponent.
/// </summary>
public sealed class SelfAwareSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SelfAwareComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, SelfAwareComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (uid != args.User || uid != args.Target)
            return;

        if (!TryComp<DamageableComponent>(uid, out var damage))
            return;

        var alive = _mobState.IsAlive(uid);

        var verb = new ExamineVerb()
        {
            Text = Loc.GetString("self-aware-verb-text"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/zap.svg.192dpi.png")),
            Disabled = !alive,
            Act = () =>
            {
                var msg = BuildDamageMessage(uid, damage);
                _examine.SendExamineTooltip(uid, uid, msg, false, false);
            }
        };

        args.Verbs.Add(verb);
    }

    private FormattedMessage BuildDamageMessage(EntityUid uid, DamageableComponent damage)
    {
        var msg = new FormattedMessage();

        var total = damage.TotalDamage;
        msg.AddMarkupOrThrow(Loc.GetString("self-aware-total", ("amount", total)));

        var anyGroup = false;
        foreach (var (groupId, amount) in damage.DamagePerGroup)
        {
            if (amount == FixedPoint2.Zero)
                continue;

            anyGroup = true;
            var groupProto = _prototypes.Index<Content.Shared.Damage.Prototypes.DamageGroupPrototype>(groupId);
            var groupName = groupProto.LocalizedName;
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("self-aware-group-line", ("group", groupName), ("amount", amount)));

            foreach (var typeId in groupProto.DamageTypes)
            {
                if (!damage.Damage.DamageDict.TryGetValue(typeId, out var typeAmt) || typeAmt == FixedPoint2.Zero)
                    continue;

                var typeName = _prototypes.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(typeId).LocalizedName;
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("self-aware-type-subline", ("type", typeName), ("amount", typeAmt)));
            }
        }

        if (!anyGroup)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("self-aware-no-damage"));
        }

        return msg;
    }
}


