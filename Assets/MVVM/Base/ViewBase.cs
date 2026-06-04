using UnityEngine;

public class ViewBase : MonoBehaviour
{
    protected ViewModelBase _viewModel;
    protected Binder _binder;
    private bool _unbinded;

    /// <summary>
    /// 设置 ViewModel 并执行绑定
    /// </summary>
    public void SetViewModel(ViewModelBase viewModel)
    {
        _unbinded = false;
        _viewModel = viewModel;
        _binder = new Binder(this, viewModel);

        // 执行绑定
        OnBinding();
    }

    /// <summary>
    /// 子类重写：在此方法中编写绑定逻辑（调用 _binder.RegisterMember / RegisterEvent）
    /// </summary>
    protected virtual void OnBinding()
    {
    }

    /// <summary>
    /// 解除绑定逻辑，子类可重写以添加额外清理操作
    /// </summary>
    protected virtual void OnUnbinding()
    {
    }

    /// <summary>
    /// 界面显示时调用（从缓存恢复或首次打开），子类可重写添加动画等逻辑
    /// </summary>
    protected virtual void OnShow()
    {
    }

    /// <summary>
    /// 界面隐藏时调用（关闭但不销毁），子类可重写添加动画等逻辑
    /// </summary>
    protected virtual void OnHide()
    {
    }

    /// <summary>
    /// 内部方法：激活 GameObject 并触发 OnShow，由 UIManager 调用
    /// </summary>
    internal void Show()
    {
        gameObject.SetActive(true);
        OnShow();
    }

    /// <summary>
    /// 内部方法：隐藏 GameObject 并触发 OnHide，由 UIManager 调用
    /// </summary>
    internal void Hide()
    {
        OnHide();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 内部方法：解除绑定并清理 ViewModel（不销毁 GameObject），由 UIManager 缓存关闭时调用
    /// </summary>
    internal void Unbind()
    {
        if (_unbinded) return;
        _unbinded = true;

        OnUnbinding();
        _binder?.UnbindAll();
        _binder = null;
        _viewModel?.Dispose();
        _viewModel = null;
    }

    /// <summary>
    /// 销毁时自动解绑，防止内存泄漏
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_unbinded) return;
        _unbinded = true;

        OnUnbinding();
        _binder?.UnbindAll();
        _binder = null;
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
