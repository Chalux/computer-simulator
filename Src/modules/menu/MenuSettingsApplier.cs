using Godot;

namespace ComputerSimulator.Modules.Menu;

/// <summary>
/// 把 <see cref="MenuSettings"/> 应用到引擎：主总线音量与窗口显示模式 / 垂直同步。
/// 只做副作用，不持有状态；由 <see cref="MenuController"/> 调用。
/// </summary>
internal sealed class MenuSettingsApplier
{
    private const int MasterBus = 0;

    /// <summary>应用主音量：0 附近静音，其余按线性转 dB。</summary>
    public void ApplyVolume(float masterVolume)
    {
        AudioServer.SetBusMute(MasterBus, masterVolume <= 0.001f);
        AudioServer.SetBusVolumeDb(MasterBus,
            masterVolume <= 0.001f ? -80f : Mathf.LinearToDb(masterVolume));
    }

    /// <summary>应用窗口模式与垂直同步。</summary>
    public void ApplyDisplay(MenuSettings settings)
    {
        DisplayServer.WindowSetMode(settings.Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetVsyncMode(settings.Vsync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
    }
}
