using System.Diagnostics.CodeAnalysis;
using Content.Client._EinsteinEngines.Language.Systems;
using Content.Shared._EinsteinEngines.Language;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Mono.Language.RichText;

/// <summary>
/// Injects text that can only be seen in its original form if the local player has a LanguageSpeakerComponent and can understand the specified language.
/// </summary>
/// <example>The following text is written in Canilunzt: [language="Canilunzt" text="Hello, world!"/]</example>
[UsedImplicitly]
public sealed class LanguageMarkupTag : IMarkupTagHandler
{
    public string Name => "language";
    public static readonly string Example = "[language=\"Canilunzt\" text=\"Hello, world!\"/]";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
{
    control = null;

    if (!node.Value.TryGetString(out var name)
        || !node.Attributes.TryGetValue("text", out var textAttr)
        || !textAttr.TryGetString(out var text))
        return false;

    var protoMan = IoCManager.Resolve<IPrototypeManager>();
    if (!protoMan.TryIndex(name, out LanguagePrototype? languageProto))
        return false;

    // Keep these in sync with TextBefore
    if (languageProto.SpeechOverride is { RequireSpeech: false, AllowRadio: false })
        return false;

    if (!IoCManager.Resolve<IEntitySystemManager>().TryGetEntitySystem<LanguageSystem>(out var languageSys))
        return false;

    var knowsLanguage = languageSys.CanLocalPlayerUnderstand(languageProto.ID);
    var color = (languageProto.SpeechOverride.Color ?? Color.White).WithAlpha(1f);

    // Font: mirrors FontTag.CreateFont, but falls back instead of throwing
    Font? font = null;
    var over = languageProto.SpeechOverride;
    if (over.FontId != null || over.FontSize != null)
    {
        var fontId = over.FontId ?? FontTag.DefaultFont;
        var size = over.FontSize ?? FontTag.DefaultSize;

        var hijack = IoCManager.Resolve<FontTagHijackHolder>();
        if (hijack.Hijack?.Invoke(fontId, size) is { } overridden)
        {
            font = overridden;
        }
        else if (protoMan.TryIndex<FontPrototype>(fontId, out var fontProto)
                 || protoMan.TryIndex(FontTag.DefaultFont, out fontProto))
        {
            #pragma warning disable CS0618
            var fontPath = fontProto.Path; // will bite me in the ass later but we ball
            #pragma warning restore CS0618

            if (IoCManager.Resolve<IResourceCache>().TryGetResource<FontResource>(fontPath, out var resource))
                font = new VectorFont(resource, size);
        }
    }

    // ObfuscationFont means "only use the custom font when the text is obfuscated"
    var fontOnlyWhenObfuscated = over.ObfuscationFont == true;

    var label = new Label
    {
        FontColorOverride = color,
        MouseFilter = Control.MouseFilterMode.Stop,
        TooltipDelay = 0.5f,
    };

    var showObfuscated = !knowsLanguage;

    void Refresh()
    {
        label.Text = showObfuscated
            ? languageSys.ObfuscateSpeech(text, languageProto)
            : text;
        label.FontOverride = !fontOnlyWhenObfuscated || showObfuscated ? font : null;
    }

    Refresh();

    // Subscribed once, so no stacking handlers
    if (knowsLanguage)
    {
        label.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIRightClick)
                return;

            showObfuscated = !showObfuscated;
            Refresh();
        };
    }

    label.TooltipSupplier = _ =>
    {
        var msg = new FormattedMessage();
        msg.AddText("Written in ");
        msg.PushColor(color);
        msg.AddText(languageProto.Name);
        msg.Pop();
        msg.AddText($". You {(knowsLanguage ? "know this language" : "don't know this language")}.");

        if (knowsLanguage)
            msg.AddText("\nRight click to toggle the obfuscated version.");

        var t = new Tooltip();
        t.SetMessage(msg);
        return t;
    };

    control = label;
    return true;
}

    // We want to render the value of this tag as text. Errors are written in all-caps to hopefully throw the reader off and prevent them from thinking it's IC text.
    public string TextBefore(MarkupNode node)
    {
        if (!node.Value.TryGetString(out var languageId))
            return $"[ERROR: SPECIFY A LANGUAGE ID. EXAMPLE: {Example} ]";

        if (!node.Attributes.TryGetValue("text", out var textAttr) || !textAttr.TryGetString(out _))
            return $"[ERROR: SPECIFY A TEXT ATTRIBUTE. EXAMPLE: {Example} ]";

        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        if (!protoMan.TryIndex<LanguagePrototype>(languageId, out var language))
            return $"[ERROR: LANGUAGE WITH ID {languageId} DOES NOT EXIST. CONSULT THE /languagelist COMMAND.]";

        if (!IoCManager.Resolve<IEntitySystemManager>().TryGetEntitySystem<LanguageSystem>(out _))
            return "[ERROR: LANGUAGE SYSTEM HAS NOT BEEN LOADED. THIS IS A BUG.]";

        // Mono: we don't intend to have written-only languages, just check these
        if (language.SpeechOverride is { RequireSpeech: false, AllowRadio: false })
            return "[ERROR: THIS LANGUAGE CANNOT BE WRITTEN]";

        return string.Empty;
    }
}
