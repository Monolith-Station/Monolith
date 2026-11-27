using Content.Client.Message;
using Content.Shared._Mono.Persistence;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Mono.Planets;

public sealed partial class CryopodSpawnClientSystem : EntitySystem
{
    public event Action<CryopodSpawnOption[]>? OptionsReceived;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CryopodSpawnOptions>(message => OptionsReceived?.Invoke(message.Options));
    }

    public void RequestOptions() => RaiseNetworkEvent(new CryopodSpawnOptionsRequest());
    public void RequestSpawn(Guid id) => RaiseNetworkEvent(new CryopodSpawnRequest(id));

    public void AddReservationNotice(BoxContainer container, CryopodSpawnOption option)
    {
        var notice = new RichTextLabel();
        notice.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("cryopod-respawn-record", ("planet", option.Planet))));
        container.AddChild(notice);
        container.AddChild(new RichTextLabel().SetMarkup(
            Loc.GetString(option.KeepsGear ? "cryopod-respawn-gear-kept" : "cryopod-respawn-gear-reset")));
    }
}
