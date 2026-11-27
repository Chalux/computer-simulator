using System.Globalization;
using System.Text;

namespace KemoCard.Frame.Content;

public sealed class GameDefinitionRegistry
{
    /// <summary>FNV-1a 64 位偏移基数。</summary>
    private const ulong FnvOffsetBasis = 14695981039346656037UL;

    /// <summary>FNV-1a 64 位质数。</summary>
    private const ulong FnvPrime = 1099511628211UL;

    private readonly object _gate = new();
    private readonly Dictionary<(EContentCategory Category, string Id), string> _ownerModIds = new();

    public GameDefinitionStore Store { get; } = new();

    public int DefinitionVersion { get; private set; }

    /// <summary>
    /// 当前生效定义集的内容版本指纹（16 位小写十六进制，FNV-1a 64 位）。
    /// 载荷按 <see cref="EContentCategory"/> 声明顺序逐类别写出「类别=数量\n」，随后每行一个该类别按
    /// 序数排序的定义 id，因此与 Mod 注册顺序、字典枚举顺序无关；只取 id，与 DTO 内容无关。
    /// 必须使用稳定哈希：<c>string.GetHashCode()</c> / <c>HashCode.Combine</c> 的字符串哈希带
    /// 进程级随机种子，跨会话不可比较，而本指纹要写进全局存档用于检测内容变更
    /// （内容 Mod 规格 §5：与 <see cref="DefinitionVersion"/> 联动）。
    /// 未执行过 <see cref="Rebuild"/> 时为空定义集的指纹。
    /// </summary>
    public string ContentVersionHash { get; private set; } =
        ComputeFnv1a64Hex(BuildFingerprintPayload(static _ => Array.Empty<string>()));

    public void Rebuild(IReadOnlyList<ModContentBundle> bundles, out ContentLoadReport report)
    {
        ArgumentNullException.ThrowIfNull(bundles);

        lock (_gate)
        {
            var merger = new ContentRegistryMerger();
            var conflicts = new List<ContentIdConflictEntry>();
            var ownerById = new Dictionary<(EContentCategory Category, string Id), string>();

            Store.Clear();
            merger.MergeInto(bundles, Store, ownerById, conflicts, excludedModIds: null);

            var removedValidationErrors = RemoveInvalidDefinitions(bundles, merger, ownerById);

            if (removedValidationErrors.Count != 0)
            {
                // 合并时记录的是首选定义。回退后按实际归属报告，避免日志把被拒绝的 Mod 标为胜出者。
                conflicts = conflicts.GroupBy(entry => (entry.Category, entry.ContentId))
                    .Where(group => ownerById.ContainsKey(group.Key))
                    .SelectMany(group =>
                    {
                        string winner = ownerById[group.Key];
                        return group.Select(entry => entry.LoserModId)
                            .Prepend(group.First().WinnerModId)
                            .Distinct(StringComparer.Ordinal)
                            .Where(id => id != winner)
                            .Select(id => new ContentIdConflictEntry(group.Key.Category, group.Key.ContentId, winner, id));
                    }).ToList();
            }

            _ownerModIds.Clear();
            foreach (var (key, modId) in ownerById)
            {
                _ownerModIds[key] = modId;
            }

            DefinitionVersion++;
            ContentVersionHash = ComputeFnv1a64Hex(BuildFingerprintPayload(CollectSortedIds));
            report = new ContentLoadReport(
                Array.Empty<ModSkipEntry>(),
                conflicts,
                Array.Empty<ContentDefinitionValidationError>(),
                Array.Empty<ScriptLoadError>(),
                removedValidationErrors);
        }
    }

    public bool Contains(EContentCategory category, string id)
    {
        lock (_gate)
        {
            return Store.Contains(category, id);
        }
    }

    public bool TryGetOwnerModId(EContentCategory category, string id, out string modId)
    {
        lock (_gate)
        {
            return _ownerModIds.TryGetValue((category, id), out modId!);
        }
    }

    /// <summary>
    /// 按内容类别取当前生效定义集的 id，并按序数排序；排序保证指纹与注册顺序无关。
    /// </summary>
    private IReadOnlyList<string> CollectSortedIds(EContentCategory category)
    {
        IEnumerable<string> ids = category switch
        {
            EContentCategory.Character => Store.Characters.Keys,
            EContentCategory.Enemy => Store.Enemies.Keys,
            EContentCategory.Battle => Store.Battles.Keys,
            EContentCategory.Event => Store.Events.Keys,
            EContentCategory.Card => Store.Cards.Keys,
            EContentCategory.Item => Store.Items.Keys,
            EContentCategory.Skill => Store.Skills.Keys,
            EContentCategory.Buff => Store.Buffs.Keys,
            EContentCategory.Effect => Store.Effects.Keys,
            EContentCategory.SkillAction => Store.SkillActions.Keys,
            EContentCategory.Attribute => Store.Attributes.Keys,
            EContentCategory.GameplayEffect => Store.GameplayEffects.Keys,
            EContentCategory.GameplayTag => Store.GameplayTags.Keys,
            EContentCategory.Story => Store.Stories.Keys,
            EContentCategory.OrbType => Store.OrbTypes.Keys,
            EContentCategory.SimCase => Store.Cases.Keys,
            EContentCategory.SimMotherboard => Store.Motherboards.Keys,
            EContentCategory.SimProcessor => Store.Processors.Keys,
            EContentCategory.SimGraphicsCard => Store.GraphicsCards.Keys,
            EContentCategory.SimMemory => Store.MemoryModules.Keys,
            EContentCategory.SimStorage => Store.StorageDevices.Keys,
            EContentCategory.SimCooler => Store.Coolers.Keys,
            EContentCategory.SimPowerSupply => Store.PowerSupplies.Keys,
            EContentCategory.SimCommission => Store.Commissions.Keys,
            EContentCategory.SimDiagnosisCase => Store.DiagnosisCases.Keys,
            EContentCategory.SimBenchmark => Store.Benchmarks.Keys,
            EContentCategory.SimGameMode => Store.GameModes.Keys,
            EContentCategory.SimChallenge => Store.Challenges.Keys,
            _ => Array.Empty<string>(),
        };

        var sorted = ids.ToList();
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    /// <summary>
    /// 拼接内容指纹载荷：按 <see cref="EContentCategory"/> 声明顺序逐类别一行「类别=数量」，
    /// 随后每行一个该类别已排序的定义 id。
    /// </summary>
    private static string BuildFingerprintPayload(Func<EContentCategory, IReadOnlyList<string>> idsByCategory)
    {
        var payload = new StringBuilder();
        foreach (var category in Enum.GetValues<EContentCategory>())
        {
            var ids = idsByCategory(category);
            payload.Append(category.ToString()).Append('=').Append(ids.Count).Append('\n');
            foreach (var id in ids)
            {
                payload.Append(id).Append('\n');
            }
        }

        return payload.ToString();
    }

    /// <summary>
    /// FNV-1a 64 位哈希（输入按 UTF-8 取字节），返回 16 位小写十六进制字符串。
    /// </summary>
    private static string ComputeFnv1a64Hex(string payload)
    {
        var hash = FnvOffsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(payload))
        {
            hash = unchecked((hash ^ value) * FnvPrime);
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 反复「校验 → 移除非法定义 → 重新合并」直到稳定。重新合并会跳过被移除 Mod 的那条定义，
    /// 让低优先级的同名定义（例如 basemod 的原始定义）回退生效；若回退定义同样非法，
    /// 下一轮继续移除并回退到更低的来源。
    /// </summary>
    private List<ContentDefinitionValidationError> RemoveInvalidDefinitions(
        IReadOnlyList<ModContentBundle> bundles,
        ContentRegistryMerger merger,
        Dictionary<(EContentCategory Category, string Id), string> ownerById)
    {
        var validator = new ContentDefinitionValidator();
        var removed = new List<ContentDefinitionValidationError>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        var excludedModIds = new Dictionary<(EContentCategory Category, string Id), HashSet<string>>();

        while (true)
        {
            var errors = validator.Validate(Store);
            if (errors.Count == 0)
            {
                break;
            }

            // 下游此时可能只因上游的坏覆盖报错。让上游先回退，再决定是否真的要移除下游。
            int phase = errors.Where(error => Store.Contains(error.Category, error.DefinitionId))
                .Select(error => ValidationPhase(error.Category)).DefaultIfEmpty(0).Min();
            var changed = false;
            foreach (var error in errors)
            {
                if (error.DefinitionId == "*")
                {
                    Report(error);
                    continue;
                }

                if (!Store.Contains(error.Category, error.DefinitionId))
                {
                    Report(error);
                    continue;
                }

                if (ValidationPhase(error.Category) != phase) continue;

                var key = (error.Category, error.DefinitionId);
                Store.Remove(error.Category, error.DefinitionId);

                if (!excludedModIds.TryGetValue(key, out var excluded))
                {
                    excluded = new HashSet<string>(StringComparer.Ordinal);
                    excludedModIds[key] = excluded;
                }

                if (ownerById.Remove(key, out var ownerModId) && !string.IsNullOrWhiteSpace(ownerModId))
                {
                    excluded.Add(ownerModId);
                }
                else
                {
                    // 理论上不会出现：没有任何 Mod 归属的定义无法定位来源，禁止所有 Mod 再提交同名 id。
                    foreach (var bundle in bundles)
                    {
                        excluded.Add(bundle.ModId);
                    }
                }

                Report(error);
                changed = true;
            }

            if (!changed)
            {
                break;
            }

            merger.MergeInto(bundles, Store, ownerById, conflicts: null, excludedModIds);
        }

        return removed;

        void Report(ContentDefinitionValidationError error)
        {
            var fingerprint = $"{error.Category}\u001f{error.DefinitionId}\u001f{error.Message}";
            if (reported.Add(fingerprint))
            {
                removed.Add(error);
            }
        }
    }

    private static int ValidationPhase(EContentCategory category) => category switch
    {
        EContentCategory.SimBenchmark => 1,
        EContentCategory.SimCommission or EContentCategory.SimDiagnosisCase or EContentCategory.SimChallenge => 2,
        _ => 0,
    };
}
