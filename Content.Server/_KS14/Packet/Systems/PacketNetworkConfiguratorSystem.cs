using Content.Server._KS14.Packet.Components;
using Content.Server.Chat.Systems;
using Content.Shared._KS14.Packets;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._KS14.Packet.Systems;

/// <summary>
/// This handles all operations with packet network and executors through packet network configurator
/// </summary>
public sealed partial class PacketNetworkConfiguratorSystem : EntitySystem
{
    [Dependency] private PacketSystem _packetSystem = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedPopupSystem _sharedPopupSystem = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<PacketNetworkConfiguratorComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<PacketNetworkConfiguratorComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<PacketNetworkConfiguratorComponent, GetVerbsEvent<AlternativeVerb>>(OnAltInteract);
    }

    private void SwitchMode(Entity<PacketNetworkConfiguratorComponent> ent, EntityUid user)
    {
        ent.Comp.Mode = 1 - ent.Comp.Mode;

        _sharedPopupSystem.PopupEntity(ent.Comp.Mode == ConfiguratorMode.Probe
                ? Loc.GetString("packet-configurator-switch-probe")
                : Loc.GetString("packet-configurator-switch-save"),
            ent,
            user);
    }

    /// <summary>
    /// Creates network if:
    /// It has saved devices
    /// All saved devices have same frequency
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnUse(Entity<PacketNetworkConfiguratorComponent> ent, ref UseInHandEvent ev)
    {
       if (ent.Comp.Addresses.Count == 0)
        {
            SwitchFrequency(ent);
            return;
        }

        _chatSystem.TrySendInGameICMessage(ent,
            _packetSystem.CreateNetwork([..ent.Comp.Addresses],
            ent.Comp.ActiveFrequency),
            InGameICChatType.Whisper,
            false);

        ent.Comp.Addresses.Clear();
    }

    private void SwitchFrequency(Entity<PacketNetworkConfiguratorComponent> ent)
    {
        if (ent.Comp.Frequencies.Count == 0)
            return;

        if (ent.Comp.ActiveFrequency == -1)
        {
            ent.Comp.ActiveFrequency = ent.Comp.Frequencies[0];
        }
        else
        {
            var index = ent.Comp.Frequencies.IndexOf(ent.Comp.ActiveFrequency);

            ent.Comp.ActiveFrequency = (index + 1) < ent.Comp.Frequencies.Count
                ? ent.Comp.Frequencies[index + 1]
                : ent.Comp.Frequencies[0];
        }

        ent.Comp.FrequencyName = _packetSystem.GetFrequency(ent.Comp.ActiveFrequency).Id;

        Dirty(ent);
    }

    /// <summary>
    /// Upon interacting with paper - tries to read frequency from it.
    /// Interacting with packet network device will either probe it (show address and network) or save it.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnInteract(Entity<PacketNetworkConfiguratorComponent> ent, ref AfterInteractEvent ev)
    {
        if (ev.Target is not { } target)
            return;

        if (TryComp<FrequenciesPaperComponent>(target, out _))
        {
            ReadFrequencies(ent, ev.User);
            return;
        }

        if (TryComp<PaperComponent>(target, out var paper))
        {
            TryReadPaper(ent, paper, ev.User);
            return;
        }

        if (!TryComp<PacketNetworkComponent>(target, out var packetNetwork)
            || _packetSystem.GetFrequency(packetNetwork.Frequency) != ent.Comp.ActiveFrequency)
            {
                _sharedPopupSystem.PopupEntity(Loc.GetString("packet-configurator-no-signal"), ent, ev.User);
                return;
            }

        if (ent.Comp.Mode == ConfiguratorMode.Probe)
            OnProbeInteract(ent, (target, packetNetwork));
        else
            OnSaveInteract(ent, (target, packetNetwork), ev.User);
    }

    /// <summary>
    /// Switches mode on alt interact.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnAltInteract(Entity<PacketNetworkConfiguratorComponent> ent, ref GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract)
            return;

        var user = ev.User;

        var verb = new AlternativeVerb()
        {
            Text = Loc.GetString("packet-configurator-switch-mode"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/in.svg.192dpi.png")),
            Act = () =>
            {
                SwitchMode(ent, user);
                Dirty(ent);
            }
        };

        ev.Verbs.Add(verb);
    }

    private void OnProbeInteract(Entity<PacketNetworkConfiguratorComponent> ent, Entity<PacketNetworkComponent> target)
    {
        _chatSystem.TrySendInGameICMessage(ent,
            Loc.GetString("packet-configurator-probe-address", ("address", target.Comp.Address), ("network", target.Comp.AddressNetwork ?? "NULL")),
            InGameICChatType.Whisper,
            false);
    }

    private void OnSaveInteract(Entity<PacketNetworkConfiguratorComponent> ent, Entity<PacketNetworkComponent> target, EntityUid user)
    {
        _sharedPopupSystem.PopupEntity(Loc.GetString("packet-configurator-save"), user, user);
        ent.Comp.Addresses.Add(target.Comp.Address);
    }

    private void TryReadPaper(Entity<PacketNetworkConfiguratorComponent> ent, PaperComponent paper, EntityUid user)
    {
        if (!int.TryParse(paper.Content, out var freq))
            return;

        _sharedPopupSystem.PopupEntity(Loc.GetString("packet-configurator-frequency-save"), ent, user);
        if (!ent.Comp.Frequencies.Contains(freq))
            ent.Comp.Frequencies.Add(freq);

        Dirty(ent);
    }

    private void ReadFrequencies(Entity<PacketNetworkConfiguratorComponent> ent, EntityUid user)
    {
        foreach (var frequency in _packetSystem._frequencies)
        {
            if (!ent.Comp.Frequencies.Contains(frequency.Value))
                ent.Comp.Frequencies.Add(frequency.Value);
        }

        _sharedPopupSystem.PopupEntity(Loc.GetString("packet-configurator-frequency-save"), ent, user);

        Dirty(ent);
    }
}
