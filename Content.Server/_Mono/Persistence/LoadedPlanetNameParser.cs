using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Mono.Planets;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Mono.Persistence;

public sealed partial class LoadedPlanetNameParser : CustomTypeParser<string>
{
    [Dependency] private IEntityManager _entities = default!;

    public override bool TryParse(ParserContext ctx, [NotNullWhen(true)] out string? result)
    {
        ctx.ConsumeWhitespace();
        if (ctx.PeekRune()?.Value == '"')
            return Toolshed.TryParse(ctx, out result);

        result = ctx.GetWord(ParserContext.IsToken);
        return !string.IsNullOrEmpty(result);
    }

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var options = _entities.System<PlanetPersistenceSystem>().GetLoadedPlanets()
            .Select(planet => planet.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new CompletionOption(name));
        return CompletionResult.FromHintOptions(options, GetArgHint(arg));
    }
}
