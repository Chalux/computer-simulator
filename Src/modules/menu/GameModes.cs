namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 顶层玩法模式标识。菜单路由只比较枚举，不依赖按钮顺序或显示文本。
/// </summary>
public enum GameMode
{
    Commission,
    Marketplace,
    MysteryCrate,
    Diagnosis,
    EraScenario,
    Sandbox,
}

/// <summary>模式的静态展示资料。当前版本只有沙盒开放，其余五个走简报页。</summary>
public sealed record GameModeInfo(
    GameMode Mode,
    string Index,
    string Title,
    string Code,
    string Summary,
    bool Available);

public static class GameModeCatalog
{
    public static readonly IReadOnlyList<GameModeInfo> All =
    [
        new(GameMode.Commission, "01", "委托挑战", "COMMISSION",
            "接受客户委托，在预算、用途与验收标准之间取舍，交出一台能通过验收的整机。", false),
        new(GameMode.Marketplace, "02", "二手市场", "MARKET",
            "按周期与成色选购二手配件，在成本与风险之间权衡，凑出一台可用的整机。", false),
        new(GameMode.MysteryCrate, "03", "神秘箱", "MYSTERY CRATE",
            "锁定商店，只能用给定的一批混合配件，拼出一台能点亮且综合评分更高的整机。", false),
        new(GameMode.Diagnosis, "04", "故障诊断", "DIAGNOSTICS",
            "只凭症状与日志定位故障，在有限的替换件中找出真正的问题所在。", false),
        new(GameMode.EraScenario, "05", "时代剧本", "ERA SCENARIO",
            "回到指定年代，用当年的配件与预算，还原一台符合时代气质的机器。", false),
        new(GameMode.Sandbox, "06", "沙盒模式", "SANDBOX",
            "自由装配、诊断与整机负载测试，不受委托与预算限制。", true),
    ];

    public static GameModeInfo Get(GameMode mode)
    {
        foreach (GameModeInfo info in All)
        {
            if (info.Mode == mode) return info;
        }

        throw new ArgumentOutOfRangeException(nameof(mode), mode, "未登记的模式");
    }
}
