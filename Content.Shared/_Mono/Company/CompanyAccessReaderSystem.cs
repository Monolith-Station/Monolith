using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared._Mono.Company;

/// <summary>
/// This system handles checking if a user belongs to the required company
/// before granting access to an entity.
/// </summary>
public sealed partial class CompanyAccessReaderSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CompanyAccessReaderComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
    }

    private void OnUIOpenAttempt(Entity<CompanyAccessReaderComponent> entity, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Get user's company
        if (!TryComp<CompanyComponent>(args.User, out var userCompany))
        {
            args.Cancel();
            if (entity.Comp.PopupMessage != null)
                _popup.PopupClient(Loc.GetString(entity.Comp.PopupMessage), entity, args.User);
            return;
        }

        // Check if user's company matches the required company
        if (userCompany.CompanyName != entity.Comp.RequiredCompany)
        {
            args.Cancel();
            if (entity.Comp.PopupMessage != null)
                _popup.PopupClient(Loc.GetString(entity.Comp.PopupMessage), entity, args.User);
        }
    }
}
