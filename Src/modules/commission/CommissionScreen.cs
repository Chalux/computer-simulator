using ComputerSimulator.Modules.Menu;
using ComputerSimulator.Modules.Workbench;
using Godot;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Base;

namespace ComputerSimulator.Modules.Commission;

/// <summary>
/// 委托简介 / 回执页：路由内容来自 <see cref="CommissionController"/> 快照，
/// 页面骨架来自 <c>commission_brief.tscn</c>，路由意图交回组合根。
/// </summary>
public partial class CommissionScreen : BaseWin
{
    public event Action? Accepted;
    public event Action? BackRequested;

    private CommissionController _controller = null!;
    private ScrollContainer _scroll = null!;
    private PanelContainer _card = null!;
    private VBoxContainer _body = null!;
    private bool _forwardingIntent;
    private Button? _primaryButton;

    public override string UIId => UiRoutes.Commission;
    public override string UIDir => UiRoutes.CommissionDir;

    protected override void OnCreate()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = WorkbenchStyle.CreateTheme();
        MouseFilter = MouseFilterEnum.Stop;

        _controller = Facade<CommissionController>();
        _scroll = GetNode<ScrollContainer>("%Scroll");
        _card = GetNode<PanelContainer>("%Card");
        _body = GetNode<VBoxContainer>("%Body");

        _card.AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.Surface, WorkbenchStyle.Border, 28, 24, 3));
        _body.AddThemeConstantOverride("separation", 14);
    }

    protected override void InitEvent()
    {
        Binder.OnResized(_scroll, SizeCard);
    }

    protected override void OnOpen()
    {
        SizeCard();
        _controller.OpenBrief();
        Render(_controller.Snapshot);
    }

    protected override void UpdateView()
    {
    }

    protected override void OnLayerVisibleUpdate()
    {
        if (!Visible || _primaryButton is null || !GodotObject.IsInstanceValid(_primaryButton)) return;
        if (GetViewport().GuiGetFocusOwner() != null) return;
        _primaryButton.GrabFocus();
    }

    private void SizeCard()
    {
        _card.CustomMinimumSize = new Vector2(Mathf.Clamp(_scroll.Size.X - 14, 280, 570), 0);
    }

    [EventSubscribe(ECommissionEvent.StateChanged)]
    private void OnStateChanged(CommissionSnapshot snapshot)
    {
        if (_forwardingIntent) return;
        Render(snapshot);
    }

    private void Render(CommissionSnapshot snapshot)
    {
        foreach (Node child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        CommissionReceipt? receipt = snapshot.Receipt;
        string? readError = snapshot.BriefError;
        var order = _controller.Order;

        _body.AddChild(WorkbenchStyle.MakeLabel(
            $"个人委托 {receipt?.Id ?? order.Id}  /  " +
            $"{(receipt is null ? order.ModeLabel : "已交付")}",
            13, WorkbenchStyle.Mint, WorkbenchStyle.TextFont));
        Label title = WorkbenchStyle.MakeLabel(
            receipt?.Title ?? order.Title, 30, WorkbenchStyle.Text, WorkbenchStyle.TextFont);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(title);
        _body.AddChild(new TechLine
        {
            CustomMinimumSize = new Vector2(0, 1),
            Tint = WorkbenchStyle.Border,
            Strength = 1f,
        });

        if (receipt is null)
        {
            AddDetail(_body, "客户留言", order.CustomerMessage);
            AddDetail(_body, "配件预算上限", $"¥{order.Budget:N0}");
            AddDetail(_body, "装机费", $"¥{order.BuildFee:N0} · 配件按实际成本结算");
            if (readError is not null)
                AddDetail(_body, "存档状态", readError);
        }
        else
        {
            AddDetail(_body, "订单要求",
                $"¥{receipt.Budget:N0} 以内 · {receipt.RequiredGpu} · 至少 {receipt.MinimumMemoryGb} GB");
            AddDetail(_body, "交付配置",
                $"{receipt.ProcessorName}  /  {receipt.GpuName}  /  {receipt.BoardName}\n" +
                $"{receipt.MemoryName} {receipt.MemoryGb} GB（{string.Join(" + ", receipt.MemorySlotNames)}） /  {receipt.StorageName}\n" +
                $"{receipt.CaseName}  /  {receipt.CoolerName}  /  {receipt.PowerSupplyName}");
            AddDetail(_body, "负载测试",
                $"{receipt.Report.Workload} · 通过 · {receipt.Report.Total.Score:F1} 分 / {receipt.Report.Total.Grade}\n" +
                $"持续 {receipt.LoadResult.Sustained} W  /  峰值 {receipt.LoadResult.Peak} W  /  {receipt.Report.Noise:F1} dBA");
            AddDetail(_body, "费用明细", $"配件 ¥{receipt.PartsCost:N0}  +  装机费 ¥{receipt.BuildFee:N0}");
            AddDetail(_body, "客户实付", $"¥{receipt.CustomerTotal:N0}");
        }

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 10);
        _body.AddChild(actions);

        var back = new Button { Text = "返回模式选择" };
        WorkbenchStyle.ApplyGhost(back, 36);
        actions.AddChild(back);
        back.Pressed += () => BackRequested?.Invoke();

        if (receipt is null && readError is null)
        {
            var accept = new Button { Text = "接单并装配" };
            WorkbenchStyle.ApplyPrimary(accept, 36);
            actions.AddChild(accept);
            accept.Pressed += Accept;
            accept.GrabFocus();
            _primaryButton = accept;
        }
        else
        {
            back.GrabFocus();
            _primaryButton = back;
        }
    }

    private void Accept()
    {
        _forwardingIntent = true;
        CommissionStage stage;
        try
        {
            stage = _controller.AcceptOrder().Stage;
        }
        finally
        {
            _forwardingIntent = false;
        }

        if (stage == CommissionStage.Configuring) Accepted?.Invoke();
    }

    private static void AddDetail(VBoxContainer body, string title, string value)
    {
        var detail = new VBoxContainer();
        detail.AddThemeConstantOverride("separation", 3);
        detail.AddChild(WorkbenchStyle.MakeLabel(title, 12, WorkbenchStyle.Muted, WorkbenchStyle.TextFont));
        Label text = WorkbenchStyle.MakeLabel(value, 15, WorkbenchStyle.Text, WorkbenchStyle.TextFont);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.AddChild(text);
        body.AddChild(detail);
    }
}
