using System.Text.Json;

namespace ComputerSimulator.Modules.Persistence;

/// <summary>
/// 生涯存档的磁盘结构。所有玩法共用一个文件，各自使用 <see cref="Sections"/> 下的独立键，
/// 由 <see cref="GameSaveService"/> 负责原子写入、备份与损坏归档。
/// </summary>
internal sealed class CareerSaveDocument
{
    /// <summary>当前存档结构版本；升级结构时必须保留旧版本的读取分支。</summary>
    public int SchemaVersion { get; set; }

    /// <summary>本次生涯启用的模组 ID；无记录时为 <c>null</c>（与"启用空列表"区分）。</summary>
    public string[]? EnabledModIds { get; set; }

    /// <summary>按玩法划分的持久化区段；值为该玩法自行定义的 JSON 快照。</summary>
    public Dictionary<string, JsonElement>? Sections { get; set; }

    public static CareerSaveDocument Empty() => new()
    {
        SchemaVersion = GameSaveService.CurrentSchemaVersion,
        Sections = new Dictionary<string, JsonElement>(StringComparer.Ordinal),
    };
}
