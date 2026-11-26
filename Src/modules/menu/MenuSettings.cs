using Godot;
using KemoCard.Frame.Logging;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 主菜单可调选项：音量 / 全屏 / 垂直同步 / 帧率显示。
/// 改动立即生效并写入 <c>user://menu.cfg</c>，下次启动自动恢复。
/// </summary>
public static class MenuSettings
{
    private const string ConfigPath = "user://menu.cfg";
    private const string Section = "menu";
    private const int MasterBus = 0;

    public static float MasterVolume { get; private set; } = 0.8f;
    public static bool Fullscreen { get; private set; }
    public static bool Vsync { get; private set; } = true;
    public static bool ShowFps { get; private set; } = true;

    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(ConfigPath) != Error.Ok) return;

        MasterVolume = Mathf.Clamp((float)file.GetValue(Section, "master_volume", MasterVolume).AsDouble(), 0f, 1f);
        Fullscreen = file.GetValue(Section, "fullscreen", Fullscreen).AsBool();
        Vsync = file.GetValue(Section, "vsync", Vsync).AsBool();
        ShowFps = file.GetValue(Section, "show_fps", ShowFps).AsBool();
    }

    public static void Apply()
    {
        ApplyVolume();
        ApplyDisplay();
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyVolume();
        // 拖动滑杆会连续触发，这里只实时生效，落盘交给 Close 时统一 Flush。
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
        ApplyDisplay();
        Save();
    }

    public static void SetVsync(bool value)
    {
        Vsync = value;
        ApplyDisplay();
        Save();
    }

    public static void SetShowFps(bool value)
    {
        ShowFps = value;
        Save();
    }

    private static void ApplyVolume()
    {
        AudioServer.SetBusMute(MasterBus, MasterVolume <= 0.001f);
        AudioServer.SetBusVolumeDb(MasterBus,
            MasterVolume <= 0.001f ? -80f : Mathf.LinearToDb(MasterVolume));
    }

    private static void ApplyDisplay()
    {
        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetVsyncMode(Vsync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
    }

    /// <summary>把当前值写入 <c>user://menu.cfg</c>。连续拖动的音量由调用方在关闭面板时统一落盘。</summary>
    public static void Save()
    {
        var file = new ConfigFile();
        file.SetValue(Section, "master_volume", MasterVolume);
        file.SetValue(Section, "fullscreen", Fullscreen);
        file.SetValue(Section, "vsync", Vsync);
        file.SetValue(Section, "show_fps", ShowFps);

        Error error = file.Save(ConfigPath);
        if (error != Error.Ok)
        {
            AppLog.Warning($"主菜单设置保存失败：{error}", "Menu");
        }
    }}
