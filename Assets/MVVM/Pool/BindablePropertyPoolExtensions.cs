using System;
using System.Collections.Generic;

/// <summary>
/// BindableProperty 扩展方法：便捷地将实例注册到全局 Pool 中
/// </summary>
public static class BindablePropertyPoolExtensions
{
    /// <summary>
    /// 记录本地 BindableProperty → 池名的映射，供 AddPoolBinding() 无参重载反向查找
    /// </summary>
    private static Dictionary<IBindableProperty, string> _propNameMap = new Dictionary<IBindableProperty, string>();

    /// <summary>
    /// 将本地 BindableProperty 注册到全局池并建立双向同步
    /// </summary>
    public static BindableProperty<T> AddToPool<T>(this BindableProperty<T> localProp, string name, T initialValue = default)
    {
        var poolProp = BindablePropertyPool.GetOrCreate<T>(name, initialValue);
        _propNameMap[localProp] = name;
        bool syncing = false;

        // 池 → 本地：外部通过 Pool.Set 修改时，同步回用户变量
        poolProp.RegistValueChanged(v =>
        {
            if (!syncing) { syncing = true; localProp.Value = v; syncing = false; }
        });

        // 本地 → 池：用户修改本地变量时，同步到池
        localProp.RegistValueChanged(v =>
        {
            if (!syncing) { syncing = true; poolProp.Value = v; syncing = false; }
        });

        return poolProp;
    }

    /// <summary>
    /// 为当前数据属性创建对应的池事件属性（Action&lt;T&gt; 占位），并自动加入该属性的事件集
    /// </summary>
    /// <param name="localProp">数据属性（需先 AddToPool）</param>
    /// <param name="eventName">事件名</param>
    public static BindableProperty<Action<T>> AddPoolEvent<T>(this BindableProperty<T> localProp, string eventName)
    {
        var eventProp = BindablePropertyPool.AddEvent<T>(eventName);

        // 自动加入事件集
        if (_propNameMap.TryGetValue(localProp, out string dataName))
            BindablePropertyPool.AddEventToSet(dataName, eventName);

        return eventProp;
    }

    /// <summary>
    /// 将当前数据属性与指定事件属性绑定（数据变化时触发事件）
    /// 需要先通过 AddPoolEvent 创建事件、通过 Action.RegistValueChanged 追加业务逻辑
    /// </summary>
    public static void AddPoolBinding<T>(this BindableProperty<T> localProp, string eventName)
    {
        var eventProp = BindablePropertyPool.GetEvent<T>(eventName);
        if (eventProp == null)
        {
            UnityEngine.Debug.LogWarning($"[Pool] AddPoolBinding 失败：事件 \"{eventName}\" 尚未创建，请先调用 AddPoolEvent");
            return;
        }

        localProp.RegistValueChanged(v =>
        {
            eventProp.Value?.Invoke(v);
        });
    }

    /// <summary>
    /// 无参重载：自动查找该属性在池中的名称，一次性绑定事件集中的全部事件
    /// 前置条件：已调用 AddToPool 注册属性名、已通过 AddPoolEvent 创建事件
    /// </summary>
    public static void AddPoolBinding<T>(this BindableProperty<T> localProp)
    {
        if (!_propNameMap.TryGetValue(localProp, out string dataName))
        {
            UnityEngine.Debug.LogWarning("[Pool] AddPoolBinding() 失败：请先调用 AddToPool 注册属性名");
            return;
        }

        BindablePropertyPool.BindAll<T>(dataName);
    }

    /// <summary>
    /// 手动将指定事件加入当前属性的事件集（通常 AddPoolEvent 已自动处理）
    /// </summary>
    public static void AddPoolEventToSet<T>(this BindableProperty<T> localProp, string eventName)
    {
        if (_propNameMap.TryGetValue(localProp, out string dataName))
            BindablePropertyPool.AddEventToSet(dataName, eventName);
    }
}
