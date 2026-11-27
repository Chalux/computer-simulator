using System.Text.Json;
using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Logging;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Commission;

/// <summary>
/// 委托 01-01 的唯一应用入口：打开简介、接单初始化、变更/还原配置、装配检查、负载测试、
/// 验收评估、交付与回执读写编排。规则引擎由本控制器持有，生命周期从构造到
/// <see cref="Close"/>；Model 只保存状态，视图只持有本控制器。
/// </summary>
public sealed class CommissionController : BaseController<CommissionMod>
{
    private readonly CommissionReceiptStore _store;
    private readonly ClearanceRules _rules;
    private readonly CommissionContentDefinition _order;
    private readonly BenchmarkController _benchmark;
    private bool _disposed;

    internal CommissionController(
        CommissionMod model,
        BenchmarkController benchmark,
        GameContent content,
        CommissionReceiptStore store) : base(model)
    {
        ArgumentNullException.ThrowIfNull(content);
        _benchmark = benchmark;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _order = content.Commission
            ?? throw new InvalidOperationException("内容缺少委托定义（content/commissions）。");
        _rules = new ClearanceRules(content);
    }

    /// <summary>只读目录查询：视图用它的名称/价格渲染选项，不接触规则引擎。</summary>
    public BenchCatalog Catalog => _rules.Catalog;
    public CommissionContentDefinition Order => _order;

    public CommissionSnapshot Snapshot => Model.CreateSnapshot();

    /// <summary>打开委托简介：从回执仓库恢复已交付状态，并发布一次完整快照。</summary>
    public CommissionSnapshot OpenBrief()
    {
        try
        {
            Model.ApplyReceipt(_store.Load(_order.Id), null);
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            AppLog.Warning($"读取委托记录失败：{exception.Message}", "Commission");
            Model.ApplyReceipt(null, StorageErrorMessage(
                exception, "交付记录暂时无法读取。请返回模式选择后重新打开委托。"));
        }

        Publish();
        return Snapshot;
    }

    /// <summary>接单：以目录初始配置建立草稿并立即做一次装配检查。</summary>
    public CommissionSnapshot AcceptOrder()
    {
        if (_disposed || Model.Stage != CommissionStage.Brief || Model.BriefError is not null)
        {
            Publish();
            return Snapshot;
        }

        ResetToInitial();
        return Snapshot;
    }

    /// <summary>
    /// 应用用户改动的配置。任何配置变化都先让当前负载结果失效，再重新装配检查与评估。
    /// 已交付后拒绝继续改配置，避免回执与草稿脱节。
    /// </summary>
    public CommissionSnapshot SetSetup(BenchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (_disposed || Model.Stage != CommissionStage.Configuring)
            return Snapshot;

        ApplySetup(setup, reset: false);
        if (Model.HasTestResult) _benchmark.MarkStale();
        Publish();
        return Snapshot;
    }

    /// <summary>恢复目录初始配置；同样会作废旧测试结果。</summary>
    public CommissionSnapshot ResetSetup()
    {
        if (_disposed || Model.Stage != CommissionStage.Configuring)
            return Snapshot;

        ResetToInitial();
        return Snapshot;
    }

    /// <summary>
    /// 对当前草稿运行整机负载测试。装配 BLOCKED 时不执行；测试前再次确保
    /// Fit 与当前草稿一致，测试后立即按同一草稿评估清单。
    /// </summary>
    public LoadResult? RunBenchmark()
    {
        if (_disposed || Model.Stage != CommissionStage.Configuring || Model.Draft is not { } setup)
            return null;

        FitResult fit = Model.Fit ?? _rules.Inspect(setup);
        // 负载规则只接受前置 360 整机；配置不满足时返回空结果，避免规则脚本抛错。
        if (!setup.Front360 || string.Equals(fit.Status, "BLOCKED", StringComparison.Ordinal))
            return null;

        LoadResult test = _rules.TestLoad(setup, setup.PsuIndex);
        CommissionChecklist checklist = CommissionOrder.Evaluate(_order, _rules.Catalog, setup, fit, test);
        Model.ApplyTest(fit, test, checklist);
        _benchmark.ApplyLoad(test);
        Publish();
        return test;
    }

    /// <summary>
    /// 交付：仅在验收清单全部通过且测试属于当前配置时写入回执。
    /// 已有回执（内存或磁盘）直接视为交付成功，绝不覆盖；写入失败转成可渲染错误。
    /// </summary>
    public bool TryDeliver()
    {
        if (_disposed || Model.Stage == CommissionStage.Brief) return false;

        if (Model.Stage == CommissionStage.Delivered && Model.Receipt is not null)
            return true;

        if (Model.Receipt is { } existing)
        {
            Model.ApplyDelivered(existing);
            Publish();
            return true;
        }

        if (Model.Draft is not { } setup || Model.Fit is not { } fit || Model.Test is not { } test)
        {
            FailDelivery("委托验收条件尚未全部满足，不能交付。");
            return false;
        }

        // 当前测试只能来自最近一次 SetSetup 之后的 RunBenchmark；Evaluate 再核对报告价格/工况，
        // 防止过期测试或换配置后仍被用于交付。
        CommissionChecklist checklist = CommissionOrder.Evaluate(_order, _rules.Catalog, setup, fit, test);
        if (!checklist.CanDeliver)
        {
            FailDelivery("委托验收条件尚未全部满足，不能交付。");
            return false;
        }

        try
        {
            CommissionReceipt receipt = CommissionOrder.CreateReceipt(_order, _rules.Catalog, setup, fit, test);
            Model.ApplyDelivered(_store.Save(receipt));
            Publish();
            return true;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            AppLog.Warning($"保存委托记录失败：{exception.Message}", "Commission");
            FailDelivery(StorageErrorMessage(exception, "交付记录保存失败，请重试。"));
            return false;
        }
    }

    /// <summary>释放功能持有的规则引擎；事件句柄由基类按 caller 清理。</summary>
    internal void Close()
    {
        if (_disposed)
            return;

        _disposed = true;
        _rules.Dispose();
        base.Dispose();
        Model.Dispose();
    }

    private void ResetToInitial()
    {
        ApplySetup(_rules.Catalog.Initial, reset: true);
        _benchmark.InvalidateCurrent();
        Publish();
    }

    private void ApplySetup(BenchSetup setup, bool reset)
    {
        setup = setup with
        {
            Front360 = _order.RequiresFront360 || setup.Front360,
            WorkloadIndex = _order.WorkloadIndex,
            Offset = 0,
            Slots = [.. setup.Slots],
        };
        FitResult fit = _rules.Inspect(setup);
        CommissionChecklist checklist = CommissionOrder.Evaluate(_order, _rules.Catalog, setup, fit, null);
        Model.ApplySetup(setup, fit, checklist, reset);
    }

    private void FailDelivery(string message)
    {
        Model.ApplyDeliveryError(message);
        Publish();
    }

    private void Publish() => InternalBus.Send(CommissionEvents.StateChanged, Snapshot);

    private static bool IsStorageError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;

    /// <summary>
    /// 持久层给出面向玩家的存档说明（版本过新、主文件无法留档等）时直接展示；
    /// 其余存储错误沿用场景默认文案，避免把系统异常原文抛给玩家。
    /// </summary>
    private static string StorageErrorMessage(Exception exception, string fallback) =>
        exception is InvalidDataException ? exception.Message : fallback;
}
