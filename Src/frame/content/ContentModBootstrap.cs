using System.Text.Json;

namespace KemoCard.Frame.Content;

public static class ContentModBootstrap
{
    private const string MarkerFileName = ".content-mod-origin";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 把随包分发的 Mod 拷贝到用户可写目录。<b>版本号相同即跳过</b>（发布语义：不每次启动都重拷）。
    /// </summary>
    /// <param name="forceRefresh">
    /// 调试构建用：忽略版本号，总是重新拷贝。开发期改内容（新增角色/卡牌/翻译……）常常忘了抬
    /// <c>mod.json</c> 版本号，导致游戏里看不到——打开它即可让每次启动都同步最新内容。
    /// 由调用方注入（<c>ModStartupContext.ForceContentModRefresh</c>），本类<b>不</b>直接读 Godot 的构建标记，
    /// 以便逻辑层测试不依赖引擎。
    /// </param>
    /// <param name="sourceFileSystem">
    /// 随包来源文件系统。导出包里的 <c>res://mods</c> 位于 PCK，必须用
    /// <c>GodotContentModFileSystem</c>；普通目录可省略。
    /// </param>
    /// <param name="preserveExisting">
    /// 目标目录存在但不是本工具写入的（没有来源标记）时跳过。默认开启，
    /// 避免启动时把第三方安装 / 玩家手改的 Mod 覆盖掉。
    /// </param>
    public static void EnsureDefaultModsCopied(
        string modRootDirectory,
        string bundledModsSourceDirectory,
        bool forceRefresh = false,
        IContentModFileSystem? sourceFileSystem = null,
        bool preserveExisting = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundledModsSourceDirectory);

        var fileSystem = sourceFileSystem ?? SystemContentModFileSystem.Instance;
        if (!fileSystem.DirectoryExists(bundledModsSourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(modRootDirectory);
        foreach (var sourceDir in fileSystem
                     .EnumerateDirectories(bundledModsSourceDirectory)
                     .OrderBy(static p => p, StringComparer.Ordinal))
        {
            var folderName = ContentModPath.GetFileName(sourceDir);
            var destDir = Path.Combine(modRootDirectory, folderName);
            EnsureModCopied(fileSystem, sourceDir, destDir, forceRefresh, preserveExisting);
        }
    }

    private static void EnsureModCopied(
        IContentModFileSystem fileSystem,
        string sourceDir,
        string destDir,
        bool forceRefresh,
        bool preserveExisting)
    {
        if (!TryReadManifestVersion(fileSystem, sourceDir, out var bundledModId, out var bundledVersion))
        {
            return;
        }

        if (Directory.Exists(destDir))
        {
            if (preserveExisting && !HasOriginMarker(destDir, bundledModId))
            {
                // 目标不是上一次由本工具安装的：视作第三方安装，绝不覆盖。
                return;
            }

            if (!forceRefresh &&
                TryReadManifestVersion(SystemContentModFileSystem.Instance, destDir, out _, out var installedVersion) &&
                string.Equals(installedVersion, bundledVersion, StringComparison.Ordinal))
            {
                return;
            }

            DeleteDirectoryIfExists(destDir);
        }

        var stagingDir = destDir + ".staging";
        DeleteDirectoryIfExists(stagingDir);

        try
        {
            CopyDirectoryRecursive(fileSystem, sourceDir, stagingDir);
            File.WriteAllText(Path.Combine(stagingDir, MarkerFileName), $"{bundledModId}\n{bundledVersion}\n");
            if (Directory.Exists(destDir))
            {
                DeleteDirectoryIfExists(destDir);
            }

            Directory.Move(stagingDir, destDir);
        }
        catch
        {
            DeleteDirectoryIfExists(stagingDir);
            throw;
        }
    }

    private static bool HasOriginMarker(string directory, string modId)
    {
        var markerPath = Path.Combine(directory, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            return false;
        }

        try
        {
            var firstLine = File.ReadLines(markerPath).FirstOrDefault();
            return string.Equals(firstLine, modId, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryReadManifestVersion(
        IContentModFileSystem fileSystem,
        string modDir,
        out string modId,
        out string version)
    {
        modId = string.Empty;
        version = string.Empty;
        var manifestPath = ContentModPath.Combine(modDir, "mod.json");
        if (!fileSystem.FileExists(manifestPath))
        {
            return false;
        }

        try
        {
            var json = fileSystem.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ContentModManifestDto>(json, JsonOptions);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) ||
                string.IsNullOrWhiteSpace(manifest.ModId))
            {
                return false;
            }

            modId = manifest.ModId;
            version = manifest.Version;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void CopyDirectoryRecursive(
        IContentModFileSystem fileSystem,
        string sourceDir,
        string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in fileSystem.EnumerateFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, ContentModPath.GetFileName(file));
            File.WriteAllBytes(destFile, fileSystem.ReadAllBytes(file));
        }

        foreach (var subDir in fileSystem.EnumerateDirectories(sourceDir))
        {
            var destSub = Path.Combine(destDir, ContentModPath.GetFileName(subDir));
            CopyDirectoryRecursive(fileSystem, subDir, destSub);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
