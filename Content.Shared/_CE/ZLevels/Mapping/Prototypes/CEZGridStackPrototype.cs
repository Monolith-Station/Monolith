/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.ZLevels.Mapping.Prototypes;

/// <summary>
/// A multi-deck ship that spans several z-levels: each entry in <see cref="Decks"/> is a
/// single grid file, bottom-up (index 0 is the lowest deck, each following one the layer
/// directly above). Spawning loads deck i onto a z-network's layer at baseDepth + i, all at
/// the same world position so their tiles line up. The <c>CEZGridConnector</c> pillars the
/// decks carry then bind them into one z-grid network on the next connector recalc.
///
/// Author the decks aligned (same local origin on each layer) so they stack cleanly — the
/// easiest way is to map them in an empty z-stack (see the <c>Empty5</c> zMap) and save each
/// deck as its own grid.
/// </summary>
[Prototype("cezGridStack")]
public sealed partial class CEZGridStackPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Deck grid files, bottom-up. Must contain at least one.
    /// </summary>
    [DataField(required: true)]
    public List<ResPath> Decks = new();
}
