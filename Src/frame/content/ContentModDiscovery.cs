using System.Text.Json;

namespace KemoCard.Frame.Content;

public sealed class ContentModDiscovery
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IContentModFileSystem _fileSystem;

    public ContentModDiscovery()
        : this(SystemContentModFileSystem.Instance)
    {
    }

    public ContentModDiscovery(IContentModFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public ContentModDiscoveryResult Scan(string modRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modRootDirectory);
        return Scan([modRootDirectory]);
    }

    /// <summary>
    /// 按给定根目录顺序扫描。根目录顺序即冲突优先级：先扫描到的 Mod id 生效，
    /// 后续根目录中的同名 Mod 记为 <see cref="ModSkipReason.DuplicateModId"/>。
    /// 因此组合根把可写的 <c>user://mods</c> 放在只读的 <c>res://mods</c> 之前，
    /// 第三方安装可以整体替换默认 basemod，而不会被覆盖。
    /// </summary>
    public ContentModDiscoveryResult Scan(IReadOnlyList<string> modRootDirectories)
    {
        ArgumentNullException.ThrowIfNull(modRootDirectories);

        var validMods = new List<DiscoveredModEntry>();
        var skippedMods = new List<ModSkipEntry>();
        var seenModIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var modRootDirectory in modRootDirectories)
        {
            if (string.IsNullOrWhiteSpace(modRootDirectory) || !_fileSystem.DirectoryExists(modRootDirectory))
            {
                continue;
            }

            foreach (var folderPath in _fileSystem
                         .EnumerateDirectories(modRootDirectory)
                         .OrderBy(static p => p, StringComparer.Ordinal))
            {
                var folderName = ContentModPath.GetFileName(folderPath);
                var manifestPath = ContentModPath.Combine(folderPath, "mod.json");
                if (!_fileSystem.FileExists(manifestPath))
                {
                    skippedMods.Add(new ModSkipEntry(folderName, ModSkipReason.InvalidManifest, "mod.json missing"));
                    continue;
                }

                ContentModManifestDto? manifest;
                try
                {
                    var json = _fileSystem.ReadAllText(manifestPath);
                    manifest = JsonSerializer.Deserialize<ContentModManifestDto>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    skippedMods.Add(new ModSkipEntry(folderName, ModSkipReason.InvalidManifest, ex.Message));
                    continue;
                }
                catch (IOException ex)
                {
                    skippedMods.Add(new ModSkipEntry(folderName, ModSkipReason.InvalidManifest, ex.Message));
                    continue;
                }

                if (manifest is null || string.IsNullOrWhiteSpace(manifest.ModId))
                {
                    skippedMods.Add(new ModSkipEntry(folderName, ModSkipReason.InvalidManifest, "modId missing"));
                    continue;
                }

                if (!seenModIds.Add(manifest.ModId))
                {
                    skippedMods.Add(new ModSkipEntry(manifest.ModId, ModSkipReason.DuplicateModId, folderName));
                    continue;
                }

                validMods.Add(new DiscoveredModEntry(folderPath, manifest));
            }
        }

        return new ContentModDiscoveryResult(validMods, skippedMods);
    }
}
