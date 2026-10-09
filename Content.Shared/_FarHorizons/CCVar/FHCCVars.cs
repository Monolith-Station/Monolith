using Robust.Shared.Configuration;

namespace Content.Shared._FarHorizons.CCVar;

[CVarDefs]
public sealed partial class FHCCVars
{
    public static readonly CVarDef<bool> RenderStarSystem =
        CVarDef.Create("render.star_system", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
