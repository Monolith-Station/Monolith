using Content.Shared.ArtilleryDetection;

namespace Content.Client.ArtilleryDetection.Systems;

public sealed class ArtilleryDetectionClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Logger.InfoS("artdet.client", "Artillery Detection Client System initialized");
    }
}
