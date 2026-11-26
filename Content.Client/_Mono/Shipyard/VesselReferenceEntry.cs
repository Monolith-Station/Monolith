namespace Content.Client._Mono.Shipyard;

public sealed class VesselReferenceEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Price;
    public string Shipyard = string.Empty;
    public List<string> Classes = new();
    public string SizeClass = string.Empty;

    public Dictionary<string, List<string>> EquipmentByCategory = new();

    public bool EquipmentLoaded;
}
