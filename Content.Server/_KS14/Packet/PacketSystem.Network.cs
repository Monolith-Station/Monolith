using System.Diagnostics.CodeAnalysis;
using Content.Server._KS14.Packet.Components;
using Content.Server._KS14.Packet.Modules.Base;
using Content.Server._KS14.Packet.Modules.Network;

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
    private Dictionary<string, PacketNetwork> _networks = new();

    /// <summary>
    /// Tries to create network.
    /// Returns "NULL" if there's no addresses.
    /// </summary>
    /// <param name="addresses"></param>
    /// <param name="frequency"></param>
    /// <returns></returns>
    public string CreateNetwork(string[] addresses, int frequency)
    {
        var networkAddress = GenerateAddress();
        var entAddresses = new List<string>();

        foreach (var address in addresses)
        {
            if (!TryGetReceiver(frequency, address, out var receiver))
                continue;

            if (receiver.Comp.AddressNetwork != null)
                continue;

            receiver.Comp.AddressNetwork = networkAddress;
            entAddresses.Add(receiver.Comp.Address);
        }

        if (entAddresses.Count == 0)
            return "NULL";

        _networks.Add(networkAddress, new PacketNetwork(frequency, entAddresses.ToArray()));

        return networkAddress;
    }

    public bool TryGetNetwork(string address, [NotNullWhen(returnValue: true)] out PacketNetwork? network)
    {
        return _networks.TryGetValue(address, out network);
    }

    #region Data

    /// <summary>
    /// Tries to send data to ReceiveDataMethod
    /// </summary>
    /// <param name="data"></param>
    /// <param name="receiver"></param>
    /// <param name="executorComponent"></param>
    public void SendData(object data, EntityUid receiver, PacketExecutorComponent? executorComponent = null)
    {
        SendData(data, receiver, typeof(ReceiveDataMethod), "NetworkPacketModule", executorComponent);
    }

    /// <summary>
    /// Tries to send data to the method.
    /// Sending data is loading object into method channel for it to be readen.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="receiver"></param>
    /// <param name="methodType"></param>
    /// <param name="moduleName"></param>
    /// <param name="executorComponent"></param>
    public void SendData(object data, EntityUid receiver, Type methodType, string moduleName, PacketExecutorComponent? executorComponent = null)
    {
        if (!Resolve(receiver, ref executorComponent)
            || !TryGetMethods((receiver, executorComponent), moduleName, out var methods)
            || !TryFindMethod(methods, methodType,  out var method))
            return;

        method.Channel.Writer.WriteAsync(data);
    }

    #endregion
}
