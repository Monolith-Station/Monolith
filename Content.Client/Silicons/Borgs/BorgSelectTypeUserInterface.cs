using Content.Shared.Silicons.Borgs.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// User interface used by borgs to select their type.
/// </summary>
/// <seealso cref="BorgSelectTypeMenu"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
/// <seealso cref="BorgSwitchableTypeUiKey"/>
[UsedImplicitly]
public sealed class BorgSelectTypeUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BorgSelectTypeMenu? _menu;

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        //Mono: Selectable borg whitelist
        EntMan.TryGetComponent<BorgSwitchableTypeComponent>(Owner, out var comp);
        var whitelist = comp?.TypeWhitelist ?? [];

        _menu = this.CreateWindow<BorgSelectTypeMenu>();
        _menu.Populate(whitelist); //Mono
        _menu.ConfirmedBorgType += (prototype, subtype) => SendPredictedMessage(new BorgSelectTypeMessage(prototype, subtype));
    }
}
