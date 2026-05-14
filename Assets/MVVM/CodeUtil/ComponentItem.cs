using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 控件类型枚举，决定代码生成器输出哪种绑定代码
/// </summary>
public enum UIComponentType
{
    Text,           // 纯展示文本（单向：VM → UI）
    Image,          // 纯展示图片（单向：VM → UI）
    InputField,     // 输入框（双向：UI ↔ VM）
    Button,         // 按钮（事件：UI → VM）
    Toggle,         // 开关（双向：UI ↔ VM）
    Slider,         // 滑动条（双向：UI ↔ VM）
    Custom          // 自定义（需手动补充绑定细节）
}

/// <summary>
/// 组件信息项：描述一个 UI 控件及其与 ViewModel 的绑定配置
/// </summary>
[System.Serializable]
public class ComponentItem
{
    /// <summary>控件字段名（如 m_UserNameInput）</summary>
    public string name;

    /// <summary>对应的 GameObject</summary>
    public GameObject target;

    /// <summary>相对于扫描根节点的路径（如 "InputUseName"、"Panel/Text(Legacy)"），用于 Start() 自动查找</summary>
    public string autoFindPath;

    /// <summary>控件类型</summary>
    public UIComponentType componentType;

    /// <summary>组件的真实 C# 类型名（如 "CanvasGroup"、"ScrollRect"），用于变量声明和 GetComponent</summary>
    public string componentTypeName;

    /// <summary>物体上的组件列表（用于编辑器查找）</summary>
    public List<Component> components;

    /// <summary>数据绑定列表（Text、Image、InputField 等）</summary>
    public List<BindingShow> viewItems = new List<BindingShow>();

    /// <summary>事件绑定列表（Button.onClick 等）</summary>
    public List<BindingEvent> eventItems = new List<BindingEvent>();

    /// <summary>强制单向绑定（即使控件是输入类型，也只生成 RegisterMember）</summary>
    public bool forceOneWay;

    /// <summary>是否为输入型控件，需要生成双向绑定</summary>
    public bool IsInputType =>
        componentType == UIComponentType.InputField ||
        componentType == UIComponentType.Toggle ||
        componentType == UIComponentType.Slider;

    /// <summary>是否为纯展示控件，只需要单向绑定</summary>
    public bool IsDisplayType =>
        componentType == UIComponentType.Text ||
        componentType == UIComponentType.Image;

    /// <summary>获取控件值的数据类型</summary>
    public Type GetValueType()
    {
        return componentType switch
        {
            UIComponentType.InputField => typeof(string),
            UIComponentType.Text => typeof(string),
            UIComponentType.Toggle => typeof(bool),
            UIComponentType.Slider => typeof(float),
            UIComponentType.Image => typeof(Sprite),
            _ => typeof(string)
        };
    }

    /// <summary>获取 UI 取值表达式（如 m_Input.text）</summary>
    public string GetValueExpression()
    {
        return componentType switch
        {
            UIComponentType.InputField => $"m_{name}.text",
            UIComponentType.Toggle => $"m_{name}.isOn",
            UIComponentType.Slider => $"m_{name}.value",
            _ => $"m_{name}.待补充值" // 需手动补充
        };
    }

    /// <summary>获取 UI 变更事件表达式（如 m_Input.onValueChanged）</summary>
    //public string GetChangeEventExpression()
    //{
    //    return componentType switch
    //    {
    //        UIComponentType.InputField => $"m_{name}.onValueChanged",
    //        UIComponentType.Toggle => $"m_{name}.onValueChanged",
    //        UIComponentType.Slider => $"m_{name}.onValueChanged",
    //        UIComponentType.Button => $"m_{name}.onClick",
    //        _ => $"m_{name}.onValueChanged"
    //    };
    //}
}

/// <summary>
/// 数据绑定信息：描述一个 UI 属性与 ViewModel 属性的映射
/// </summary>
[System.Serializable]
public class BindingShow
{
    /// <summary>ViewModel 属性名（如 "UserName"）</summary>
    public string bindingSource;

    /// <summary>UI 属性名（如 "text"、"sprite"）</summary>
    public string bindingTarget;

    /// <summary>绑定值的类型</summary>
    public Type bindingTargetType;
}

/// <summary>
/// 事件绑定信息：描述一个 UI 事件与 ViewModel 命令的映射
/// </summary>
[System.Serializable]
public class BindingEvent
{
    /// <summary>是否由 ViewModel 动态注册</summary>
    public bool runtime;

    /// <summary>ViewModel 命令/事件名</summary>
    public string bindingSource;

    /// <summary>UI 事件（如 "onClick"）</summary>
    public string bindingTarget;

    /// <summary>事件参数类型</summary>
    public Type bindingTargetType;

    /// <summary>可选：捕获参数的表达式（如 "m_InputPassword.text"），不为空时生成 RegisterEvent&lt;T&gt;</summary>
    public string captureParamExpr;
}
