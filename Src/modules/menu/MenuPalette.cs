using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单唯一色彩与字体来源。色号与用法见 <c>.doc/spec/ui-color-tokens.md</c>。
/// 六个令牌：Ink / Graphite / Steel / Platinum / Ash / Signal，其中只有 Signal 是强调色。
/// </summary>
public static class MenuPalette
{
    /// <summary>全屏底色。</summary>
    public static readonly Color Ink = new("#05080a");

    /// <summary>弹层与浮起面。</summary>
    public static readonly Color Graphite = new("#0a1014");

    /// <summary>1px 分割线与描边。</summary>
    public static readonly Color Steel = new("#1a2429");

    /// <summary>标题与主文本。</summary>
    public static readonly Color Platinum = new("#eef2f3");

    /// <summary>次要文本与未激活态。</summary>
    public static readonly Color Ash = new("#8b99a1");

    /// <summary>唯一强调色：激活编号、重点线、状态点。</summary>
    public static readonly Color Signal = new("#5be3d0");

    /// <summary>标题字体：窄体轻字重，贴近工业丝印。</summary>
    public static readonly Font Display = new SystemFont
    {
        FontNames = ["Bahnschrift Light", "Bahnschrift", "Segoe UI Light", "Microsoft YaHei UI"],
        FontWeight = 300,
    };

    /// <summary>中文与正文。</summary>
    public static readonly Font Text = new SystemFont
    {
        FontNames = ["Microsoft YaHei UI", "Noto Sans CJK SC", "Segoe UI"],
    };

    /// <summary>编号与英文元信息。</summary>
    public static readonly Font Mono = new SystemFont
    {
        FontNames = ["Cascadia Mono", "Consolas", "JetBrains Mono", "Microsoft YaHei UI"],
    };

    public static Color Alpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    public static Color Mix(Color a, Color b, float t) => a.Lerp(b, t);

    public static Theme CreateTheme() => new()
    {
        DefaultFont = Text,
        DefaultFontSize = 14,
    };

    /// <summary>生成直角样式盒（仅弹层使用）。</summary>
    public static StyleBoxFlat Box(Color fill, Color border, int borderWidth = 1) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        AntiAliasing = true,
    };

    /// <summary>
    /// 逐字绘制带字距的文本。Label 不支持字距，所有小号元信息都走这里。
    /// </summary>
    public static void DrawTracked(CanvasItem target, Font font, Vector2 baseline, string text, int size,
        float tracking, Color color)
    {
        float x = baseline.X;
        foreach (char ch in text)
        {
            string glyph = ch.ToString();
            target.DrawString(font, new Vector2(x, baseline.Y), glyph, HorizontalAlignment.Left, -1, size, color);
            x += font.GetStringSize(glyph, HorizontalAlignment.Left, -1, size).X + tracking;
        }
    }

    /// <summary>测量带字距文本的宽度。</summary>
    public static float MeasureTracked(Font font, string text, int size, float tracking)
    {
        float width = 0f;
        foreach (char ch in text)
        {
            width += font.GetStringSize(ch.ToString(), HorizontalAlignment.Left, -1, size).X + tracking;
        }
        return Mathf.Max(0f, width - tracking);
    }

    /// <summary>计算在给定高度内垂直居中时的文本基线 Y。</summary>
    public static float Baseline(Font font, float top, float height, int size)
        => top + (height + font.GetAscent(size) - font.GetDescent(size)) * 0.5f;

    private static Texture2D? _sliderHandle;

    /// <summary>滑杆手柄：一根竖向细光条，替代默认的白色圆点。</summary>
    public static Texture2D SliderHandle => _sliderHandle ??= BuildSliderHandle();

    private static Texture2D BuildSliderHandle()
    {
        const int w = 8, h = 16;
        Image image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        for (int y = 2; y < h - 2; y++)
        {
            image.SetPixel(3, y, Signal);
            image.SetPixel(4, y, Signal);
            image.SetPixel(2, y, Alpha(Signal, 0.28f));
            image.SetPixel(5, y, Alpha(Signal, 0.28f));
        }
        for (int y = 5; y < 11; y++)
        {
            image.SetPixel(3, y, Colors.White);
            image.SetPixel(4, y, Colors.White);
        }

        return ImageTexture.CreateFromImage(image);
    }
}
