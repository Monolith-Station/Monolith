using Content.Client._DV.Traits.UI; // Mono - Backport of Delta V trait style sheetlet
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;

        public Stylesheet SheetNano { get; private set; } = default!;
        public Stylesheet SheetSpace { get; private set; } = default!;
        public Stylesheet SheetTraits { get; private set; } = default!; // Mono - Backport of Delta V trait style sheetlet

        public void Initialize()
        {
            SheetNano = new StyleNano(_resourceCache).Stylesheet;
            SheetSpace = new StyleSpace(_resourceCache).Stylesheet;
            SheetTraits = new StyleTraits(_resourceCache, SheetNano).Stylesheet; // Mono - Backport of Delta V trait style sheetlet

            _userInterfaceManager.Stylesheet = SheetNano;
        }
    }
}
