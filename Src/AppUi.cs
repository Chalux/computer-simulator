using ComputerSimulator.Modules.Commission;
using ComputerSimulator.Modules.Diagnosis;
using ComputerSimulator.Modules.Menu;
using ComputerSimulator.Modules.Workbench;
using KemoCard.Frame.UI;
using KemoCard.Frame.UI.Def;

namespace ComputerSimulator;

/// <summary>
/// 生产路由清单。界面 id 同时是 <c>{Dir}/{Id}.tscn</c> 的文件名，
/// 组合根与各界面共用同一份常量，避免路由字符串两处漂移。
/// </summary>
internal static class UiRoutes
{
    public const string MenuDir = "Src/modules/menu/ui";
    public const string CommissionDir = "Src/modules/commission/ui";
    public const string WorkbenchDir = "Src/modules/workbench/ui";
    public const string DiagnosisDir = "Src/modules/diagnosis/ui";

    public const string MainMenu = "main_menu";
    public const string ModeSelect = "mode_select";
    public const string ModeBrief = "mode_brief";
    public const string MenuSettings = "menu_settings";
    public const string MenuCredits = "menu_credits";
    public const string Commission = "commission_brief";
    public const string Workbench = "workbench";
    public const string Diagnosis = "diagnosis_case";

    public static readonly UiId<EmptyPayload> MainMenuId = new(MainMenu);
    public static readonly UiId<EmptyPayload> ModeSelectId = new(ModeSelect);
    public static readonly UiId<ModeBriefPayload> ModeBriefId = new(ModeBrief);
    public static readonly UiId<EmptyPayload> MenuSettingsId = new(MenuSettings);
    public static readonly UiId<EmptyPayload> MenuCreditsId = new(MenuCredits);
    public static readonly UiId<EmptyPayload> CommissionId = new(Commission);
    public static readonly UiId<WorkbenchRoutePayload> WorkbenchId = new(Workbench);
    public static readonly UiId<EmptyPayload> DiagnosisId = new(Diagnosis);
}

/// <summary>未开放模式的简报路由载荷：只承载“要展示哪个模式”这一条路由数据。</summary>
internal sealed record ModeBriefPayload(GameMode Mode);

/// <summary>工作台路由载荷：模式属于路由数据，控制器属于会话门面，两者不混在同一个对象里。</summary>
internal sealed record WorkbenchRoutePayload(bool CommissionMode);

/// <summary>
/// 启动期声明全部生产界面。归属 Mod 与 <see cref="MainRoot"/> 注入的门面提供者一一对应。
/// </summary>
internal static class AppUiRegistration
{
    public static IEnumerable<UIRegistration> Create()
    {
        yield return UIRegistration.Window(MenuMod.ModIdValue, UiRoutes.MainMenu, UiRoutes.MenuDir);
        yield return UIRegistration.Window(MenuMod.ModIdValue, UiRoutes.ModeSelect, UiRoutes.MenuDir);
        yield return UIRegistration.Window(MenuMod.ModIdValue, UiRoutes.ModeBrief, UiRoutes.MenuDir);
        yield return UIRegistration.Dialog(MenuMod.ModIdValue, UiRoutes.MenuSettings, UiRoutes.MenuDir);
        yield return UIRegistration.Dialog(MenuMod.ModIdValue, UiRoutes.MenuCredits, UiRoutes.MenuDir);
        yield return UIRegistration.Window(CommissionMod.FeatureId, UiRoutes.Commission, UiRoutes.CommissionDir);
        yield return UIRegistration.Window(WorkbenchUiFacade.ModId, UiRoutes.Workbench, UiRoutes.WorkbenchDir);
        yield return UIRegistration.Window(DiagnosisMod.ModIdValue, UiRoutes.Diagnosis, UiRoutes.DiagnosisDir);
    }
}
