using Content.Client.Eui;
using Content.Shared._NF.CryoSleep;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._NF.CryoSleep;

[UsedImplicitly]
public sealed class CryoSleepEui : BaseEui
{
    private readonly AcceptCryoWindow _window;

    public CryoSleepEui()
    {
        _window = new AcceptCryoWindow();

        _window.DenyButton.OnPressed += _ =>
        {
            SendMessage(new AcceptCryoChoiceMessage(AcceptCryoUiButton.Deny));
            _window.Close();
        };

        _window.AcceptButton.OnPressed += _ =>
        {
            SendMessage(new AcceptCryoChoiceMessage(AcceptCryoUiButton.Accept));
            _window.Close();
        };
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is CryoSleepEuiState cryo)
            _window.RespawnWarning.Visible = cryo.SavesRespawn;
    }

    public override void Closed()
    {
        _window.Close();
    }

}
