using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局 BindableProperty 池：单例，托管所有跨脚本共享的数据属性与事件属性
/// 自定义数据属性（如 "Count"）和事件属性（如 "CountChanged"）统一存于此池中
/// </summary>
public static class BindablePropertyPool
{
    private static Dictionary<string, IBindableProperty> _pool = new Dictionary<string, IBindableProperty>();

    /// <summary>
    /// 事件集：每个数据属性名下关联一组事件名，供 BindAll 一次性绑定全部事件
    /// </summary>
    private static Dictionary<string, List<string>> _eventSets = new Dictionary<string, List<string>>();

    #region 数据属性

    /// <summary>
    /// 获取或创建数据属性。已有则返回已有，无则创建新实例并设置初始值
    /// </summary>
    public static BindableProperty<T> GetOrCreate<T>(string name, T initialValue = default)
    {
        if (_pool.TryGetValue(name, out var existing))
        {
            var typed = existing as BindableProperty<T>;
            if (typed != null) return typed;
            // 类型冲突：已有该名称但类型不同，记录警告并覆盖
            Debug.LogWarning($"[Pool] 属性 \"{name}\" 已存在但类型不匹配，将覆盖");
            _pool.Remove(name);
        }
        var prop = new BindableProperty<T> { Value = initialValue };
        _pool[name] = prop;
        return prop;
    }

    /// <summary>
    /// 获取数据属性（不存在时返回 null）
    /// </summary>
    public static BindableProperty<T> Get<T>(string name)
    {
        if (_pool.TryGetValue(name, out var existing))
            return existing as BindableProperty<T>;
        return null;
    }

    #endregion

    #region 事件属性

    /// <summary>
    /// 创建/获取数据变化事件属性：Action&lt;T&gt; 占位，具体业务逻辑由外部订阅
    /// </summary>
    public static BindableProperty<Action<T>> AddEvent<T>(string eventName)
    {
        if (_pool.TryGetValue(eventName, out var existing))
        {
            var typed = existing as BindableProperty<Action<T>>;
            if (typed != null) return typed;
            Debug.LogWarning($"[Pool] 事件 \"{eventName}\" 已存在但类型不匹配，将覆盖");
            _pool.Remove(eventName);
        }
        var prop = new BindableProperty<Action<T>>();
        _pool[eventName] = prop;
        return prop;
    }

    /// <summary>
    /// 获取事件属性
    /// </summary>
    public static BindableProperty<Action<T>> GetEvent<T>(string eventName)
    {
        return Get<Action<T>>(eventName);
    }

    #endregion

    #region 绑定：数据 ↔ 事件

    /// <summary>
    /// 将数据属性 "dataName" 的 onValueChanged 桥接到事件属性 "eventName" 的 Invoke
    /// 需要外部先通过 AddEvent 创建事件占位、业务逻辑通过 RegistValueChanged 追加到事件上
    /// </summary>
    public static void Bind<T>(string dataName, string eventName)
    {
        var dataProp = GetOrCreate<T>(dataName);
        var eventProp = GetOrCreate<Action<T>>(eventName);
        if (dataProp == null || eventProp == null)
        {
            Debug.LogError($"[Pool] Bind 失败：\"{dataName}\" 或 \"{eventName}\" 不存在");
            return;
        }

        dataProp.RegistValueChanged(v =>
        {
            eventProp.Value?.Invoke(v);
        });
    }

    #endregion

    #region 事件集：一个数据属性 → 多个事件，BindAll 一次性批量绑定

    /// <summary>
    /// 将事件名加入指定数据属性的事件集。BindAll 时会全部绑定
    /// </summary>
    public static void AddEventToSet(string dataName, string eventName)
    {
        if (!_pool.ContainsKey(dataName))
        {
            Debug.LogWarning($"[Pool] AddEventToSet: 数据属性 \"{dataName}\" 不存在，请先 AddToPool");
            return;
        }

        if (!_eventSets.ContainsKey(dataName))
            _eventSets[dataName] = new List<string>();

        if (!_eventSets[dataName].Contains(eventName))
        {
            _eventSets[dataName].Add(eventName);
            Debug.Log($"[Pool] 事件 \"{eventName}\" 已加入属性 \"{dataName}\" 的事件集");
        }
    }

    /// <summary>
    /// 将数据属性一次性绑定到其事件集中的全部事件（无参重载），不指定数据名时遍历匹配
    /// </summary>
    public static void BindAll<T>(string dataName)
    {
        var dataProp = Get<T>(dataName);
        if (dataProp == null)
        {
            Debug.LogWarning($"[Pool] BindAll 失败：数据属性 \"{dataName}\" 不存在");
            return;
        }

        if (!_eventSets.TryGetValue(dataName, out var eventList) || eventList.Count == 0)
        {
            Debug.LogWarning($"[Pool] BindAll: 属性 \"{dataName}\" 的事件集为空，请先 AddEventToSet");
            return;
        }

        int bound = 0;
        foreach (var eventName in eventList)
        {
            var eventProp = GetEvent<T>(eventName);
            if (eventProp == null)
            {
                Debug.LogWarning($"[Pool] BindAll: 事件 \"{eventName}\" 不存在，跳过");
                continue;
            }
            dataProp.RegistValueChanged(v => eventProp.Value?.Invoke(v));
            bound++;
        }

        Debug.Log($"[Pool] BindAll: \"{dataName}\" 已绑定 {bound}/{eventList.Count} 个事件");
    }

    /// <summary>
    /// 输出指定属性的完整事件集信息，用于 Debug
    /// </summary>
    public static void LogEventSet(string dataName)
    {
        if (!_eventSets.TryGetValue(dataName, out var list) || list.Count == 0)
        {
            Debug.Log($"[Pool] 属性 \"{dataName}\" 的事件集为空");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Pool] ═══ 属性 \"{dataName}\" 事件集 ({list.Count} 个) ═══");
        for (int i = 0; i < list.Count; i++)
        {
            var evt = Get<Action<object>>(list[i]);
            string status = evt != null ? (evt.Value != null ? "已实现" : "空占位") : "缺";
            sb.AppendLine($"  [{i + 1}] {list[i]}  ({status})");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 获取指定属性的事件集列表（只读）
    /// </summary>
    public static IReadOnlyList<string> GetEventSet(string dataName)
    {
        if (_eventSets.TryGetValue(dataName, out var list))
            return list;
        return Array.Empty<string>();
    }

    #endregion

    #region 工具

    /// <summary>
    /// 直接设置数据属性的值，等效于 SetValue
    /// </summary>
    public static void Set<T>(string name, T value)
    {
        GetOrCreate<T>(name).Value = value;
    }

    /// <summary>
    /// 获取数据属性的当前值
    /// </summary>
    public static T GetValue<T>(string name)
    {
        var prop = Get<T>(name);
        return prop != null ? prop.Value : default;
    }

    /// <summary>
    /// 检查池中是否存在指定名称的属性
    /// </summary>
    public static bool Contains(string name)
    {
        return _pool.ContainsKey(name);
    }

    /// <summary>
    /// 移除指定属性
    /// </summary>
    public static bool Remove(string name)
    {
        return _pool.Remove(name);
    }

    /// <summary>
    /// 清空整个池（含属性池和事件集）
    /// </summary>
    public static void Clear()
    {
        _pool.Clear();
        _eventSets.Clear();
    }

    #endregion
}
