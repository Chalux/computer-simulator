using Godot;

namespace ComputerSimulator.Modules.Workbench;

/// <summary>
/// 装配工作台唯一的色彩、字体与控件样式来源。主色沿用模式菜单的
/// 石墨 / 钢灰 / 薄荷信号体系，仅状态告警使用珊瑚与琥珀。
/// </summary>
public static class WorkbenchStyle
{
    public static readonly Color Canvas = new("#0a0e11");
    public static readonly Color CanvasDeep = new("#080b0e");
    public static readonly Color Surface = new("#10161a");
    public static readonly Color SurfaceRaised = new("#151d22");
    public static readonly Color Inset = new("#0c1216");
    public static readonly Color Border = new("#26333a");
    public static readonly Color BorderSoft = new("#1b252b");
    public static readonly Color Text = new("#e9eeef");
    public static readonly Color TextSoft = new("#bcc8cd");
    public static readonly Color Muted = new("#93a5ad");
    public static readonly Color Faint = new("#6d8088");
    public static readonly Color DisabledText = new("#7f929a");
    public static readonly Color Mint = new("#5be3d0");
    public static readonly Color Amber = new("#e8bd7a");
    public static readonly Color Coral = new("#f28d80");

    public static readonly Font TextFont = new SystemFont
    {
        FontNames = ["Microsoft YaHei UI", "Noto Sans CJK SC", "Source Han Sans SC", "Segoe UI"],
    };

    public static readonly Font DisplayFont = new SystemFont
    {
        FontNames = ["Bahnschrift Light", "Bahnschrift", "Segoe UI Light", "Microsoft YaHei UI"],
        FontWeight = 300,
    };

    public static readonly Font MonoFont = new SystemFont
    {
        FontNames = ["Cascadia Mono", "Consolas", "JetBrains Mono", "Microsoft YaHei UI"],
    };

    public static Color Alpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    public static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFont = TextFont, DefaultFontSize = 13 };
        theme.SetColor("font_color", "Label", TextSoft);

        BuildButtons(theme);
        BuildOptionButtons(theme);
        BuildCheckBoxes(theme);
        BuildSlider(theme);
        BuildTabs(theme);
        BuildScrollBars(theme);
        BuildPopups(theme);

        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = BorderSoft, Thickness = 1 });
        theme.SetConstant("separation", "HSeparator", 0);
        return theme;
    }

    private static void BuildButtons(Theme theme)
    {
        theme.SetStylebox("normal", "Button", Box(SurfaceRaised, BorderSoft, 12, 6, 3));
        theme.SetStylebox("hover", "Button", Box(new Color("#1a242a"), new Color("#3d4f58"), 12, 6, 3));
        theme.SetStylebox("pressed", "Button", Box(new Color("#202d34"), new Color("#5b707a"), 12, 6, 3));
        theme.SetStylebox("hover_pressed", "Button", Box(new Color("#223139"), Mint, 12, 6, 3));
        theme.SetStylebox("disabled", "Button", Box(Alpha(Surface, .65f), BorderSoft, 12, 6, 3));
        theme.SetStylebox("focus", "Button", FocusRing(Mint, 12, 6, 3));
        theme.SetColor("font_color", "Button", TextSoft);
        theme.SetColor("font_hover_color", "Button", Text);
        theme.SetColor("font_pressed_color", "Button", Mint);
        theme.SetColor("font_hover_pressed_color", "Button", Mint);
        theme.SetColor("font_focus_color", "Button", Text);
        theme.SetColor("font_disabled_color", "Button", DisabledText);
        theme.SetFontSize("font_size", "Button", 12);
    }

    private static void BuildOptionButtons(Theme theme)
    {
        theme.SetStylebox("normal", "OptionButton", Box(Surface, Border, 10, 6, 3));
        theme.SetStylebox("hover", "OptionButton", Box(new Color("#182228"), new Color("#40525b"), 10, 6, 3));
        theme.SetStylebox("pressed", "OptionButton", Box(new Color("#1d2a30"), new Color("#5b707a"), 10, 6, 3));
        theme.SetStylebox("disabled", "OptionButton", Box(Alpha(Surface, .55f), BorderSoft, 10, 6, 3));
        theme.SetStylebox("focus", "OptionButton", FocusRing(Mint, 10, 6, 3));
        theme.SetColor("font_color", "OptionButton", Text);
        theme.SetColor("font_hover_color", "OptionButton", Text);
        theme.SetColor("font_pressed_color", "OptionButton", Mint);
        theme.SetColor("font_focus_color", "OptionButton", Text);
        theme.SetColor("font_disabled_color", "OptionButton", DisabledText);
        theme.SetFontSize("font_size", "OptionButton", 14);
        theme.SetConstant("arrow_margin", "OptionButton", 6);
    }

    private static void BuildCheckBoxes(Theme theme)
    {
        theme.SetStylebox("normal", "CheckBox", EmptyBox(3, 2));
        theme.SetStylebox("hover", "CheckBox", Box(Alpha(Text, .04f), Colors.Transparent, 3, 2, 2, 0));
        theme.SetStylebox("pressed", "CheckBox", Box(Alpha(Mint, .07f), Colors.Transparent, 3, 2, 2, 0));
        theme.SetStylebox("disabled", "CheckBox", EmptyBox(3, 2));
        theme.SetStylebox("focus", "CheckBox", FocusRing(Mint, 3, 2, 2));
        theme.SetColor("font_color", "CheckBox", Muted);
        theme.SetColor("font_hover_color", "CheckBox", Text);
        theme.SetColor("font_pressed_color", "CheckBox", Mint);
        theme.SetColor("font_hover_pressed_color", "CheckBox", Mint);
        theme.SetColor("font_focus_color", "CheckBox", Text);
        theme.SetColor("font_disabled_color", "CheckBox", DisabledText);
        theme.SetFontSize("font_size", "CheckBox", 13);
    }

    private static void BuildSlider(Theme theme)
    {
        theme.SetStylebox("slider", "HSlider", Box(Alpha(Border, .8f), Colors.Transparent, 0, 2, 1, 0));
        theme.SetStylebox("grabber_area", "HSlider", Box(Alpha(Mint, .52f), Colors.Transparent, 0, 2, 1, 0));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Box(Alpha(Mint, .78f), Colors.Transparent, 0, 2, 1, 0));
        Texture2D grabber = SliderTexture(Mint, false);
        Texture2D hot = SliderTexture(Mint, true);
        theme.SetIcon("grabber", "HSlider", grabber);
        theme.SetIcon("grabber_highlight", "HSlider", hot);
        theme.SetIcon("grabber_disabled", "HSlider", grabber);
    }

    private static void BuildTabs(Theme theme)
    {
        foreach (string type in new[] { "TabContainer", "TabBar" })
        {
            theme.SetStylebox("tab_unselected", type, TabBox(Colors.Transparent, Colors.Transparent));
            theme.SetStylebox("tab_hovered", type, TabBox(Alpha(Text, .035f), Border));
            theme.SetStylebox("tab_selected", type, TabBox(Alpha(Mint, .07f), Mint));
            theme.SetStylebox("tab_disabled", type, TabBox(Colors.Transparent, Colors.Transparent));
            theme.SetStylebox("tab_focus", type, FocusRing(Mint, 14, 7, 2));
            theme.SetColor("font_unselected_color", type, Muted);
            theme.SetColor("font_hovered_color", type, Text);
            theme.SetColor("font_selected_color", type, Text);
            theme.SetColor("font_disabled_color", type, Faint);
            theme.SetColor("font_focus_color", type, Text);
            theme.SetFont("font", type, TextFont);
            theme.SetFontSize("font_size", type, 11);
            theme.SetConstant("h_separation", type, 4);
        }

        StyleBoxFlat bar = Box(Inset, Colors.Transparent, 0, 0, 0, 0);
        bar.BorderColor = BorderSoft;
        bar.BorderWidthBottom = 1;
        theme.SetStylebox("tabbar_background", "TabContainer", bar);
        theme.SetStylebox("panel", "TabContainer", Box(Surface, Border, 0, 0, 3));
        theme.SetConstant("side_margin", "TabContainer", 0);
        theme.SetConstant("tab_separation", "TabContainer", 4);
    }

    private static void BuildScrollBars(Theme theme)
    {
        foreach (string type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, Box(Alpha(CanvasDeep, .5f), Colors.Transparent, 2, 2, 3, 0));
            theme.SetStylebox("scroll_focus", type, Box(Alpha(CanvasDeep, .5f), Colors.Transparent, 2, 2, 3, 0));
            theme.SetStylebox("grabber", type, Box(Alpha(Muted, .38f), Colors.Transparent, 2, 2, 3, 0));
            theme.SetStylebox("grabber_highlight", type, Box(Alpha(Mint, .55f), Colors.Transparent, 2, 2, 3, 0));
            theme.SetStylebox("grabber_pressed", type, Box(Alpha(Mint, .78f), Colors.Transparent, 2, 2, 3, 0));
        }
    }

    private static void BuildPopups(Theme theme)
    {
        theme.SetStylebox("panel", "PopupMenu", Box(CanvasDeep, Border, 6, 6, 3));
        theme.SetStylebox("hover", "PopupMenu", Box(Alpha(Mint, .11f), Colors.Transparent, 0, 0, 2, 0));
        theme.SetStylebox("separator", "PopupMenu", new StyleBoxLine { Color = BorderSoft, Thickness = 1 });
        theme.SetColor("font_color", "PopupMenu", TextSoft);
        theme.SetColor("font_hover_color", "PopupMenu", Text);
        theme.SetColor("font_disabled_color", "PopupMenu", Faint);
        theme.SetColor("font_separator_color", "PopupMenu", Faint);
        theme.SetColor("font_accelerator_color", "PopupMenu", Muted);
        theme.SetFontSize("font_size", "PopupMenu", 14);
        theme.SetConstant("v_separation", "PopupMenu", 3);
        theme.SetConstant("item_start_padding", "PopupMenu", 10);
        theme.SetConstant("item_end_padding", "PopupMenu", 10);
        theme.SetConstant("indent", "PopupMenu", 10);

        theme.SetStylebox("panel", "TooltipPanel", Box(CanvasDeep, Border, 8, 4, 2));
        theme.SetColor("font_color", "TooltipLabel", TextSoft);
        theme.SetFontSize("font_size", "TooltipLabel", 11);
    }

    public static StyleBoxFlat Box(Color fill, Color border, int padH = 0, int padV = 0, int radius = 3, int borderWidth = 1)
    {
        var box = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            ContentMarginLeft = padH,
            ContentMarginRight = padH,
            ContentMarginTop = padV,
            ContentMarginBottom = padV,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            AntiAliasing = true,
        };
        return box;
    }

    private static StyleBoxEmpty EmptyBox(int padH, int padV) => new()
    {
        ContentMarginLeft = padH,
        ContentMarginRight = padH,
        ContentMarginTop = padV,
        ContentMarginBottom = padV,
    };

    private static StyleBoxFlat FocusRing(Color color, int padH, int padV, int radius) =>
        Box(Colors.Transparent, color, padH, padV, radius);

    private static StyleBoxFlat TabBox(Color fill, Color border)
    {
        StyleBoxFlat box = Box(fill, border, 14, 7, 2, 0);
        box.BorderWidthBottom = 2;
        return box;
    }

    private static Texture2D SliderTexture(Color color, bool hot)
    {
        const int width = 10, height = 18;
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        for (int y = 2; y < height - 2; y++)
        {
            image.SetPixel(4, y, color);
            image.SetPixel(5, y, color);
            image.SetPixel(3, y, Alpha(color, .24f));
            image.SetPixel(6, y, Alpha(color, .24f));
        }
        if (hot)
        {
            for (int y = 6; y < 12; y++)
            {
                image.SetPixel(4, y, Colors.White);
                image.SetPixel(5, y, Colors.White);
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    public static PanelContainer SurfacePanel(Color fill, Color border, int padH = 0, int padV = 0, int radius = 3)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(fill, border, padH, padV, radius));
        return panel;
    }

    public static Label MakeLabel(string content, int size, Color color, Font? font = null)
    {
        var label = new Label { Text = content, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font ?? TextFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static Label Micro(string content) => MakeLabel(content, 11, Muted);

    public static Label Field(string content) => MakeLabel(content, 13, Muted);

    public static Label Heading(string content) => MakeLabel(content, 15, Text);

    public static Label Mono(string content, int size, Color color) => MakeLabel(content, size, color, MonoFont);

    public static HSeparator Rule(Color? color = null)
    {
        var rule = new HSeparator();
        rule.AddThemeConstantOverride("separation", 0);
        if (color.HasValue)
            rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = color.Value, Thickness = 1 });
        return rule;
    }

    public static void ApplyPrimary(Button button, int minHeight = 34)
    {
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, minHeight);
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.AddThemeStyleboxOverride("normal", Box(Mint, Mint, 12, 8, 3));
        button.AddThemeStyleboxOverride("hover", Box(new Color("#78efdd"), new Color("#a9fff0"), 12, 8, 3));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("#41c8b6"), new Color("#8ff3e4"), 12, 8, 3));
        button.AddThemeStyleboxOverride("hover_pressed", Box(new Color("#41c8b6"), new Color("#8ff3e4"), 12, 8, 3));
        button.AddThemeStyleboxOverride("disabled", Box(Alpha(Mint, .08f), Alpha(Mint, .24f), 12, 8, 3));
        button.AddThemeStyleboxOverride("focus", FocusRing(new Color("#eafffb"), 12, 8, 3));
        button.AddThemeColorOverride("font_color", new Color("#06120f"));
        button.AddThemeColorOverride("font_hover_color", new Color("#04100d"));
        button.AddThemeColorOverride("font_pressed_color", new Color("#04100d"));
        button.AddThemeColorOverride("font_hover_pressed_color", new Color("#04100d"));
        button.AddThemeColorOverride("font_focus_color", new Color("#04100d"));
        button.AddThemeColorOverride("font_disabled_color", Alpha(Mint, .56f));
        button.AddThemeFontSizeOverride("font_size", 12);
    }

    public static void ApplyGhost(Button button, int minHeight = 28)
    {
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, minHeight);
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.AddThemeStyleboxOverride("normal", Box(Colors.Transparent, BorderSoft, 10, 5, 2));
        button.AddThemeStyleboxOverride("hover", Box(Alpha(Text, .045f), Border, 10, 5, 2));
        button.AddThemeStyleboxOverride("pressed", Box(Alpha(Mint, .08f), Alpha(Mint, .5f), 10, 5, 2));
        button.AddThemeStyleboxOverride("disabled", Box(Colors.Transparent, BorderSoft, 10, 5, 2));
        button.AddThemeStyleboxOverride("focus", FocusRing(Mint, 10, 5, 2));
        button.AddThemeColorOverride("font_color", Muted);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Mint);
        button.AddThemeColorOverride("font_hover_pressed_color", Mint);
        button.AddThemeColorOverride("font_focus_color", Text);
        button.AddThemeColorOverride("font_disabled_color", DisabledText);
        button.AddThemeFontSizeOverride("font_size", 11);
    }

    public static void ApplySegment(Button button)
    {
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.AddThemeStyleboxOverride("normal", Box(Colors.Transparent, Colors.Transparent, 8, 4, 2, 0));
        button.AddThemeStyleboxOverride("hover", Box(Alpha(Text, .05f), Colors.Transparent, 8, 4, 2, 0));
        button.AddThemeStyleboxOverride("pressed", Box(Alpha(Mint, .13f), Alpha(Mint, .52f), 8, 4, 2, 1));
        button.AddThemeStyleboxOverride("hover_pressed", Box(Alpha(Mint, .16f), Alpha(Mint, .65f), 8, 4, 2, 1));
        button.AddThemeStyleboxOverride("disabled", Box(Colors.Transparent, Colors.Transparent, 8, 4, 2, 0));
        button.AddThemeStyleboxOverride("focus", FocusRing(Mint, 8, 4, 2));
        button.AddThemeColorOverride("font_color", Muted);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Mint);
        button.AddThemeColorOverride("font_hover_pressed_color", Mint);
        button.AddThemeColorOverride("font_focus_color", Text);
        button.AddThemeColorOverride("font_disabled_color", DisabledText);
        button.AddThemeFontSizeOverride("font_size", 13);
    }

    public static void ApplyStatusChip(PanelContainer chip, Color color)
    {
        StyleBoxFlat box = Box(Alpha(color, .10f), Alpha(color, .42f), 10, 4, 2);
        box.BorderWidthLeft = 2;
        chip.AddThemeStyleboxOverride("panel", box);
    }
}
