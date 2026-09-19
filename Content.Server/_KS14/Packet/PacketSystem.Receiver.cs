using System.Linq;
using System.Text;
using Content.Server._KS14.Packet.Components;
using Content.Server._KS14.Packet.Prototypes;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;

namespace Content.Server._KS14.Packet;

/// <summary>
/// Handles <see cref="PacketNetworkComponent"/> logic - Getting receivers, randomizing frequencies, etc.
/// </summary>
public sealed partial class PacketSystem
{
    /// <summary>
    /// Stores all packet receivers.
    /// String value is address.
    /// </summary>
    private Dictionary<string, Entity<PacketNetworkComponent>> _packetEntities = new();
    private Dictionary<ProtoId<PacketFrequencyPrototype>, int> _frequencies = new();

    /// <summary>
    /// Generates random address for entity.
    /// </summary>
    /// <param name="entity"></param>
    private void SetupAddress(Entity<PacketNetworkComponent> entity)
    {
        entity.Comp.Address =  GenerateAddress();
        _packetEntities.Add(entity.Comp.Address, entity);
    }

    /// <summary>
    /// Generates paper with all frequencies written on it.
    /// </summary>
    /// <param name="paper"></param>
    private void GenerateFrequenciesPaper(Entity<FrequenciesPaperComponent> paper)
    {
        if (!TryComp<PaperComponent>(paper, out var paperComponent))
            return;

        StringBuilder builder = new();

        builder.Append(Loc.GetString("packet-frequencies-paper-label") + "\n\n");

        foreach (var freq in _frequencies)
        {
            builder.Append(freq.Key.Id + ": "  + freq.Value + "\n");
        }

        paperComponent.Content = builder.ToString();
    }

    /// <summary>
    /// Reloads frequencies this entity can listen to the standard.
    /// </summary>
    /// <param name="entity"></param>
    private void ReloadFrequencies(Entity<PacketNetworkComponent> entity)
    {
        entity.Comp.ListeningFrequencies.Clear();
        var freq = _prototypeManager.Index(entity.Comp.Frequency);

        foreach (var listFreq in freq.ListeningFrequencies)
        {
            entity.Comp.ListeningFrequencies.Add(listFreq);
        }
    }

    /// <summary>
    /// Generates random address while also accounting for existing ones
    /// </summary>
    /// <returns></returns>
    private string GenerateAddress()
    {
        var value = _random.Next((int) Math.Pow(16, 6));
        var address = "0x" + value.ToString("X");

        if (_packetEntities.ContainsKey(address))
        {
            return GenerateAddress(); // There is 1/10³⁵⁰⁰⁰⁰⁰ chance that this causes stack overflow btw.
        }
        return address;
    }

    /// <summary>
    /// Randomizes frequencies based on frequency prototype.
    /// Frequencies are randomized each round.
    /// </summary>
    private void RandomizeFrequencies()
    {
        var protoEnum = _prototypeManager.EnumeratePrototypes<PacketFrequencyPrototype>();

        foreach (var freqProto in protoEnum)
        {
            freqProto.Frequency = _random.Next(freqProto.MinimalFrequency, freqProto.MaximumFrequency);
            _frequencies.Add(freqProto, freqProto.Frequency);
        }
    }

    public bool TryGetReceiver(string address, out Entity<PacketNetworkComponent> receiver)
    {
        return _packetEntities.TryGetValue(address, out receiver) && Exists(receiver);
    }

    public bool TryGetReceiver(int freq, string address, out Entity<PacketNetworkComponent> receiver)
    {

        return _packetEntities.TryGetValue(address, out receiver) && Exists(receiver) && GetFrequency(receiver.Comp.Frequency) == freq;
    }

    public bool TryGetReceiver(Entity<PacketNetworkComponent?> sender, int freq, string address, out Entity<PacketNetworkComponent> receiver)
    {
        if (!(_packetEntities.TryGetValue(address, out receiver) && Exists(receiver) && GetFrequency(receiver.Comp.Frequency) == freq))
            return false;

        return ValidateReceiver(receiver, sender);
    }

    /// <summary>
    /// Tries to get random receiver in range. Used for pinging.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="freq"></param>
    /// <param name="range"></param>
    /// <param name="receiver"></param>
    /// <returns></returns>
    public bool TryRandomReceiver(Entity<PacketExecutorComponent> sender,
        int freq,
        int range,
        out Entity<PacketNetworkComponent> receiver)
    {
        var xform = Transform(sender);
        var closeEntities = new List<Entity<PacketNetworkComponent>>();

        foreach (var packetEntity in _packetEntities.Values)
        {
            var packetTransform = Transform(packetEntity);

            if (xform.Coordinates.TryDistance(EntityManager, packetTransform.Coordinates, out var distance)
                && distance <= range
                && GetFrequency(packetEntity.Comp.Frequency) == freq)
                closeEntities.Add(packetEntity);
        }

        if (closeEntities.Count <= 0)
        {
            receiver = default;
            return false;
        }

        receiver = closeEntities[_random.Next(closeEntities.Count)];
        return true;
    }

    /// <summary>
    /// Checks if receiver is listening for sender and if receiver has same grid as sender (Assuming receiver is in local state)
    /// </summary>
    /// <param name="receiver"></param>
    /// <param name="sender"></param>
    /// <returns></returns>
    public bool ValidateReceiver(Entity<PacketNetworkComponent> receiver, Entity<PacketNetworkComponent?> sender)
    {
        if (sender.Comp == null
            || !receiver.Comp.ListeningFrequencies.Contains(sender.Comp.Frequency))
            return false;

        if (receiver.Comp.IsGlobal)
            return true;

        var receiverTransform = Transform(receiver);
        var senderTransform = Transform(sender);

        return receiverTransform.GridUid == senderTransform.GridUid;
    }

    public int GetFrequency(ProtoId<PacketFrequencyPrototype> freqProto)
    {
        return _frequencies.TryGetValue(freqProto, out var freq) ? freq : 0;
    }

    public ProtoId<PacketFrequencyPrototype> GetFrequency(int freqProto)
    {
        return _frequencies.FirstOrDefault(key => key.Value == freqProto).Key;
    }
}
