using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Rat.Overwatch;

/// <summary>
/// Оверлей для отображения объявлений Overwatch.
/// </summary>
public sealed class OverwatchAnnouncementOverlay : Overlay
{
    private const string FontPath = "/Fonts/Helvetica/Helvetica-Bold.ttf";
    private const int TitleFontSize = 20;
    private const int MessageFontSize = 25;
    private const float TitleAnimationDuration = 1.5f;
    private const float MessageAnimationDuration = 2.5f;
    private const float AnnouncementDisplayDuration = 5f;

    private readonly IResourceCache _cache;
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private Font _titleFont = default!;
    private Font _messageFont = default!;

    private string? Title;
    private int TitleIndex;
    private bool TitleReverse;
    private Vector2 TitlePosition;
    private TimeSpan TitleCharInterval;
    private Color TitleColor;
    private TimeSpan _nextUpdTitle;

    private string? Text;
    private int Index;
    private bool Reverse;
    private Vector2 Position;
    private TimeSpan CharInterval;
    private Color TextColor;
    private TimeSpan _nextUpd;

    public OverwatchAnnouncementOverlay(IResourceCache cache, IGameTiming timing)
    {
        _cache = cache;
        _timing = timing;
        _titleFont = _cache.GetFont(FontPath, TitleFontSize);
        _messageFont = _cache.GetFont(FontPath, MessageFontSize);
    }

    /// <summary>
    /// Сбрасывает все параметры оверлея.
    /// </summary>
    public void Reset()
    {
        Title = null;
        TitleIndex = 0;
        TitleReverse = false;
        TitlePosition = Vector2.Zero;
        TitleColor = Color.White;
        _nextUpdTitle = TimeSpan.Zero;

        Text = null;
        Index = 0;
        Reverse = false;
        Position = Vector2.Zero;
        TextColor = Color.White;
        _nextUpd = TimeSpan.Zero;
    }

    /// <summary>
    /// Устанавливает текст объявления для отображения с эффектом печатной машинки.
    /// </summary>
    /// <param name="title">Заголовок объявления.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="color">Цвет текста.</param>
    public void SetText(string title, string message, Color color)
    {
        Title = title;
        TitleIndex = 0;
        TitleReverse = false;
        TitlePosition = Vector2.Zero;
        TitleColor = color;
        _nextUpdTitle = TimeSpan.Zero;

        TitleCharInterval = title.Length > 0
            ? TimeSpan.FromSeconds(TitleAnimationDuration / title.Length)
            : TimeSpan.Zero;

        Text = message;
        Index = 0;
        Reverse = false;
        Position = Vector2.Zero;
        TextColor = color;
        _nextUpd = TimeSpan.Zero;

        CharInterval = message.Length > 0
            ? TimeSpan.FromSeconds(MessageAnimationDuration / message.Length)
            : TimeSpan.Zero;
    }

    /// <inheritdoc/>
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var viewport = new Vector2(args.ViewportBounds.Width, args.ViewportBounds.Height);

        if (Position == Vector2.Zero)
            Position = CalcPosition(_messageFont, Text, viewport, 180);

        args.ScreenHandle.DrawString(_messageFont, Position, Text[..Index], TextColor);

        if (TitlePosition == Vector2.Zero)
        {
            var titleSize = CalcTextSize(_titleFont, Title);
            TitlePosition = new Vector2(Position.X, Position.Y - titleSize.Y - 10);
        }

        DrawTitle(args);

        if (_nextUpd > _timing.CurTime)
            return;

        if (!Reverse && Index == Text.Length)
        {
            Reverse = true;
            _nextUpd += TimeSpan.FromSeconds(AnnouncementDisplayDuration);
            return;
        }

        if (Reverse && Index == 0)
        {
            Reset();
            return;
        }

        Index = Reverse ? Index - 1 : Index + 1;

        if (_nextUpd == TimeSpan.Zero)
            _nextUpd = _timing.CurTime;
        _nextUpd += CharInterval;
    }

    /// <summary>
    /// Отрисовывает заголовок объявления с эффектом печатной машинки.
    /// </summary>
    private void DrawTitle(in OverlayDrawArgs args)
    {
        if (string.IsNullOrEmpty(Title))
            return;

        args.ScreenHandle.DrawString(_titleFont, TitlePosition, Title[..TitleIndex], TitleColor);

        if (_nextUpdTitle > _timing.CurTime)
            return;

        if (!TitleReverse && TitleIndex == Title.Length)
        {
            TitleReverse = true;
            _nextUpdTitle += TimeSpan.FromSeconds(AnnouncementDisplayDuration);
            return;
        }

        if (TitleReverse && TitleIndex == 0)
        {
            Title = null;
            return;
        }

        TitleIndex = TitleReverse ? TitleIndex - 1 : TitleIndex + 1;

        if (_nextUpdTitle == TimeSpan.Zero)
            _nextUpdTitle = _timing.CurTime;
        _nextUpdTitle += TitleCharInterval;
    }

    /// <summary>
    /// Вычисляет позицию центрирования текста с учётом размера вьюпорта.
    /// </summary>
    private Vector2 CalcPosition(Font font, string str, Vector2 viewport, int yOffset)
    {
        var strSize = CalcTextSize(font, str);

        return new Vector2((viewport.X - strSize.X) / 2, strSize.Y + yOffset);
    }

    /// <summary>
    /// Вычисляет размер текста в пикселях.
    /// </summary>
    private Vector2 CalcTextSize(Font font, string? str)
    {
        Vector2 strSize = new();
        if (string.IsNullOrEmpty(str))
            return strSize;

        foreach (Rune r in str)
        {
            if (font.TryGetCharMetrics(r, 1, out var metrics))
            {
                strSize.X += metrics.Width;
                strSize.Y = Math.Max(strSize.Y, metrics.Height);
            }
        }
        return strSize;
    }
}
