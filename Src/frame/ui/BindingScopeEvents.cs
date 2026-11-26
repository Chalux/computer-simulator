using Godot;
using KemoCard.Frame.Logging;
using KemoCard.Frame.Mvc;
using KemoCard.Frame.UI.Base;

namespace KemoCard.Frame.UI;

/// <summary>
/// <see cref="BindingScope"/> 的事件总线糖：把 <see cref="EventSubscribeAttribute"/> 标注的方法
/// 收进界面订阅账本，使装饰器订阅与 Godot 信号拥有同一条生命周期。
/// </summary>
public static class BindingScopeEvents
{
    /// <summary>
    /// 扫描 <paramref name="target"/> 上的装饰器方法并登记到 <paramref name="bus"/>；
    /// 解绑动作进入账本，离场或框架重新 <c>InitEvent</c> 前统一取消。
    /// </summary>
    /// <param name="scope">界面订阅账本。</param>
    /// <param name="bus">要订阅的事件总线。</param>
    /// <param name="target">被扫描的实例（通常是界面自身）。</param>
    /// <param name="caller">登记 caller；缺省为 <paramref name="target"/>。</param>
    /// <returns>本次批量登记句柄；没有装饰器方法时 <see cref="EventBinding.Count"/> 为 0，不占用账本条目。</returns>
    public static EventBinding BindDecorated(
        this BindingScope scope,
        EventDispatcher bus,
        object target,
        object? caller = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(target);

        var binding = bus.BindDecorated(target, caller ?? target, bindingOwner: scope);
        if (binding.Count > 0)
        {
            scope.Add(binding.Off);
        }

        return binding;
    }

    /// <summary>
    /// 把 <paramref name="target"/> 上的装饰器方法绑定到界面所属的功能总线
    /// （归属门面实现 <see cref="IEventBusProvider"/> 时）。
    /// </summary>
    /// <remarks>
    /// <para>由框架在 <c>InitEvent</c> 之后自动调用。界面尚未绑定 <see cref="UIVo"/>（编辑器预览、
    /// 独立实例化）或归属门面不提供总线时不做任何绑定，也不抛异常——界面实例化不应因归属解析而失败。</para>
    /// <para>只绑定归属功能总线，不碰 <see cref="GlobalEvents.Bus"/>：跨功能总线按需由界面在
    /// <c>InitEvent</c> 中显式 <see cref="BindDecorated"/>，避免把所有装饰器方法注册到错误的总线。</para>
    /// <para>登记与解绑走 <see cref="BindingScope"/>：缓存重开时 <c>InvokeResetBindings</c> 先清账，
    /// 不会积累重复订阅。</para>
    /// </remarks>
    public static void BindOwnerDecorated(this BindingScope scope, UIVo? vo, object target)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(target);

        if (vo is null || string.IsNullOrEmpty(vo.OwnerModId))
        {
            WarnUnresolved(target, "尚未绑定 UIVo / OwnerModId");
            return;
        }

        var provider = vo.Manager.FacadeProvider;
        var facade = provider?.ResolveUiFacade(vo.OwnerModId);
        if (facade is not IEventBusProvider busProvider)
        {
            WarnUnresolved(target, provider is null
                ? "组合根未注入 IUiFacadeProvider"
                : $"功能 '{vo.OwnerModId}' 未提供 IEventBusProvider");
            return;
        }

        scope.BindDecorated(busProvider.EventBus, target);
    }

    private static void WarnUnresolved(object target, string reason)
    {
        if (!EventDecoratorBinder.HasDecoratedMethods(target.GetType()))
        {
            return;
        }

        AppLog.Warning(
            $"UI<{target.GetType().Name}> 声明了 [EventSubscribe]，但{reason}，装饰器订阅未绑定。",
            "UI");
    }

    /// <summary>
    /// 重建 <paramref name="root"/> 子树内所有 <see cref="BaseCmp"/> 的装饰器订阅。
    /// 由窗口在每次 <c>InitEvent</c> 后调用：已挂载窗口再次 Open 时子组件不会重走
    /// <c>_EnterTree</c>，必须显式切到当前功能总线。
    /// </summary>
    internal static void RebindDescendantDecoratedEvents(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is BaseCmp component)
            {
                component.RebindDecoratedEvents(FindOwnerVo(component));
            }

            RebindDescendantDecoratedEvents(child);
        }
    }

    /// <summary>
    /// 沿父链寻找最近的界面归属（<see cref="BaseWin"/> / <see cref="BaseMask"/>）。
    /// 组件类（<see cref="BaseCmp"/>）没有自己的 <see cref="UIVo"/>，靠它取得所属界面的总线。
    /// </summary>
    /// <remarks>
    /// 命中最近的界面根就返回（<see cref="UIVo"/> 可能为 null），不会再向上找到外层界面的归属；
    /// 否则手动实例化 / 尚未绑定 UIVo 的内层窗口会把组件的事件绑到外层功能总线上。
    /// </remarks>
    internal static UIVo? FindOwnerVo(Node node)
    {
        for (var current = node.GetParent(); current is not null; current = current.GetParent())
        {
            switch (current)
            {
                case BaseWin win:
                    return win.UIVo;
                case BaseMask mask:
                    return mask.UIVo;
            }
        }

        return null;
    }
}