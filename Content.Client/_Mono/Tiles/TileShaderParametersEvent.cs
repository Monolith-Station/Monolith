using Content.Shared.Maps;
using Robust.Client.Graphics;

namespace Content.Client._Mono.Tiles;

/// <summary>
/// Raised on an entity when its tile shader is about to draw.
/// </summary>
[ByRefEvent]
public readonly record struct TileShaderParametersEvent(ShaderInstance Shader, ContentTileDefinition Tile);
