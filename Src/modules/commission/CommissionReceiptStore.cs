using System.Text;
using System.Text.Json;
using ComputerSimulator.Modules.Persistence;
using ProjectSettings = Godot.ProjectSettings;

namespace ComputerSimulator.Modules.Commission;

/// <summary>
/// 委托回执仓库。回执写入统一的生涯存档（<see cref="GameSaveService"/>）中的
/// <c>commissionReceipts</c> 区段，以订单 ID 为键各占一项，同一订单绝不重复覆盖。
/// 旧原型 <c>user://commission/01-01-receipt.json</c> 仅作为迁移来源读取，
/// 不修改、不删除。
/// </summary>
internal sealed class CommissionReceiptStore
{
    private const string SectionKey = "commissionReceipts";
    private const string LegacyReceiptResourcePath = "user://commission/01-01-receipt.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GameSaveService _saves;

    public CommissionReceiptStore(GameSaveService saves)
    {
        _saves = saves ?? throw new ArgumentNullException(nameof(saves));
    }

    /// <summary>
    /// 按订单 ID 读取已交付回执；统一存档中没有时尝试从旧原型文件迁移一次。
    /// 两者都不存在返回 <c>null</c>；存档损坏抛出 IO/JSON 异常供控制器提示/重试。
    /// </summary>
    public CommissionReceipt? Load(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Dictionary<string, CommissionReceipt> receipts = ReadAll();
        if (receipts.TryGetValue(id, out var stored))
        {
            ValidateReceipt(stored, id);
            return stored;
        }

        CommissionReceipt? legacy = TryReadLegacy(id);
        if (legacy is null)
            return null;

        // 迁移写入失败时仍返回旧回执：读取旧原型不应因为新存档只读而失败，
        // 旧文件保持原样，下次启动可重试导入。
        try
        {
            return Store(legacy);
        }
        catch (IOException)
        {
            return legacy;
        }
        catch (UnauthorizedAccessException)
        {
            return legacy;
        }
    }

    /// <summary>
    /// 写入回执并返回最终生效内容。同一订单已有有效回执时原样返回，绝不覆盖；
    /// 交付重复调用因而是幂等的。
    /// </summary>
    public CommissionReceipt Save(CommissionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ValidateReceipt(receipt, receipt.Id);
        return Store(receipt);
    }

    private CommissionReceipt Store(CommissionReceipt receipt)
    {
        Dictionary<string, CommissionReceipt> receipts = ReadAll();
        if (receipts.TryGetValue(receipt.Id, out var existing))
        {
            ValidateReceipt(existing, receipt.Id);
            return existing;
        }

        receipts[receipt.Id] = receipt;
        _saves.WriteSection(SectionKey, receipts);
        return receipt;
    }

    private Dictionary<string, CommissionReceipt> ReadAll()
    {
        Dictionary<string, CommissionReceipt>? receipts =
            _saves.ReadSection<Dictionary<string, CommissionReceipt>>(SectionKey);
        return receipts is null
            ? new Dictionary<string, CommissionReceipt>(StringComparer.Ordinal)
            : new Dictionary<string, CommissionReceipt>(receipts, StringComparer.Ordinal);
    }

    /// <summary>只读旧原型回执；文件缺失、损坏或不属于该订单时视为没有，迁移失败不影响统一存档。</summary>
    private static CommissionReceipt? TryReadLegacy(string id)
    {
        string path = ProjectSettings.GlobalizePath(LegacyReceiptResourcePath);
        string json;
        try
        {
            if (!File.Exists(path)) return null;
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            CommissionReceipt? receipt = JsonSerializer.Deserialize<CommissionReceipt>(json, JsonOptions);
            if (receipt is null || !string.Equals(receipt.Id, id, StringComparison.Ordinal))
                return null;

            ValidateReceipt(receipt, id);
            return receipt;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static void ValidateReceipt(CommissionReceipt? receipt, string expectedId)
    {
        if (receipt is null
            || string.IsNullOrWhiteSpace(receipt.Id)
            || !string.Equals(receipt.Id, expectedId, StringComparison.Ordinal)
            || receipt.Report is null
            || receipt.LoadResult is null
            || receipt.LoadResult.Report is null
            || receipt.MemorySlotNames is null
            || receipt.Setup is null
            || receipt.Setup.Slots is null)
        {
            throw new InvalidDataException("委托回执缺少必要字段或订单号不匹配。");
        }
    }
}
