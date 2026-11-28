namespace Content.Server._KS14.Packet.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class PacketFieldAttribute(PacketFieldType fieldType) : Attribute
{
    public PacketFieldType FieldType = fieldType;
}

public enum PacketFieldType
{
    Accepts,
    Returns,
}
