using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Radar;

[ByRefEvent]
public readonly record struct BiomeTerrainChangedEvent(EntityUid Map);

[Serializable, NetSerializable]
public readonly record struct RadarTerrainChunk(Vector2i Index, int Step)
{
    public const int Size = 32;
    public Vector2i Origin => Index * (Size * Step);
}

[Serializable, NetSerializable]
public sealed class RequestRadarTerrainEvent(NetEntity console, NetEntity map, RadarTerrainChunk[] chunks) : EntityEventArgs
{
    public readonly NetEntity Console = console;
    public readonly NetEntity Map = map;
    public readonly RadarTerrainChunk[] Chunks = chunks;
}

[Serializable, NetSerializable]
public sealed class RadarTerrainChunkEvent(NetEntity map, RadarTerrainChunk chunk, ushort[] indices, uint[] pixels) : EntityEventArgs
{
    public readonly NetEntity Map = map;
    public readonly RadarTerrainChunk Chunk = chunk;
    public readonly ushort[] Indices = indices;
    public readonly uint[] Pixels = pixels;
}

[Serializable, NetSerializable]
public sealed class RadarTerrainInvalidatedEvent(NetEntity map, Vector2i[] chunks) : EntityEventArgs
{
    public readonly NetEntity Map = map;
    public readonly Vector2i[] Chunks = chunks;
}
