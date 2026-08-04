// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Lua.AmmoLoader.UI;

public sealed class MarqueeLabel : Control
{
    private const float ScrollSpeedPx = 28f;
    private const float EndPauseSeconds = 1.25f;
    private const float GapPx = 32f;
    private readonly Label _label;
    private float _offset;
    private float _pauseRemaining;
    private bool _scrollingForward = true;

    public string? Text
    {
        get => _label.Text;
        set
        {
            _label.Text = value;
            ResetScroll();
            InvalidateMeasure();
        }
    }

    public Color? FontColorOverride
    {
        get => _label.FontColorOverride;
        set => _label.FontColorOverride = value;
    }

    public MarqueeLabel()
    {
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
        _label = new Label
        {
            MouseFilter = MouseFilterMode.Ignore,
            ClipText = false,
        };
        AddChild(_label);
    }

    public void ResetScroll()
    {
        _offset = 0f;
        _pauseRemaining = EndPauseSeconds;
        _scrollingForward = true;
        InvalidateArrange();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _label.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
        var height = _label.DesiredSize.Y > 0 ? _label.DesiredSize.Y : 16f;
        if (float.IsFinite(availableSize.X) && availableSize.X > 0f) return new Vector2(availableSize.X, height);
        return new Vector2(_label.DesiredSize.X, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var textSize = _label.DesiredSize;
        if (textSize.X <= finalSize.X + 0.5f)
        { _label.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize)); }
        else
        { _label.Arrange(UIBox2.FromDimensions(new Vector2(-_offset, 0), new Vector2(textSize.X, finalSize.Y))); }
        return finalSize;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var avail = Size.X;
        var textWidth = _label.DesiredSize.X;
        if (textWidth <= avail + 0.5f)
        {
            if (_offset != 0f)
            {
                _offset = 0f;
                InvalidateArrange();
            }
            return;
        }
        if (_pauseRemaining > 0f)
        { _pauseRemaining -= args.DeltaSeconds; return; }
        var maxOffset = textWidth - avail + GapPx;
        var delta = ScrollSpeedPx * args.DeltaSeconds;
        if (_scrollingForward)
        {
            _offset += delta;
            if (_offset >= maxOffset)
            {
                _offset = maxOffset;
                _scrollingForward = false;
                _pauseRemaining = EndPauseSeconds;
            }
        }
        else
        {
            _offset -= delta;
            if (_offset <= 0f)
            {
                _offset = 0f;
                _scrollingForward = true;
                _pauseRemaining = EndPauseSeconds;
            }
        }
        InvalidateArrange();
    }
}
