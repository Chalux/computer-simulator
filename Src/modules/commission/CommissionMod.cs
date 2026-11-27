using ComputerSimulator.Modules.Hardware;
using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Commission;

/// <summary>委托流程阶段：订单简介、装配配置、已交付。</summary>
public enum CommissionStage
{
    Brief,
    Configuring,
    Delivered,
}

/// <summary>
/// 控制器对外发布的只读状态快照。视图只读取本对象，不直接访问 <see cref="CommissionMod"/>。
/// </summary>
public sealed record CommissionSnapshot(
    CommissionStage Stage,
    CommissionReceipt? Receipt,
    string? BriefError,
    BenchSetup? Draft,
    FitResult? Fit,
    LoadResult? Test,
    bool HasTestResult,
    CommissionChecklist? Checklist,
    string? DeliveryError)
{
    public bool HasReceipt => Receipt is not null;

    public bool CanDeliver => Stage != CommissionStage.Delivered && Checklist?.CanDeliver == true;
}

/// <summary>
/// 委托 01-01 的功能内 Model：只保存订单回执、当前草稿、装配结果、负载结果、验收清单与错误。
/// 全部写入入口均为 <see cref="CommissionController"/> 调用的 internal 方法，自身不读文件、不跑规则。
/// </summary>
public sealed class CommissionMod : BaseMod
{
    public const string FeatureId = "commission";

    public CommissionMod() : base(FeatureId)
    {
    }

    public CommissionStage Stage { get; private set; } = CommissionStage.Brief;
    public CommissionReceipt? Receipt { get; private set; }
    public string? BriefError { get; private set; }
    public BenchSetup? Draft { get; private set; }
    public FitResult? Fit { get; private set; }
    public LoadResult? Test { get; private set; }
    public bool HasTestResult { get; private set; }
    public CommissionChecklist? Checklist { get; private set; }
    public string? DeliveryError { get; private set; }

    public CommissionSnapshot CreateSnapshot() =>
        new(Stage, Receipt, BriefError, Draft is null ? null : Draft with { Slots = [.. Draft.Slots] },
            Fit, Test, HasTestResult, Checklist, DeliveryError);

    /// <summary>打开委托简介时刷新回执与读取错误；已有有效回执即视为已交付。</summary>
    internal void ApplyReceipt(CommissionReceipt? receipt, string? error)
    {
        Receipt = receipt;
        BriefError = error;
        Draft = null;
        Fit = null;
        Test = null;
        HasTestResult = false;
        Checklist = null;
        DeliveryError = null;
        Stage = receipt is null ? CommissionStage.Brief : CommissionStage.Delivered;
    }

    /// <summary>应用新草稿与装配结果；任何配置变化都会让旧负载结果失效。</summary>
    internal void ApplySetup(BenchSetup setup, FitResult fit, CommissionChecklist checklist, bool reset)
    {
        Draft = setup;
        Fit = fit;
        Test = null;
        if (reset) HasTestResult = false;
        Checklist = checklist;
        DeliveryError = null;
        Stage = CommissionStage.Configuring;
    }

    /// <summary>应用当前草稿的新负载结果与对应验收清单。</summary>
    internal void ApplyTest(FitResult fit, LoadResult test, CommissionChecklist checklist)
    {
        Fit = fit;
        Test = test;
        HasTestResult = test.Report is not null;
        Checklist = checklist;
        DeliveryError = null;
        Stage = CommissionStage.Configuring;
    }

    internal void ApplyDelivered(CommissionReceipt receipt)
    {
        Receipt = receipt;
        DeliveryError = null;
        Stage = CommissionStage.Delivered;
    }

    internal void ApplyDeliveryError(string error) => DeliveryError = error;
}
