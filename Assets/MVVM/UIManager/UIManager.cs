using System.Collections.Generic;
using UnityEngine;

/*
使用方式

  1. 注册界面配置

  // 在游戏初始化时注册
  UIConfig.Entries["LoginPanel"] = new UIEntry("UI/LoginPanel", UILayer.Normal);
  UIConfig.Entries["SettingsPanel"] = new UIEntry("UI/SettingsPanel", UILayer.Popup);

  2. 打开/关闭界面

  // MVVM 版：自动创建 ViewModel 并注入
  var info = UIManager.Instance.Open<LoginPanel, LoginViewModel>();

  // 带数据传递
  UIManager.Instance.Open<LoginPanel, LoginViewModel>(new { UserName = "test" });

  // 关闭（缓存复用）
  UIManager.Instance.Close("LoginPanel");

  // 返回上一个
  UIManager.Instance.CloseCurrent();

  3. 生命周期钩子

  public class LoginPanel : ViewBase
  {
      protected override void OnShow()
      {
          // 从缓存恢复时播放打开动画
          Debug.Log("LoginPanel 打开");
      }

      protected override void OnHide()
      {
          // 关闭时播放关闭动画
          Debug.Log("LoginPanel 关闭");
      }
  }

  缓存复用流程

  首次 Open → 加载 Prefab → SetViewModel → 显示
  Close → Unbind() → Hide() → 缓存
  再次 Open → 从缓存取 → Show() → 新建 ViewModel → SetViewModel → 显示

  View 实例被复用，ViewModel 每次都是全新的，Binder 每次都是全新的。

*/
/// <summary>
/// UI 管理器：管理 UI 生命周期，与 MVVM 框架联动
/// 功能：打开/关闭界面、缓存复用、层级管理、栈管理
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager _instance;

    /// <summary>单例访问点</summary>
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[UIManager]");
                _instance = go.AddComponent<UIManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // ─── 运行时数据 ───

    /// <summary>已打开的界面</summary>
    private Dictionary<string, UIInfo> _opened = new Dictionary<string, UIInfo>();

    /// <summary>已关闭但缓存的实例（等待复用）</summary>
    private Dictionary<string, UIInfo> _cached = new Dictionary<string, UIInfo>();

    /// <summary>层级栈：Normal 层维护打开顺序，支持返回上一个</summary>
    private Dictionary<UILayer, Stack<string>> _stacks = new Dictionary<UILayer, Stack<string>>();

    /// <summary>各层级的 Canvas 根节点</summary>
    private Dictionary<UILayer, Transform> _layerRoots = new Dictionary<UILayer, Transform>();

    // ─── 生命周期 ───

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitLayerRoots();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// 为每个 UILayer 创建一个 Canvas 子节点作为容器
    /// </summary>
    private void InitLayerRoots()
    {
        // 查找场景中的 Canvas 作为根
        var rootCanvas = FindObjectOfType<Canvas>();
        Transform canvasTransform;

        if (rootCanvas != null)
        {
            canvasTransform = rootCanvas.transform;
        }
        else
        {
            // 没有 Canvas 时创建一个
            var canvasGo = new GameObject("UICanvas");
            canvasGo.transform.SetParent(transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasTransform = canvasGo.transform;
        }

        // 为每个层级创建子节点
        foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
        {
            var layerGo = new GameObject(layer.ToString());
            layerGo.transform.SetParent(canvasTransform, false);

            var rt = layerGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _layerRoots[layer] = layerGo.transform;
        }
    }

    // ─── 公共 API ───

    /// <summary>
    /// 打开界面（MVVM 版）：自动创建 ViewModel 并注入
    /// </summary>
    /// <typeparam name="TView">View 类型</typeparam>
    /// <typeparam name="TViewModel">ViewModel 类型</typeparam>
    /// <param name="data">可选初始化数据（传给实现了 IDataReceiver 的 View）</param>
    /// <returns>界面信息，失败返回 null</returns>
    public UIInfo Open<TView, TViewModel>(object data = null)
        where TView : ViewBase
        where TViewModel : ViewModelBase, new()
    {
        var info = OpenInternal<TView>(data);
        if (info == null) return null;

        // 创建 ViewModel 并注入
        var viewModel = new TViewModel();
        info.ViewModel = viewModel;
        info.View.SetViewModel(viewModel);

        return info;
    }

    /// <summary>
    /// 打开界面（非 MVVM 版）：不创建 ViewModel，适用于纯展示或自管理的 UI
    /// </summary>
    public UIInfo Open<TView>(object data = null) where TView : ViewBase
    {
        return OpenInternal<TView>(data);
    }

    /// <summary>
    /// 关闭指定界面
    /// </summary>
    /// <param name="key">界面标识（View 类名）</param>
    /// <param name="destroy">true = 直接销毁，false = 缓存复用</param>
    public void Close(string key, bool destroy = false)
    {
        if (!_opened.TryGetValue(key, out var info))
        {
            Debug.LogWarning($"[UIManager] 关闭失败：界面 \"{key}\" 未打开");
            return;
        }

        CloseInfo(info, destroy);
    }

    /// <summary>
    /// 关闭当前层最顶部界面（用于返回键操作）
    /// </summary>
    /// <param name="layer">目标层级，默认 Normal</param>
    public void CloseCurrent(UILayer layer = UILayer.Normal)
    {
        if (!_stacks.TryGetValue(layer, out var stack) || stack.Count == 0)
        {
            Debug.Log($"[UIManager] {layer} 层栈为空，无法关闭");
            return;
        }

        string topKey = stack.Peek();
        Close(topKey);
    }

    /// <summary>
    /// 关闭某一层级的所有界面
    /// </summary>
    public void CloseLayer(UILayer layer)
    {
        var toClose = new List<string>();
        foreach (var kvp in _opened)
        {
            if (kvp.Value.Layer == layer)
                toClose.Add(kvp.Key);
        }

        foreach (var key in toClose)
            Close(key);
    }

    /// <summary>
    /// 关闭所有已打开的界面
    /// </summary>
    public void CloseAll()
    {
        var keys = new List<string>(_opened.Keys);
        foreach (var key in keys)
            Close(key);
    }

    /// <summary>
    /// 获取已打开的界面信息
    /// </summary>
    public UIInfo Get(string key)
    {
        _opened.TryGetValue(key, out var info);
        return info;
    }

    /// <summary>
    /// 判断指定界面是否已打开
    /// </summary>
    public bool IsOpen(string key)
    {
        return _opened.ContainsKey(key);
    }

    // ─── 内部实现 ───

    /// <summary>
    /// 打开界面的核心逻辑（与 MVVM 无关的部分）
    /// </summary>
    private UIInfo OpenInternal<TView>(object data) where TView : ViewBase
    {
        string key = typeof(TView).Name;

        // 1. 已打开 → 置顶并返回
        if (_opened.TryGetValue(key, out var existing))
        {
            BringToFront(existing);
            return existing;
        }

        // 2. 从缓存恢复
        if (_cached.TryGetValue(key, out var cached))
        {
            _cached.Remove(key);
            cached.View.Show();
            RegisterOpened(cached);
            DeliverData(cached.View, data);
            return cached;
        }

        // 3. 首次加载：从 UIConfig 获取地址和层级
        if (!UIConfig.Entries.TryGetValue(key, out var entry))
        {
            Debug.LogError($"[UIManager] 界面 \"{key}\" 未在 UIConfig 中注册");
            return null;
        }

        // 4. 异步加载 → 这里用同步等待（简化实现，后续可改 async）
        var parent = GetLayerRoot(entry.Layer);
        var instance = UILoader.LoadAsync(entry.Address, parent).GetAwaiter().GetResult();

        if (instance == null)
        {
            Debug.LogError($"[UIManager] 加载界面 \"{key}\" 失败，地址: {entry.Address}");
            return null;
        }

        // 5. 获取 ViewBase 组件
        var view = instance.GetComponent<TView>();
        if (view == null)
        {
            Debug.LogError($"[UIManager] Prefab \"{key}\" 上找不到 {typeof(TView).Name} 组件");
            UILoader.Release(instance);
            return null;
        }

        // 6. 构建 UIInfo
        var info = new UIInfo
        {
            Key = key,
            View = view,
            ViewModel = null,
            Layer = entry.Layer,
            Root = instance,
            Address = entry.Address
        };

        RegisterOpened(info);
        DeliverData(view, data);

        return info;
    }

    /// <summary>
    /// 注册到已打开列表和栈
    /// </summary>
    private void RegisterOpened(UIInfo info)
    {
        _opened[info.Key] = info;

        // Normal 层入栈
        if (info.Layer == UILayer.Normal)
        {
            if (!_stacks.ContainsKey(UILayer.Normal))
                _stacks[UILayer.Normal] = new Stack<string>();
            _stacks[UILayer.Normal].Push(info.Key);
        }
    }

    /// <summary>
    /// 关闭一个界面：解绑 ViewModel → 隐藏 → 缓存或销毁
    /// </summary>
    private void CloseInfo(UIInfo info, bool destroy)
    {
        // 1. 从已打开列表移除
        _opened.Remove(info.Key);

        // 2. 从栈中移除（如果是 Normal 层）
        if (info.Layer == UILayer.Normal && _stacks.TryGetValue(UILayer.Normal, out var stack))
        {
            // 重建栈（移除目标 key，保持其余顺序）
            var temp = new Stack<string>();
            while (stack.Count > 0)
            {
                string k = stack.Pop();
                if (k != info.Key) temp.Push(k);
            }
            while (temp.Count > 0) stack.Push(temp.Pop());
        }

        // 3. 解绑 ViewModel（软清理，不销毁 GameObject）
        info.View.Unbind();
        info.ViewModel = null;

        if (destroy)
        {
            // 4a. 直接销毁
            UILoader.Release(info.Root);
        }
        else
        {
            // 4b. 隐藏并缓存
            info.View.Hide();
            _cached[info.Key] = info;

            // 尝试显示栈顶界面
            TryShowPrevious(info.Layer);
        }
    }

    /// <summary>
    /// 将已打开的界面置顶（设为同层级最后一个渲染）
    /// </summary>
    private void BringToFront(UIInfo info)
    {
        if (info.Root != null && _layerRoots.TryGetValue(info.Layer, out var parent))
        {
            info.Root.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 关闭后尝试显示栈顶的下一个界面
    /// </summary>
    private void TryShowPrevious(UILayer layer)
    {
        if (layer != UILayer.Normal) return;
        if (!_stacks.TryGetValue(UILayer.Normal, out var stack) || stack.Count == 0) return;

        string topKey = stack.Peek();
        if (_opened.TryGetValue(topKey, out var topInfo))
        {
            topInfo.View.Show();
        }
    }

    /// <summary>
    /// 获取层级根节点
    /// </summary>
    private Transform GetLayerRoot(UILayer layer)
    {
        if (_layerRoots.TryGetValue(layer, out var root))
            return root;

        // 未找到时回退到自身
        Debug.LogWarning($"[UIManager] 层级 {layer} 根节点不存在，回退到 UIManager 根");
        return transform;
    }

    /// <summary>
    /// 传递数据给 View（如果实现了 IDataReceiver）
    /// </summary>
    private void DeliverData(ViewBase view, object data)
    {
        if (data != null && view is IDataReceiver receiver)
        {
            receiver.OnReceiveData(data);
        }
    }
}
