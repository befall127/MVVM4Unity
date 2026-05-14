using UnityEngine;

public class ViewBase : MonoBehaviour
{
    protected ViewModelBase _viewModel;
    protected Binder _binder;

    /// <summary>
    /// 设置 ViewModel 并执行绑定
    /// </summary>
    public void SetViewModel(ViewModelBase viewModel)
    {
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
    /// 销毁时自动解绑，防止内存泄漏
    /// </summary>
    protected virtual void OnDestroy()
    {
        // 先通知子类解绑
        OnUnbinding();

        // 再清理所有 Binder 中注册的绑定
        _binder?.UnbindAll();
        _binder = null;

        // 清理 ViewModel 数据
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
