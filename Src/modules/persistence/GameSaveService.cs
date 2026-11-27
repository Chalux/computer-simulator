using System.Text;
using System.Text.Json;

namespace ComputerSimulator.Modules.Persistence;

/// <summary>
/// 生涯存档的唯一入口。所有玩法（委托回执、启用的模组等）共用 <see cref="DefaultDirectoryPath"/>
/// 下的一个 JSON 文件，按区段读写；写入采用"临时文件 + 旧文件转备份 + 原子提升"的顺序，
/// 读取时若主文件损坏则归档并回退到备份。结构版本高于本程序的存档保持原样、拒绝读写，
/// 避免降级运行时静默归档并覆盖更新版本的存档。目录由 Root 注入（<c>user://saves</c>）。
/// </summary>
internal sealed class GameSaveService
{
    /// <summary>Root 约定的存档目录；委托回执仓库的兼容构造也使用它。</summary>
    public const string DefaultDirectoryPath = "user://saves";

    /// <summary>当前存档结构版本。</summary>
    internal const int CurrentSchemaVersion = 1;

    private const string SaveFileName = "career.json";
    private const int IoAttempts = 4;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly object _gate = new();
    private readonly string _directory;
    private CareerSaveDocument? _document;

    /// <summary>主文件结构版本高于本程序时记录该版本；此时只读空生涯并拒绝区段读写。</summary>
    private int? _newerSchemaVersion;

    /// <summary>主文件损坏且加载时无法留档；后续写入必须先重新归档，绝不能经备份位覆盖好备份。</summary>
    private bool _primaryCorruptUnarchived;

    /// <param name="directoryPath">
    /// 存档目录，支持 Godot 资源路径（<c>user://saves</c>）或普通文件系统路径。
    /// 目录在首次读写时创建，构造本身不触盘，便于 Root 在启动阶段注入。
    /// </param>
    public GameSaveService(string directoryPath)
    {
        _directory = ResolveDirectory(directoryPath);
        SavePath = Path.Combine(_directory, SaveFileName);
        BackupPath = SavePath + ".bak";
    }

    /// <summary>主存档文件路径（仅用于诊断，不供玩法直接读写）。</summary>
    public string SavePath { get; }

    /// <summary>上一次成功存档的备份路径。</summary>
    public string BackupPath { get; }

    /// <summary>
    /// 本次生涯启用的模组 ID；存档没有该字段时为 <c>null</c>。
    /// 存档来自更新版本时同样为 <c>null</c>，让 Root 能用默认内容启动菜单。
    /// 启动阶段存档暂时不可读时也返回 <c>null</c>，不阻断菜单；区段读写仍会抛出存储错误。
    /// </summary>
    public IReadOnlyList<string>? EnabledModIds
    {
        get
        {
            lock (_gate)
            {
                try
                {
                    EnsureLoaded();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // 不缓存失败结果，后续区段访问会再次尝试加载并给出明确错误。
                    return null;
                }

                return _document!.EnabledModIds;
            }
        }
    }

    /// <summary>
    /// 读取指定区段并反序列化为 <typeparamref name="T"/>；区段不存在时返回 <c>null</c>。
    /// 区段内容损坏或存档来自更新版本时抛出 <see cref="InvalidDataException"/>，
    /// 由调用方转成可渲染的读取错误。
    /// </summary>
    internal T? ReadSection<T>(string sectionKey) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionKey);

        lock (_gate)
        {
            EnsureLoaded();
            ThrowIfNewerSchema();
            if (!_document!.Sections!.TryGetValue(sectionKey, out JsonElement element))
                return null;

            try
            {
                return element.Deserialize<T>(JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"生涯存档区段「{sectionKey}」无法读取。", exception);
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidDataException($"生涯存档区段「{sectionKey}」类型不受支持。", exception);
            }
        }
    }

    /// <summary>
    /// 覆盖写入指定区段。先写临时文件并落盘，再把旧主文件转为备份、最后原子提升临时文件；
    /// 任一步失败都不改动内存中的已加载状态。存档来自更新版本时拒绝写入。
    /// </summary>
    internal void WriteSection<T>(string sectionKey, T value) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionKey);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            EnsureLoaded();
            ThrowIfNewerSchema();

            var sections = new Dictionary<string, JsonElement>(_document!.Sections!, StringComparer.Ordinal)
            {
                [sectionKey] = JsonSerializer.SerializeToElement(value, JsonOptions),
            };
            var next = new CareerSaveDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                EnabledModIds = _document.EnabledModIds,
                Sections = sections,
            };

            PersistWithRetry(next);
            _document = next;
        }
    }

    private void EnsureLoaded()
    {
        if (_document is not null) return;

        // 加载失败时不缓存文档，下次访问可重试；损坏文件已在加载过程中归档。
        _document = LoadDocument();
    }

    /// <summary>更新版本的存档只允许保持原样：任何区段读写都必须显式失败。</summary>
    private void ThrowIfNewerSchema()
    {
        if (_newerSchemaVersion is not int version)
            return;

        throw new InvalidDataException(
            $"生涯存档由更新版本的游戏创建（结构版本 {version}），当前版本无法读写。请升级游戏后重试。");
    }

    private CareerSaveDocument LoadDocument()
    {
        // 加载可能因读写失败被整体重试，状态必须以本次尝试为准，避免沿用上一次的结论。
        _newerSchemaVersion = null;
        _primaryCorruptUnarchived = false;

        Directory.CreateDirectory(_directory);

        string? primary = TryReadText(SavePath);
        if (primary is not null)
        {
            if (TryParse(primary, out CareerSaveDocument primaryDocument, out int primaryVersion))
                return primaryDocument;

            if (primaryVersion > CurrentSchemaVersion)
            {
                // 更新版本写入的存档：保留原文件，只读空生涯让菜单可用；
                // 区段读写抛可读错误，绝不归档或覆盖。
                _newerSchemaVersion = primaryVersion;
                return CareerSaveDocument.Empty();
            }

            if (!ArchiveCorrupt(SavePath))
                _primaryCorruptUnarchived = true;
        }

        string? backup = TryReadText(BackupPath);
        if (backup is not null)
        {
            if (TryParse(backup, out CareerSaveDocument backupDocument, out int backupVersion))
                return backupDocument;

            if (backupVersion > CurrentSchemaVersion)
            {
                _newerSchemaVersion = backupVersion;
                return CareerSaveDocument.Empty();
            }

            ArchiveCorrupt(BackupPath);
        }

        // 主文件与备份都不可用时从空生涯继续；损坏内容已尽量归档留档，
        // 不让一份坏存档阻断 Root 读取 EnabledModIds 或首次交付。
        return CareerSaveDocument.Empty();
    }

    /// <summary>
    /// 解析整份存档；结构版本不符或 JSON 损坏都视为不可用，交给调用方归档。
    /// <paramref name="schemaVersion"/> 总会尽力读出，供调用方区分"版本过新"与"文件损坏"。
    /// </summary>
    private static bool TryParse(string json, out CareerSaveDocument document, out int schemaVersion)
    {
        document = null!;
        schemaVersion = 0;

        try
        {
            // 先只读版本号再反序列化整份文档：更新版本的区段结构可能已超出当前 DTO，
            // 不能因为反序列化失败就把一份完好的新版本存档当成损坏文件归档。
            using (JsonDocument raw = JsonDocument.Parse(json))
            {
                if (!TryGetSchemaVersion(raw.RootElement, out schemaVersion)
                    || schemaVersion != CurrentSchemaVersion)
                {
                    return false;
                }
            }

            // 区段要长期留在内存里，必须从原始字符串反序列化，不能借用已释放的 JsonDocument。
            CareerSaveDocument? parsed = JsonSerializer.Deserialize<CareerSaveDocument>(json, JsonOptions);
            if (parsed is null || parsed.SchemaVersion != schemaVersion)
                return false;

            parsed.Sections ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            document = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryGetSchemaVersion(JsonElement root, out int version)
    {
        version = 0;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        bool found = false;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, "schemaVersion", StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out version))
            {
                return false;
            }

            found = true;
        }

        return found;
    }

    private void PersistWithRetry(CareerSaveDocument document)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Persist(document);
                return;
            }
            catch (IOException) when (attempt < IoAttempts - 1)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private void Persist(CareerSaveDocument document)
    {
        document.SchemaVersion = CurrentSchemaVersion;
        byte[] payload = Utf8NoBom.GetBytes(JsonSerializer.Serialize(document, JsonOptions));
        string tempPath = Path.Combine(_directory, $".{SaveFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(_directory);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(SavePath))
            {
                // 加载时主文件已判定损坏却未能留档：此时直接 File.Replace 会把损坏的主文件
                // 顶进备份位，覆盖唯一可用的备份。先重试归档，失败就中止本次写入。
                if (_primaryCorruptUnarchived)
                {
                    if (!ArchiveCorrupt(SavePath))
                    {
                        throw new InvalidDataException(
                            "生涯存档主文件已损坏且无法留档；为避免覆盖可用备份，本次写入已取消。请检查存档目录权限后重试。");
                    }

                    _primaryCorruptUnarchived = false;
                }
                else
                {
                    File.Replace(tempPath, SavePath, BackupPath, ignoreMetadataErrors: true);
                    return;
                }
            }

            File.Move(tempPath, SavePath, overwrite: false);
            _primaryCorruptUnarchived = false;
        }
        finally
        {
            DeleteTemp(tempPath);
        }
    }

    private static string? TryReadText(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException exception) when (
                exception is not (FileNotFoundException or DirectoryNotFoundException) && attempt < IoAttempts - 1)
            {
                Thread.Sleep(20 * (attempt + 1));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 把损坏文件改名留档，避免下次写入把它当成有效备份；归档失败时保留原文件并返回 <c>false</c>，
    /// 由调用方决定是否继续回退读取或中止写入。
    /// </summary>
    private bool ArchiveCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path)) return true;

            string archivePath = Path.Combine(
                _directory,
                $"{Path.GetFileNameWithoutExtension(path)}.{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}." +
                $"{Guid.NewGuid():N}.corrupt{Path.GetExtension(path)}");
            File.Move(path, archivePath, overwrite: false);
            return true;
        }
        catch (IOException)
        {
            // 留档失败时保留原文件，读取仍继续尝试备份。
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // 同上。
            return false;
        }
    }

    private static string ResolveDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        string localized = directoryPath.Contains("://", StringComparison.Ordinal)
            ? Godot.ProjectSettings.GlobalizePath(directoryPath)
            : directoryPath;
        return Path.GetFullPath(localized);
    }

    private static void DeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // 清理临时文件失败不掩盖原始写入异常。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上。
        }
    }
}
