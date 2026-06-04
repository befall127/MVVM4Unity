using System;
using System.Collections.Generic;
using UnityEngine;

/*
 使用方式

  1. 定义事件数据

  public class GameStartEvent
  {
      public int Level;
      public string Difficulty;
  }

  2. ViewModel 中注册处理器

  public class GameViewModel : ViewModelBase
  {
      public GameViewModel()
      {
          SetValue<Action<GameStartEvent>>("OnGameStart", e =>
          {
              Debug.Log($"游戏开始: 关卡{e.Level}, 难度{e.Difficulty}");
          });
      }
  }

  3. View 中绑定全局事件

  public class GameView : ViewBase
  {
      protected override void OnBinding()
      {
          // 一行代码，View 销毁时自动取消订阅
          _binder.RegisterGlobalEvent<GameStartEvent>("OnGameStart");
      }
  }

  4. 任意位置发布事件

  // 发布事件（每次都触发）
  EventBus.Publish(new GameStartEvent { Level = 1, Difficulty = "Hard" });

  // 持久化发布（晚订阅者也能收到）
  EventBus.Publish(new GameStartEvent { Level = 1 }, persist: true);
  var last = EventBus.GetPersisted<GameStartEvent>(); // 获取最后一次
*/

/// <summary>
/// 全局事件总线：基于 BindableProperty&lt;Action&lt;T&gt;&gt; 实现的类型安全事件系统
/// 与 MVVM 框架深度集成，支持通过 Binder.RegisterGlobalEvent 在 View 中直接绑定
///
/// 设计要点：
/// - 内部使用 BindableProperty&lt;Action&lt;T&gt;&gt; 作为事件载体
/// - Publish 直接调用 Action.Invoke，不修改 Value，绕过 Equals 守卫
/// - 每次 Publish 都会触发所有订阅者（区别于 BindableProperty 的值变化才通知）
/// </summary>
public static class EventBus
{
    /// <summary>事件存储：每个事件类型 T 对应一个 BindableProperty&lt;Action&lt;T&gt;&gt;</summary>
    private static Dictionary<Type, IBindableProperty> _events = new Dictionary<Type, IBindableProperty>();

    /// <summary>持久化存储：保存最后一次发布的事件数据，供晚订阅者获取</summary>
    private static Dictionary<Type, object> _persisted = new Dictionary<Type, object>();

    // ─── 订阅 / 取消 ───

    /// <summary>
    /// 订阅指定类型的全局事件
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="handler">事件处理器</param>
    public static void Subscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        var prop = GetOrCreateEventProperty<T>();
        prop.RegistValueChanged(handler);
    }

    /// <summary>
    /// 取消订阅指定类型的全局事件
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        var prop = GetEventProperty<T>();
        if (prop != null)
        {
            prop.RemoveValueChanged(handler);
        }
    }

    // ─── 发布 ───

    /// <summary>
    /// 发布全局事件，所有订阅者同步收到通知
    /// 每次调用都会触发（无相等检查），无论事件数据是否与上次相同
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="eventData">事件数据</param>
    public static void Publish<T>(T eventData)
    {
        var prop = GetEventProperty<T>();
        if (prop == null) return;

        var handler = prop.Value;
        if (handler != null)
        {
            try
            {
                handler.Invoke(eventData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] 事件 {typeof(T).Name} 处理异常: {e}");
            }
        }
    }

    /// <summary>
    /// 发布全局事件并可选持久化
    /// 持久化后，晚订阅者可通过 GetPersisted 获取最后一次发布的数据
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <param name="persist">是否持久化</param>
    public static void Publish<T>(T eventData, bool persist)
    {
        Publish(eventData);

        if (persist)
        {
            _persisted[typeof(T)] = eventData;
        }
    }

    // ─── 持久化 ───

    /// <summary>
    /// 获取最后一次持久化发布的事件数据
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <returns>持久化的事件数据，不存在返回 default</returns>
    public static T GetPersisted<T>()
    {
        if (_persisted.TryGetValue(typeof(T), out var data))
        {
            return (T)data;
        }
        return default;
    }

    // ─── 清理 ───

    /// <summary>
    /// 清除指定事件类型的所有订阅和持久化数据
    /// </summary>
    public static void Clear<T>()
    {
        _events.Remove(typeof(T));
        _persisted.Remove(typeof(T));
    }

    /// <summary>
    /// 清除所有事件订阅和持久化数据
    /// </summary>
    public static void ClearAll()
    {
        _events.Clear();
        _persisted.Clear();
    }

    // ─── 内部方法 ───

    /// <summary>
    /// 获取或创建指定事件类型的 BindableProperty&lt;Action&lt;T&gt;&gt;
    /// </summary>
    private static BindableProperty<Action<T>> GetOrCreateEventProperty<T>()
    {
        if (_events.TryGetValue(typeof(T), out var existing))
        {
            return existing as BindableProperty<Action<T>>;
        }

        var prop = new BindableProperty<Action<T>>();
        _events[typeof(T)] = prop;
        return prop;
    }

    /// <summary>
    /// 获取指定事件类型的 BindableProperty（不存在返回 null）
    /// </summary>
    private static BindableProperty<Action<T>> GetEventProperty<T>()
    {
        if (_events.TryGetValue(typeof(T), out var existing))
        {
            return existing as BindableProperty<Action<T>>;
        }
        return null;
    }
}
