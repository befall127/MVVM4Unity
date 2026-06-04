/// <summary>
/// UI 层级枚举，决定界面的渲染顺序和管理策略
/// </summary>
public enum UILayer
{
    /// <summary>普通界面，支持栈管理（可返回上一个）</summary>
    Normal = 100,

    /// <summary>弹窗，覆盖在 Normal 之上，不影响栈</summary>
    Popup = 200,

    /// <summary>提示信息，短暂显示后自动消失</summary>
    Toast = 300,

    /// <summary>加载界面，最高层级</summary>
    Loading = 400,
}
