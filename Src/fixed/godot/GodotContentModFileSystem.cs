using System.Text;
using Godot;
using KemoCard.Frame.Content;
using DirAccess = global::Godot.DirAccess;
using FileAccess = global::Godot.FileAccess;

namespace KemoCard.Fixed.Godot;

/// <summary>
/// Godot 资源文件系统：<c>res://</c> 在编辑器与 PCK 中都能枚举 / 读取，
/// <c>user://</c> 与普通绝对路径回落到 <see cref="FileAccess"/> / <see cref="DirAccess"/>。
/// </summary>
public sealed class GodotContentModFileSystem : IContentModFileSystem
{
    public static GodotContentModFileSystem Instance { get; } = new();

    public bool DirectoryExists(string path)
    {
        if (ContentModPath.IsResourcePath(path))
        {
            return DirAccess.DirExistsAbsolute(path);
        }

        return Directory.Exists(path);
    }

    public bool FileExists(string path)
    {
        if (ContentModPath.IsResourcePath(path))
        {
            return FileAccess.FileExists(path);
        }

        return File.Exists(path);
    }

    public IReadOnlyList<string> EnumerateDirectories(string path) => Enumerate(path, directories: true);

    public IReadOnlyList<string> EnumerateFiles(string path) => Enumerate(path, directories: false);

    public string ReadAllText(string path)
    {
        if (!ContentModPath.IsResourcePath(path))
        {
            return File.ReadAllText(path);
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new IOException($"无法读取内容 Mod 文件 '{path}'：{FileAccess.GetOpenError()}。");
        return Encoding.UTF8.GetString(file.GetBuffer((long)file.GetLength()));
    }

    public byte[] ReadAllBytes(string path)
    {
        if (!ContentModPath.IsResourcePath(path))
        {
            return File.ReadAllBytes(path);
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new IOException($"无法读取内容 Mod 文件 '{path}'：{FileAccess.GetOpenError()}。");
        return file.GetBuffer((long)file.GetLength());
    }

    private static List<string> Enumerate(string path, bool directories)
    {
        var result = new List<string>();

        if (ContentModPath.IsResourcePath(path))
        {
            using var dir = DirAccess.Open(path);
            if (dir is null)
            {
                return result;
            }

            dir.ListDirBegin();
            while (true)
            {
                string name = dir.GetNext();
                if (string.IsNullOrEmpty(name))
                {
                    break;
                }

                if (name is "." or ".." || dir.CurrentIsDir() != directories)
                {
                    continue;
                }

                result.Add(ContentModPath.Combine(path, name));
            }

            dir.ListDirEnd();
            return result;
        }

        if (!Directory.Exists(path))
        {
            return result;
        }

        result.AddRange(directories ? Directory.GetDirectories(path) : Directory.GetFiles(path));
        return result;
    }
}
