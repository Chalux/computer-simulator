using ComputerSimulator.Modules.Simulation;

namespace ComputerSimulator.Modules.Benchmark;

/// <summary>一次成功负载测试时实际选择的配置身份，供沙盒对照。</summary>
public sealed record BenchmarkRunIdentity(string CaseName, string GpuName, string PsuName, string RamName, string Slots);

/// <summary>报告与其运行身份的不可变组合；同一实例同时充当“当前结果”和“对照”时表示本次结果已固定。</summary>
public sealed record BenchmarkRunSnapshot(BenchmarkReport Report, BenchmarkRunIdentity Identity);
