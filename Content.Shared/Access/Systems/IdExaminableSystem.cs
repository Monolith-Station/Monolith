using Content.Shared.Access.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Verbs;
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Access.Systems;

public sealed partial class IdExaminableSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, IdExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);
        var info = GetMessage(uid);

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var markup = FormattedMessage.FromMarkupOrThrow(info);

                _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
            },
            Text = Loc.GetString("id-examinable-component-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("id-examinable-component-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/character.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    public string GetMessage(EntityUid uid)
    {
        return GetInfo(uid) ?? Loc.GetString("id-examinable-component-verb-no-id");
    }

    public string? GetInfo(EntityUid uid)
    {
        if (_inventorySystem.TryGetSlotEntity(uid, "id", out var idUid))
        {
            // PDA
            if (EntityManager.TryGetComponent(idUid, out PdaComponent? pda) &&
                TryComp<IdCardComponent>(pda.ContainedId, out var id))
            {
                return GetNameAndJob(id);
            }
            // ID Card
            if (EntityManager.TryGetComponent(idUid, out id))
            {
                return GetNameAndJob(id);
            }
        }
        return null;
    }

    private string GetNameAndJob(IdCardComponent id)
    {
        var jobSuffix = string.IsNullOrWhiteSpace(id.LocalizedJobTitle) ? string.Empty : $" ({id.LocalizedJobTitle})";

        // Get company information if available
        var companySuffix = string.Empty;
        if (!string.IsNullOrWhiteSpace(id.CompanyName) && id.CompanyName != "None")
        {
            if (_prototypeManager.TryIndex<CompanyPrototype>(id.CompanyName, out var companyProto))
            {
                companySuffix = $" - [color={companyProto.Color.ToHex()}]{companyProto.Name}[/color]";
            }
            else
            {
                companySuffix = $" - {id.CompanyName}";
            }
        }

        var val = string.IsNullOrWhiteSpace(id.FullName)
            ? Loc.GetString(id.NameLocId,
                ("jobSuffix", jobSuffix),
                ("companySuffix", companySuffix))
            : Loc.GetString(id.FullNameLocId,
                ("fullName", id.FullName),
                ("jobSuffix", jobSuffix),
                ("companySuffix", companySuffix));

        return val;
    }
}
