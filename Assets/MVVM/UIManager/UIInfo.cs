using UnityEngine;

/// <summary>
/// 运行时界面信息容器，记录一个已打开界面的所有关联数据
/// </summary>
public class UIInfo
{
    /// <summary>界面标识（如 "LoginPanel"）</summary>
    public string Key;

    /// <summary>View 组件引用</summary>
    public ViewBase View;

    /// <summary>ViewModel 引用（非 MVVM UI 时为 null）</summary>
    public ViewModelBase ViewModel;

    /// <summary>所在层级</summary>
    public UILayer Layer;

    /// <summary>根 GameObject</summary>
    public GameObject Root;

    /// <summary>Addressables 加载地址</summary>
    public string Address;
}
