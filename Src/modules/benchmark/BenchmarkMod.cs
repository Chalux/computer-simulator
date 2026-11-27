using ComputerSimulator.Modules.Simulation;
using KemoCard.Frame.Mvc;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>
/// 负载报告功能的 Model：持有最近一次展示的报告、对照基准、当前有效结果及其失效原因。
/// 所有写入入口只由 <see cref="BenchmarkController"/> 调用，视图不得直接修改。
/// </summary>
internal sealed class BenchmarkMod : BaseMod
{
    internal const string ModIdValue = "benchmark";

    internal BenchmarkMod() : base(ModIdValue)
    {
    }

    /// <summary>沙盒报告启用对照；神秘箱保持原报告。</summary>
    internal bool ComparisonEnabled { get; private set; }

    /// <summary>最近一次展示的报告；配置变更后仍保留，用于显示“上次测试”。</summary>
    internal BenchmarkReport? Report { get; private set; }

    /// <summary>本次会话固定下来的对照基准。</summary>
    internal BenchmarkRunSnapshot? Baseline { get; private set; }

    /// <summary>当前有效结果；配置变更、测试失败或还原后为空。</summary>
    internal BenchmarkRunSnapshot? Current { get; private set; }

    /// <summary>当前结果是否仍与当前配置一致（复测通过后才为真）。</summary>
    internal bool CurrentFresh { get; private set; }

    /// <summary>报告是否因配置变更而标记为过期。</summary>
    internal bool IsStale { get; private set; }

    /// <summary>失效原因提示；由视图决定是否以警告色展示。</summary>
    internal string Notice { get; private set; } = string.Empty;

    internal void SetComparisonEnabled(bool enabled) => ComparisonEnabled = enabled;

    /// <summary>
    /// 记录一次展示。仅在对照启用且提供了运行身份时登记当前结果；
    /// 展示会清除过期标记与失效提示。
    /// </summary>
    internal void ApplyResult(BenchmarkReport report, BenchmarkRunIdentity? identity, bool trackComparison)
    {
        Report = report;
        IsStale = false;
        Notice = string.Empty;

        if (!trackComparison)
        {
            return;
        }

        Current = identity is null ? null : new BenchmarkRunSnapshot(report, identity);
        CurrentFresh = Current is not null;
    }

    /// <summary>配置变更：报告标记为过期，当前结果由 Controller 按对照开关决定是否作废。</summary>
    internal void MarkStale() => IsStale = true;

    /// <summary>作废当前结果；已保存的对照保留。</summary>
    internal void InvalidateCurrent(string notice)
    {
        Current = null;
        CurrentFresh = false;
        Notice = notice;
    }

    /// <summary>把当前有效结果固定为对照。</summary>
    internal void PinCurrentAsBaseline()
    {
        Baseline = Current;
        Notice = string.Empty;
    }

    /// <summary>清除本次会话保存的对照。</summary>
    internal void ClearBaseline()
    {
        Baseline = null;
        Notice = string.Empty;
    }
}
