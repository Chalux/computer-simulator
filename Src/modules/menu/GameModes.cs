using KemoCard.Frame.Content.Simulator;

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

/// <summary>模式的展示资料，含当前开放状态；数据源为 content/game_modes。</summary>
public sealed record GameModeInfo(
    GameMode Mode,
    string Index,
    string Title,
    string Code,
    string Summary,
    bool Available);

/// <summary>
/// 将内容包的模式定义转换为菜单的路由及展示数据。
/// 只保留 <see cref="GameMode"/> 中存在的路由标识；未知、纯数字（枚举底层值）或
/// 重复的标识会被跳过，避免内容包把菜单项指向不存在的路由。
/// </summary>
public static class GameModeCatalog
{
    internal static IReadOnlyList<GameModeInfo> Parse(IReadOnlyList<GameModeDefinitionDto> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var supported = new Dictionary<string, GameMode>(StringComparer.OrdinalIgnoreCase);
        foreach (GameMode mode in Enum.GetValues<GameMode>())
        {
            supported[Enum.GetName(mode)!] = mode;
        }

        var modes = new List<GameModeInfo>(definitions.Count);
        var seen = new HashSet<GameMode>();
        foreach (var definition in definitions)
        {
            string identifier = (definition.Mode ?? string.Empty).Trim();
            if (!supported.TryGetValue(identifier, out GameMode mode) || !seen.Add(mode))
            {
                continue;
            }

            // 内容包可能为尚未实现的路线声明 Available=true；只有可玩路线才对外显示为开放，
            // 保证标签与试玩提示一致。
            bool playable = mode is GameMode.Commission
                or GameMode.MysteryCrate
                or GameMode.Sandbox
                or GameMode.Diagnosis;

            modes.Add(new GameModeInfo(
                mode,
                definition.Index,
                definition.Title,
                definition.Code,
                definition.Summary,
                definition.Available && playable));
        }

        return modes.AsReadOnly();
    }
}
