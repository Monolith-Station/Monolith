namespace Content.Server._KS14.Packet;

public sealed class PacketNetwork
{
    public int Frequency;
    public string[] Addresses;

    public PacketNetwork(int frequency, string[] addresses)
    {
        Frequency = frequency;
        Addresses = addresses;
    }
}
