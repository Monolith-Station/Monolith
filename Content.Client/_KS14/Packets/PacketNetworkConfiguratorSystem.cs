using Content.Client.Items;
using Content.Client.Message;
using Content.Shared._KS14.Packets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._KS14.Packets;

public sealed partial class PacketNetworkConfiguratorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<PacketNetworkConfiguratorComponent>(OnItemStatus);
    }

    private Control OnItemStatus(Entity<PacketNetworkConfiguratorComponent> entity)
    {
        return new PacketNetworkConfiguratorStatusControl(entity);
    }

    private sealed class PacketNetworkConfiguratorStatusControl : Control
    {
        private readonly RichTextLabel _label = new()
        {
            StyleClasses = { "ItemStatus" }
        };

        private readonly EntityUid _uid;
        private PacketNetworkConfiguratorComponent _configurator;

        public PacketNetworkConfiguratorStatusControl(Entity<PacketNetworkConfiguratorComponent> entity)
        {
            _configurator = entity.Comp;
            _uid = entity.Owner;
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            var modeKey = $"packet-configurator-mode-{_configurator.Mode.ToString().ToLowerInvariant()}";
            var modeName = Robust.Shared.Localization.Loc.GetString(modeKey);

            var freqName = _configurator.FrequencyName.Replace("Frequency", "") + " | " + _configurator.ActiveFrequency;

            _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("packet-configurator-status-label",
                ("mode", $"[color=cyan]{modeName}[/color]"),
                ("frequency", $"[color=orange]{freqName}[/color]")));
        }
    }
}
