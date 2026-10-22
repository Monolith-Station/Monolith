using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._EinsteinEngines.Radio;

public sealed class IntrinsicRadioKeySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EncryptionChannelsChangedEvent>(OnTransmitterChannelsChanged);
        SubscribeLocalEvent<ActiveRadioComponent, EncryptionChannelsChangedEvent>(OnReceiverChannelsChanged);
    }

    private void OnTransmitterChannelsChanged(EntityUid uid, IntrinsicRadioTransmitterComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateChannels(uid, args.Component, ref component.Channels);
    }

    private void OnReceiverChannelsChanged(EntityUid uid, ActiveRadioComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateChannels(uid, args.Component, ref component.Channels);
    }

    private void UpdateChannels(EntityUid _, EncryptionKeyHolderComponent keyHolderComp, ref HashSet<ProtoId<RadioChannelPrototype>> channels)
    {
        channels.Clear();
        foreach (var channel in keyHolderComp.Channels)
            channels.Add(channel);
    }
}
