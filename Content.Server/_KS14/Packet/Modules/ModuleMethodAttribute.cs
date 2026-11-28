namespace Content.Server._KS14.Packet.Modules;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuleMethodAttribute(string? method = null) : Attribute
{
    public string? Method = method;
}
