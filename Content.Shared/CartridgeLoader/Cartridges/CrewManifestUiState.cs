using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class CrewManifestUiState : BoundUserInterfaceState
{
    public string StationName = "Crew Manifest"; // Monolith: Remove name
    public CrewManifestEntries? Entries;

    public CrewManifestUiState(CrewManifestEntries? entries)
    {
        Entries = entries;
    }
}
