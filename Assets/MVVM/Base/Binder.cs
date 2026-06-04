using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// 绑定层，连接 View 与 ViewModel，供子类使用
/// 负责数据绑定（ViewModel → UI）和事件绑定（UI → ViewModel）
/// </summary>
public class Binder
{
    private ViewModelBase _viewModel;
    private MonoBehaviour _view;

    /// <summary>
    /// 存储所有已注册的解绑操作，用于统一清理
    /// </summary>
    private List<Action> _unbindActions = new List<Action>();

    public Binder(MonoBehaviour view, ViewModelBase viewModel)
    {
        _view = view;
        _viewModel = viewModel;
    }

    #region 单向绑定：ViewModel 属性变化 → 自动更新 UI

    /// <summary>
    /// 注册单向数据绑定：ViewModel 属性变化 → 自动更新 UI
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="uiPath">UI 路径标识</param>
    /// <param name="viewModelProperty">ViewModel 中的属性名</param>
    /// <param name="onSetUI">数据变化时更新 UI 的回调</param>
    public void RegisterMember<T>(string uiPath, string viewModelProperty, Action<T> onSetUI)
    {
        var prop = _viewModel.GetBindableProperty<T>(viewModelProperty);

        UnityAction<T> callback = (newValue) =>
        {
            onSetUI?.Invoke(newValue);
        };
        prop.RegistValueChanged(callback);

        _unbindActions.Add(() => prop.RemoveValueChanged(callback));

        onSetUI?.Invoke(prop.Value);
    }

    /// <summary>
    /// 注册单向数据事件绑定: Value变化 → onChange响应 value: T , onChange: UnityAction
    /// </summary>
    public void RegisterMember<T>(string valueName , string onChange)
    {
        var valueProperty = _viewModel.GetBindableProperty<T>(valueName);
        var onChangeProperty = _viewModel.GetBindableProperty<Action<T>>(onChange);

        if (onChangeProperty == null)
        {
            Debug.LogWarning($"[Binder] RegisterMember 失败：属性 \"{valueName}\" 预期绑定的类型与实际传入 Action 不匹配");
            return;
        }

        UnityAction<T> callback = (v) =>
        {
            onChangeProperty.Value?.Invoke(v);
        };

        valueProperty.RegistValueChanged(callback);
        _unbindActions.Add(() =>
        {
            valueProperty.RemoveValueChanged(callback);
        });
    }

    #endregion

    #region 双向绑定：BindableProperty ↔ BindableProperty

    /// <summary>
    /// 将任意值包装为 BindableProperty，可选绑定 UnityEvent&lt;T&gt; 实现 UI 变更时自动同步
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="initialValue">初始值（如 _input.text、_toggle.isOn、_slider.value）</param>
    /// <param name="onChange">UI 变更事件（如 _input.onValueChanged、_slider.onValueChanged），可为 null 仅做静态包装</param>
    public BindableProperty<T> Wrap<T>(T initialValue, UnityEvent<T> onChange = null)
    {
        var prop = new BindableProperty<T>();
        prop.Value = initialValue;

        if (onChange != null)
        {
            UnityAction<T> listener = (v) =>
            {
                prop.Value = v;
            };
            onChange.AddListener(listener);
            _unbindActions.Add(() => onChange.RemoveListener(listener));
        }

        return prop;
    }

    /// <summary>
    /// 双向绑定两个 BindableProperty：任意一方变化都会自动同步到另一方
    /// 通过防递归标志避免死循环
    /// </summary>
    /// <typeparam name="T">属性类型，双方必须一致</typeparam>
    /// <param name="propA">属性 A（如 UI 控件的包装属性）</param>
    /// <param name="propB">属性 B（如 ViewModel 中的存储属性）</param>
    public void BindTwoWay<T>(BindableProperty<T> propA, BindableProperty<T> propB)
    {
        bool isSyncing = false;

        // propA → propB
        UnityAction<T> syncAtoB = (v) =>
        {
            if (isSyncing) return;
            isSyncing = true;
            propB.Value = v;
            isSyncing = false;
        };
        propA.RegistValueChanged(syncAtoB);

        // propB → propA
        UnityAction<T> syncBtoA = (v) =>
        {
            if (isSyncing) return;
            isSyncing = true;
            propA.Value = v;
            isSyncing = false;
        };
        propB.RegistValueChanged(syncBtoA);

        _unbindActions.Add(() => propA.RemoveValueChanged(syncAtoB));
        _unbindActions.Add(() => propB.RemoveValueChanged(syncBtoA));
    }

    #endregion

    #region 事件绑定：UI 事件 → 驱动 ViewModel 逻辑

    /// <summary>
    /// 无参事件绑定：UI 事件触发时调用 ViewModel 中的 Action
    /// </summary>
    /// <param name="uEvent">任意 UnityEvent（如 button.onClick）</param>
    /// <param name="viewModelFunc">ViewModel 中 Action 的属性名</param>
    public bool RegisterEvent(UnityEvent uEvent, string viewModelFunc)
    {
        var prop = _viewModel.GetBindableProperty<Action>(viewModelFunc);

        if (prop == null)
        {
            Debug.LogWarning($"[Binder] RegisterEvent 失败：属性 \"{viewModelFunc}\" 绑定的类型与 Action 不匹配");
            return false;
        }

        UnityAction listener = () =>
        {
            prop.Value?.Invoke();
        };
        uEvent.AddListener(listener);

        _unbindActions.Add(() => uEvent.RemoveListener(listener));
        return true;
    }

    /// <summary>
    /// 带参事件绑定：UnityEvent&lt;T&gt; 的参数直接传给 ViewModel 的 Action&lt;T&gt;
    /// </summary>
    /// <typeparam name="T">事件参数类型</typeparam>
    /// <param name="uEvent">带参的 UnityEvent（如 inputField.onValueChanged）</param>
    /// <param name="viewModelFunc">ViewModel 中 Action&lt;T&gt; 的属性名</param>
    public bool RegisterEvent<T>(UnityEvent<T> uEvent, string viewModelFunc)
    {
        var prop = _viewModel.GetBindableProperty<Action<T>>(viewModelFunc);

        if (prop == null)
        {
            Debug.LogWarning($"[Binder] RegisterEvent<{typeof(T).Name}> 失败：属性 \"{viewModelFunc}\" 绑定的类型不匹配。" +
                $"请确保 ViewModel 中已通过 SetValue<Action<{typeof(T).Name}>> 设置该属性。");
            return false;
        }

        UnityAction<T> listener = (arg) =>
        {
            prop.Value?.Invoke(arg);
        };
        uEvent.AddListener(listener);

        _unbindActions.Add(() => uEvent.RemoveListener(listener));
        return true;
    }

    /// <summary>
    /// 带上下文捕获的事件绑定：UI 事件触发时，通过 getParameter 从 UI 获取参数并传给 ViewModel 的 Action&lt;T&gt;
    /// 适用于 UnityEvent（无参事件）需要携带额外数据的场景
    /// </summary>
    /// <typeparam name="T">命令参数类型</typeparam>
    /// <param name="uEvent">任意 UnityEvent（如 button.onClick、toggle.onValueChanged 等）</param>
    /// <param name="viewModelFunc">ViewModel 中 Action&lt;T&gt; 的属性名</param>
    /// <param name="getParameter">从 UI 上下文获取参数的回调</param>
    public bool RegisterEvent<T>(UnityEvent uEvent, string viewModelFunc, Func<T> getParameter)
    {
        var prop = _viewModel.GetBindableProperty<Action<T>>(viewModelFunc);

        if (prop == null)
        {
            Debug.LogWarning($"[Binder] RegisterEvent<{typeof(T).Name}> 失败：属性 \"{viewModelFunc}\" 绑定的类型不匹配。");
            return false;
        }

        UnityAction listener = () =>
        {
            T param = getParameter();
            prop.Value?.Invoke(param);
        };
        uEvent.AddListener(listener);

        _unbindActions.Add(() => uEvent.RemoveListener(listener));
        return true;
    }

    /// <summary>
    /// 注册全局事件绑定：EventBus 发布 T 时触发 ViewModel 中的 Action&lt;T&gt;
    /// View 销毁时自动取消订阅，无需手动管理生命周期
    /// </summary>
    /// <typeparam name="T">事件数据类型</typeparam>
    /// <param name="viewModelFunc">ViewModel 中 Action&lt;T&gt; 的属性名</param>
    public bool RegisterGlobalEvent<T>(string viewModelFunc)
    {
        var prop = _viewModel.GetBindableProperty<Action<T>>(viewModelFunc);

        if (prop == null)
        {
            Debug.LogWarning($"[Binder] RegisterGlobalEvent<{typeof(T).Name}> 失败：属性 \"{viewModelFunc}\" 绑定的类型不匹配。" +
                $"请确保 ViewModel 中已通过 SetValue<Action<{typeof(T).Name}>> 设置该属性。");
            return false;
        }

        // 捕获 prop 引用（非值），EventBus 触发时读取最新 prop.Value
        Action<T> handler = (data) =>
        {
            prop.Value?.Invoke(data);
        };

        EventBus.Subscribe<T>(handler);
        _unbindActions.Add(() => EventBus.Unsubscribe<T>(handler));
        return true;
    }

    #endregion

    #region 解绑

    /// <summary>
    /// 解除所有已注册的绑定（数据绑定 + 事件绑定），通常在界面销毁时调用
    /// </summary>
    public void UnbindAll()
    {
        foreach (var unbind in _unbindActions)
        {
            unbind?.Invoke();
        }
        _unbindActions.Clear();
    }

    #endregion
}
