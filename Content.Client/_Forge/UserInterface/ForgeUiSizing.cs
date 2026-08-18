// Forge-Change-full
using System.Text.RegularExpressions;
using Content.Shared._Forge.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._Forge.UserInterface;

/// <summary>
/// Client-side UI sizing helpers for Forge: chat/examine fonts and HUD/storage chrome scale.
/// These are independent from the engine-wide UI scale so one control can stay readable
/// without blowing up every window.
/// </summary>
public static class ForgeUiSizing
{
    public const int BaseButtonSize = 64;
    public const int DefaultFontSize = 12;
    public const int MinFontSize = 8;
    public const int MaxFontSize = 24;
    public const float MinHudScale = 0.75f;
    public const float MaxHudScale = 2f;
    public const float DefaultStorageTextureScale = 2f;
    public const float DefaultStorageHalfTile = 8f;

    private static readonly Regex FontSizeRegex = new(@"\[font\s+size\s*=\s*(\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IConfigurationManager? _cfg;
    private static float _hudScale = 1f;
    private static float _storageScale = 1f;
    private static int _chatFontSize = DefaultFontSize;
    private static int _examineFontSize = DefaultFontSize;

    public static event Action? HudScaleChanged;
    public static event Action? StorageScaleChanged;
    public static event Action? ChatFontSizeChanged;

    public static float HudScale
    {
        get
        {
            EnsureInitialized();
            return _hudScale;
        }
    }

    public static float StorageScale
    {
        get
        {
            EnsureInitialized();
            return _storageScale;
        }
    }

    /// <summary>
    /// TextureScale used by bag/belt grid tiles. Upstream default is 2.
    /// </summary>
    public static float StorageTextureScale => DefaultStorageTextureScale * StorageScale;

    public static int ButtonSize
    {
        get
        {
            EnsureInitialized();
            return Math.Max(32, (int) MathF.Round(BaseButtonSize * _hudScale));
        }
    }

    public static int ChatFontSize
    {
        get
        {
            EnsureInitialized();
            return _chatFontSize;
        }
    }

    public static int ExamineFontSize
    {
        get
        {
            EnsureInitialized();
            return _examineFontSize;
        }
    }

    public static void EnsureInitialized()
    {
        if (_cfg != null)
            return;

        _cfg = IoCManager.Resolve<IConfigurationManager>();
        _cfg.OnValueChanged(ForgeCVars.HudScale, OnHudScaleChanged, true);
        _cfg.OnValueChanged(ForgeCVars.StorageScale, OnStorageScaleChanged, true);
        _cfg.OnValueChanged(ForgeCVars.ChatFontSize, OnChatFontSizeChanged, true);
        _cfg.OnValueChanged(ForgeCVars.ExamineFontSize, OnExamineFontSizeChanged, true);
    }

    /// <summary>
    /// Wraps markup in the configured font size and proportionally scales nested font tags.
    /// </summary>
    public static string ApplyFontSize(string markup, int fontSize)
    {
        markup = FontSizeRegex.Replace(markup, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out var original))
                return match.Value;

            var scaled = Math.Max(1, (int) MathF.Round(original * (fontSize / (float) DefaultFontSize)));
            return $"[font size={scaled}]";
        });

        return $"[font size={fontSize}]{markup}[/font]";
    }

    public static string ApplyChatFontSize(string markup)
    {
        return ApplyFontSize(markup, ChatFontSize);
    }

    public static string ApplyExamineFontSize(string markup)
    {
        return ApplyFontSize(markup, ExamineFontSize);
    }

    private static void OnHudScaleChanged(float value)
    {
        _hudScale = Math.Clamp(value, MinHudScale, MaxHudScale);
        HudScaleChanged?.Invoke();
    }

    private static void OnStorageScaleChanged(float value)
    {
        _storageScale = Math.Clamp(value, MinHudScale, MaxHudScale);
        StorageScaleChanged?.Invoke();
    }

    private static void OnChatFontSizeChanged(int value)
    {
        _chatFontSize = Math.Clamp(value, MinFontSize, MaxFontSize);
        ChatFontSizeChanged?.Invoke();
    }

    private static void OnExamineFontSizeChanged(int value)
    {
        _examineFontSize = Math.Clamp(value, MinFontSize, MaxFontSize);
    }
}
