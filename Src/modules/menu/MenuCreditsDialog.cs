using Godot;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Menu;

/// <summary>制作名单对话框：内容全部来自场景，框架只负责打开、关闭与 Dlg 层排序。</summary>
public partial class MenuCreditsDialog : BaseDlg
{
    private ColorRect _dim = null!;
    private PanelContainer _panel = null!;
    private Button _close = null!;

    public override string UIId => UiRoutes.MenuCredits;
    public override string UIDir => UiRoutes.MenuDir;

    protected override void OnCreate()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _dim = GetNode<ColorRect>("%Dim");
        _panel = GetNode<PanelContainer>("%Panel");
        _close = GetNode<Button>("%CloseButton");

        _dim.Color = MenuPalette.Alpha(MenuPalette.Ink, 0.92f);

        StyleBoxFlat panelStyle = MenuPalette.Box(
            MenuPalette.Alpha(MenuPalette.Graphite, 0.985f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        panelStyle.ContentMarginLeft = 32;
        panelStyle.ContentMarginRight = 32;
        panelStyle.ContentMarginTop = 26;
        panelStyle.ContentMarginBottom = 26;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        Label title = GetNode<Label>("%Title");
        title.AddThemeFontOverride("font", MenuPalette.Text);
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", MenuPalette.Platinum);

        Label kicker = GetNode<Label>("%Kicker");
        kicker.AddThemeFontOverride("font", MenuPalette.Mono);
        kicker.AddThemeFontSizeOverride("font_size", 9);
        kicker.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Signal, 0.75f));

        Label intro = GetNode<Label>("%Intro");
        intro.AddThemeFontOverride("font", MenuPalette.Text);
        intro.AddThemeFontSizeOverride("font_size", 13);
        intro.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Platinum, 0.88f));
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        GridContainer grid = GetNode<GridContainer>("%InfoGrid");
        int index = 0;
        foreach (Node child in grid.GetChildren())
        {
            if (child is not Label label) continue;

            bool key = index % 2 == 0;
            label.AddThemeFontOverride("font", key ? MenuPalette.Mono : MenuPalette.Text);
            label.AddThemeFontSizeOverride("font_size", key ? 9 : 12);
            label.AddThemeColorOverride("font_color", key
                ? MenuPalette.Alpha(MenuPalette.Ash, 0.50f)
                : MenuPalette.Alpha(MenuPalette.Platinum, 0.85f));
            index++;
        }

        Label footer = GetNode<Label>("%Footer");
        footer.AddThemeFontOverride("font", MenuPalette.Mono);
        footer.AddThemeFontSizeOverride("font_size", 9);
        footer.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.42f));

        _close.Flat = true;
        _close.FocusMode = FocusModeEnum.All;
        _close.MouseDefaultCursorShape = CursorShape.PointingHand;
        _close.AddThemeFontOverride("font", MenuPalette.Text);
        _close.AddThemeFontSizeOverride("font_size", 18);
        _close.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.75f));
        _close.AddThemeColorOverride("font_hover_color", MenuPalette.Platinum);
        _close.AddThemeColorOverride("font_pressed_color", MenuPalette.Signal);
        _close.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        _close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        Label hint = GetNode<Label>("%FooterHint");
        hint.AddThemeFontOverride("font", MenuPalette.Mono);
        hint.AddThemeFontSizeOverride("font_size", 9);
        hint.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.35f));
    }

    protected override void InitEvent()
    {
        OnClicks(_close, CloseDialog);
        Binder.OnGuiInputLeftClick(_dim, CloseDialog);
    }

    protected override void OnOpen()
    {
        Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1f, 0.16).SetEase(Tween.EaseType.Out);
        _close.GrabFocus();
    }

    protected override void UpdateView()
    {
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        CloseDialog();
    }

    private void CloseDialog() => Close();
}
