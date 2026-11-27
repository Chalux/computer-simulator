using ComputerSimulator.Modules.Commission;
using Godot;
using KemoCard.Frame.UI;

namespace ComputerSimulator.Modules.Workbench;

public partial class ClearanceWorkbench
{
    public event Action? CommissionDelivered;

    private Button _deliver = null!;
    private Label _budgetCheck = null!, _gpuCheck = null!, _memoryCheck = null!, _testCheck = null!;
    private Label _deliveryError = null!;

    private CommissionSnapshot? _shownCommission;

    private void InitializeCommissionView()
    {
        _deliver = GetNode<Button>("%DeliverButton");
        _budgetCheck = GetNode<Label>("%BudgetCheck");
        _gpuCheck = GetNode<Label>("%GpuCheck");
        _memoryCheck = GetNode<Label>("%MemoryCheck");
        _testCheck = GetNode<Label>("%TestCheck");
        _deliveryError = GetNode<Label>("%DeliveryError");

        WorkbenchStyle.ApplyPrimary(_deliver, 34);
        _deliver.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _runLoad.Text = "运行负载测试";
        _runLoad.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Label heading = GetNode<Label>("%ChecklistHeading");
        heading.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        heading.AddThemeFontSizeOverride("font_size", 15);
        heading.AddThemeColorOverride("font_color", WorkbenchStyle.Text);

        foreach (Label label in new[] { _budgetCheck, _gpuCheck, _memoryCheck, _testCheck })
        {
            label.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", WorkbenchStyle.TextSoft);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        _deliveryError.AddThemeFontOverride("font", WorkbenchStyle.TextFont);
        _deliveryError.AddThemeFontSizeOverride("font_size", 12);
        _deliveryError.AddThemeColorOverride("font_color", WorkbenchStyle.Coral);
        _deliveryError.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _deliveryError.Hide();

        GetNode<PanelContainer>("%CommissionChecklist").AddThemeStyleboxOverride("panel",
            WorkbenchStyle.Box(WorkbenchStyle.SurfaceRaised, WorkbenchStyle.Border, 14, 10, 3));
        GetNode<PanelContainer>("%CommissionChecklist").Visible = true;
    }

    private void BindCommissionInputs()
    {
        Binder.OnPressed(_deliver, DeliverCommission);
    }

    private static void DisplayCheck(Label label, string criterion, bool passed, string incomplete = "未达标")
    {
        label.Text = $"{(passed ? "通过" : incomplete)}  ·  {criterion}";
        SetTextColor(label, passed ? WorkbenchStyle.Mint : WorkbenchStyle.Amber);
    }

    private void RenderCommissionState(CommissionSnapshot state)
    {
        if (state.Draft is not { } setup || state.Fit is not { } fit || state.Checklist is not { } checklist)
            return;

        var previous = _shownCommission;
        _shownCommission = state;
        if (!ReferenceEquals(previous?.Fit, fit))
        {
            ShowSetup(setup);
            ShowAssembly(fit, Catalog, setup, state.HasTestResult);
            if (!state.HasTestResult && state.Test is null)
            {
                _views.CurrentTab = 0;
                _views.SetTabDisabled(1, true);
            }
        }

        if (state.Test is { } run && !ReferenceEquals(previous?.Test, run))
            ShowLoad(run);

        var order = _facade.Commission!.Order;
        DisplayCheck(_budgetCheck, $"配件 ¥{checklist.PartsCost:N0} / ¥{order.Budget:N0}", checklist.WithinBudget, "超预算");
        DisplayCheck(_gpuCheck, order.RequiredGpuName, checklist.GpuMet);
        DisplayCheck(_memoryCheck, $"内存 {fit.Capacity} GB / 至少 {order.MinimumMemoryGb} GB", checklist.MemoryMet);
        string loadState = state.Test is not null ? "未通过"
            : fit.Status == "BLOCKED" ? "装配阻断"
            : state.HasTestResult ? "待复测" : "待测试";
        DisplayCheck(_testCheck, "当前配置负载测试", checklist.LoadMet, loadState);
        _deliveryError.Text = state.DeliveryError ?? "";
        _deliveryError.Visible = state.DeliveryError is not null;
        _deliver.Disabled = !state.CanDeliver;
        if (state.Stage == CommissionStage.Delivered) _runLoad.Disabled = true;
    }

    private void DeliverCommission()
    {
        if (_facade.Commission!.TryDeliver())
            CommissionDelivered?.Invoke();
    }
}
