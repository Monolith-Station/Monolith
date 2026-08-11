using Content.Shared._Forge.Silicons.StationAi;
using Robust.Client.UserInterface;

namespace Content.Client._Forge.Silicons.StationAi;

public sealed class StationAiCustomizationBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private StationAiCustomizationWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StationAiCustomizationWindow>();
        _window.OnApply += (name, screen, color) => SendMessage(new StationAiCustomizationApplyMessage(name, screen, color));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is StationAiCustomizationState customization)
            _window?.UpdateState(customization);
    }
}
