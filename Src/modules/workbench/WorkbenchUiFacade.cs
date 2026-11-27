using ComputerSimulator.Modules.Benchmark;
using ComputerSimulator.Modules.Commission;

namespace ComputerSimulator.Modules.Workbench;

/// <summary>
/// 工作台界面的归属门面。工作台有两种会话（装配 / 委托），而 UI 路由只在启动期注册一次，
/// 因此由组合根在打开路由前写入本次会话的控制器，界面统一经 <c>Facade&lt;WorkbenchUiFacade&gt;()</c> 取用。
/// </summary>
internal sealed class WorkbenchUiFacade
{
    /// <summary>与 Model 的 id 保持一致；路由归属校验也读取这个值。</summary>
    internal const string ModId = "workbench";

    /// <summary>非委托模式的装配控制器；委托模式为 <c>null</c>。</summary>
    internal WorkbenchController? Assembly { get; set; }

    /// <summary>委托模式的控制器；装配模式为 <c>null</c>。</summary>
    internal CommissionController? Commission { get; set; }

    /// <summary>与当前会话配套的负载报告控制器。</summary>
    internal BenchmarkController? Report { get; set; }

    public bool CommissionMode => Commission is not null;
}
