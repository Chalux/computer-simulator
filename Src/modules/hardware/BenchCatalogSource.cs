namespace ComputerSimulator.Modules.Hardware;

/// <summary>
/// 已移除的静态目录入口。目录数据现在只来自 <see cref="GameContent.Load"/>；
/// 保留本类型仅为避免旧二进制 / 旧源码引用在编译期找不到符号，
/// 调用时会立即抛错，不再提供任何嵌入 JSON 兜底。
/// </summary>
internal static class BenchCatalogSource
{
    [Obsolete("装配硬件目录已迁移到内容 Mod（GameContent.Load）；不存在静态目录兜底。", true)]
    public static BenchCatalog Load() =>
        throw new InvalidOperationException("装配硬件目录必须通过 GameContent.Load 从内容 Mod 加载。");
}
