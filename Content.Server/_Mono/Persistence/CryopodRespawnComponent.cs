namespace Content.Server._Mono.Persistence;

[RegisterComponent]
public sealed partial class CryopodRespawnComponent : Component
{
    [DataField] public List<CryopodReservation> Reservations = new();
}

[DataDefinition]
public sealed partial class CryopodReservation
{
    [DataField(customTypeSerializer: typeof(CryopodGuidSerializer))] public Guid Id;
    [DataField(customTypeSerializer: typeof(CryopodGuidSerializer))] public Guid User;
    [DataField] public int Slot;
    [DataField] public string? Job;
    [DataField] public string Body = string.Empty;
}
