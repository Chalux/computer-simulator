using Godot;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 尚未开放模式的通用简报页；模式通过强类型路由载荷传入，不再由组合根在入树前写属性。
/// </summary>
public partial class ModeTeaserScreen : BaseWin
{
    public event Action? BackRequested;

    private PanelContainer _panel = null!;
    private Label _title = null!;
    private Label _summary = null!;
    private TechMenuItem _back = null!;

    public override string UIId => UiRoutes.ModeBrief;
    public override string UIDir => UiRoutes.MenuDir;

    protected override void OnCreate()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuPalette.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _panel = GetNode<PanelContainer>("%Panel");
        _title = GetNode<Label>("%Title");
        _summary = GetNode<Label>("%Summary");
        _back = GetNode<TechMenuItem>("%BackItem");

        StyleBoxFlat panelStyle = MenuPalette.Box(
            MenuPalette.Alpha(MenuPalette.Graphite, 0.985f),
            MenuPalette.Alpha(MenuPalette.Steel, 0.95f), 1);
        panelStyle.ContentMarginLeft = 34;
        panelStyle.ContentMarginRight = 34;
        panelStyle.ContentMarginTop = 28;
        panelStyle.ContentMarginBottom = 28;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        _title.AddThemeFontOverride("font", MenuPalette.Text);
        _title.AddThemeFontSizeOverride("font_size", 34);
        _title.AddThemeColorOverride("font_color", MenuPalette.Platinum);

        _summary.AddThemeFontOverride("font", MenuPalette.Text);
        _summary.AddThemeFontSizeOverride("font_size", 15);
        _summary.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Platinum, 0.86f));
        _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        Label status = GetNode<Label>("%Status");
        status.AddThemeFontOverride("font", MenuPalette.Text);
        status.AddThemeFontSizeOverride("font_size", 16);
        status.AddThemeColorOverride("font_color", MenuPalette.Alpha(MenuPalette.Ash, 0.72f));

        _back.Index = "ESC";
        _back.Title = "返回模式选择";
        _back.Subtitle = "MODE SELECT";
        _back.QueueRedraw();
    }

    protected override void InitEvent()
    {
        OnClicks(_back, RequestBack);
    }

    protected override void OnOpen()
    {
        ModeBriefPayload payload = GetTypedPayload<ModeBriefPayload>();
        GameModeInfo info = Facade<MenuController>().GetMode(payload.Mode);
        _title.Text = info.Title;
        _summary.Text = info.Summary;
        PlayIntro();
    }

    protected override void UpdateView()
    {
    }

    private void RequestBack() => BackRequested?.Invoke();

    private void PlayIntro()
    {
        _panel.Modulate = MenuPalette.Alpha(Colors.White, 0f);
        Tween tween = CreateTween();
        tween.TweenProperty(_panel, "modulate:a", 1f, 0.16).SetEase(Tween.EaseType.Out);

        // 等黑场完全退去后再聚焦返回项，转场期间不接收键盘确认。
        tween.TweenInterval(0.26);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(this)) _back.GrabFocus();
        }));
    }
}
