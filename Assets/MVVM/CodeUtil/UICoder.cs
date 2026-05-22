using System.Text;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 代码构建器：将 ComponentItem 列表构建为完整的 C# 脚本
/// </summary>
public class UICoder
{
    public string className;
    public List<ComponentItem> components;
    public GenCodeRule rule;

    private StringBuilder _sb = new StringBuilder();
    private string _indent = "";

    /// <summary>
    /// 编译所有组件信息，生成完整脚本内容
    /// </summary>
    public string Compile()
    {
        _sb.Clear();
        GenUsings();
        GenClassHeader();
        GenFields();
        GenStart();
        GenOnBinding();
        GenClassFooter();
        return _sb.ToString();
    }

    private void GenUsings()
    {
        _sb.AppendLine("using UnityEngine;");
        _sb.AppendLine("using UnityEngine.UI;");
        _sb.AppendLine("using System;");
        _sb.AppendLine();
    }

    private void GenClassHeader()
    {
        string baseClass = rule != null && !string.IsNullOrEmpty(rule.baseClassName)
            ? rule.baseClassName : "ViewBase";
        _sb.AppendLine($"public class {className} : {baseClass}");
        _sb.AppendLine("{");
    }

    private void GenFields()
    {
        if (components == null) return;

        var seen = new HashSet<string>();
        foreach (var c in components)
        {
            string fieldType = GetFieldType(c);
            string fieldName = $"{rule.fieldPrefix}{c.name}";
            if (seen.Add(fieldName))
                _sb.AppendLine($"    [SerializeField] private {fieldType} {fieldName};");
        }
        _sb.AppendLine();
    }

    private string GetFieldType(ComponentItem c)
    {
        // 对于 Custom 类型，使用扫描到的真实类型名
        if (c.componentType == UIComponentType.Custom && !string.IsNullOrEmpty(c.componentTypeName))
            return c.componentTypeName;

        return c.componentType switch
        {
            UIComponentType.InputField => "InputField",
            UIComponentType.Button => "Button",
            UIComponentType.Text => "Text",
            UIComponentType.Image => "Image",
            UIComponentType.Toggle => "Toggle",
            UIComponentType.Slider => "Slider",
            _ => "MonoBehaviour"
        };
    }

    /// <summary>
    /// 生成 Awake() + InitComponents()：惰性初始化，字段为空时自动 transform.Find
    /// Awake 在对象激活时优先执行，OnBinding 内兜底调用确保未激活时 SetViewModel 也能正常工作
    /// </summary>
    private void GenStart()
    {
        if (components == null || components.Count == 0) return;

        var seen = new HashSet<string>();
        bool hasAutoFind = false;

        foreach (var c in components)
        {
            string fieldName = $"{rule.fieldPrefix}{c.name}";
            if (!seen.Add(fieldName)) continue;
            if (string.IsNullOrEmpty(c.autoFindPath)) continue;
            hasAutoFind = true;
            break;
        }

        seen.Clear();
        _sb.AppendLine("    // 将脚本挂载到被扫描对象上，以确保 transform.Find 能正确定位子物体");
        _sb.AppendLine("    private void Awake()");
        _sb.AppendLine("    {");
        _sb.AppendLine("        InitComponents();");
        _sb.AppendLine("    }");
        _sb.AppendLine();
        _sb.AppendLine("    private void InitComponents()");
        _sb.AppendLine("    {");

        if (hasAutoFind)
        {
            foreach (var c in components)
            {
                string fieldName = $"{rule.fieldPrefix}{c.name}";
                if (!seen.Add(fieldName)) continue;
                if (string.IsNullOrEmpty(c.autoFindPath)) continue;

                string typeName = GetFieldType(c);
                string findPath = c.autoFindPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
                _sb.AppendLine($"        if ({fieldName} == null)");
                if (c.autoFindPath == ".")
                    _sb.AppendLine($"            {fieldName} = GetComponent<{typeName}>();");
                else
                    _sb.AppendLine($"            {fieldName} = transform.Find(\"{findPath}\").GetComponent<{typeName}>();");
            }
        }

        _sb.AppendLine("    }");
        _sb.AppendLine();
    }

    private void GenOnBinding()
    {
        _sb.AppendLine("    protected override void OnBinding()");
        _sb.AppendLine("    {");
        _sb.AppendLine("        InitComponents();");

        if (components != null)
        {
            foreach (var c in components)
            {
                bool hasViewItems = c.viewItems != null && c.viewItems.Count > 0;
                bool hasEventItems = c.eventItems != null && c.eventItems.Count > 0;

                if (c.IsInputType && rule.enableTwoWayBinding && !c.forceOneWay)
                    GenTwoWayBinding(c);
                else if (c.IsDisplayType || (c.IsInputType && c.forceOneWay))
                    GenOneWayBinding(c);
                else if (c.componentType == UIComponentType.Button || hasEventItems)
                    GenEventBindings(c);
                else if (hasViewItems && c.componentType == UIComponentType.Custom)
                {
                    // Custom 类型但有 viewItems：按单向绑定生成（没有已知的 onChange 事件，Wrap 仅静态包装）
                    _sb.AppendLine("        // 组件属性无onChange事件，仅作单向绑定处理");
                    GenOneWayBinding(c);
                }
                else if (c.componentType == UIComponentType.Custom)
                    GenCustomPlaceholder(c);
            }
        }

        _sb.AppendLine("    }");
    }

    /// <summary>
    /// 生成双向绑定代码：Wrap + BindTwoWay
    /// </summary>
    private void GenTwoWayBinding(ComponentItem c)
    {
        string fieldName = $"{rule.fieldPrefix}{c.name}";

        if (c.viewItems != null && c.viewItems.Count > 0)
        {
            foreach (var vi in c.viewItems)
            {
                // 根据用户实际选择的属性名拼表达式，不再由 componentType 硬编码
                string valueExpr = $"{fieldName}.{vi.bindingTarget}";
                string eventExpr = GetChangeEventForProperty(fieldName, c.componentType, vi.bindingTarget);
                string typeName = CSharpTypeName(vi.bindingTargetType);
                string wrapCall = !string.IsNullOrEmpty(eventExpr)
                    ? $"_binder.Wrap<{typeName}>({valueExpr}, {eventExpr})"
                    : $"_binder.Wrap<{typeName}>({valueExpr})";

                _sb.AppendLine($"        // 双向绑定：{c.componentType}.{vi.bindingTarget} ↔ ViewModel.{vi.bindingSource}");
                _sb.AppendLine($"        _binder.BindTwoWay(");
                _sb.AppendLine($"            {wrapCall},");
                _sb.AppendLine($"            _viewModel.GetBindableProperty<{typeName}>(\"{vi.bindingSource}\"));");
            }
        }
        else
        {
            string typeName = CSharpTypeName(c.GetValueType());
            _sb.AppendLine($"        // TODO: 请补充 {c.componentType} 的绑定目标 ViewModel 属性名");
            _sb.AppendLine($"        // _binder.BindTwoWay(_binder.Wrap<{typeName}>({fieldName}.属性, ...), _viewModel.GetBindableProperty<{typeName}>(\"属性名\"));");
        }
    }

    /// <summary>
    /// 生成单向绑定代码：RegisterMember
    /// </summary>
    private void GenOneWayBinding(ComponentItem c)
    {
        string fieldName = $"{rule.fieldPrefix}{c.name}";

        if (c.viewItems != null && c.viewItems.Count > 0)
        {
            foreach (var vi in c.viewItems)
            {
                string typeName = CSharpTypeName(vi.bindingTargetType);
                string setter = $"{fieldName}.{vi.bindingTarget} = val;";

                _sb.AppendLine($"        // 单向绑定：ViewModel.{vi.bindingSource} → {c.componentType}");
                _sb.AppendLine($"        _binder.RegisterMember<{typeName}>(");
                _sb.AppendLine($"            \"{fieldName}\",");
                _sb.AppendLine($"            \"{vi.bindingSource}\",");
                _sb.AppendLine($"            (val) => {{ {setter} }});");
            }
        }
        else
        {
            _sb.AppendLine($"        // TODO: 请补充 {c.componentType} 的绑定目标 ViewModel 属性名");
        }
    }

    /// <summary>
    /// 生成事件绑定代码：RegisterEvent
    /// </summary>
    private void GenEventBindings(ComponentItem c)
    {
        string fieldName = $"{rule.fieldPrefix}{c.name}";

        if (c.eventItems != null && c.eventItems.Count > 0)
        {
            foreach (var ei in c.eventItems)
            {
                if (!ei.runtime)
                {
                    if (!string.IsNullOrEmpty(ei.captureParamExpr))
                    {
                        // 带参数捕获：RegisterEvent<T>(UnityEvent, string, Func<T>)
                        _sb.AppendLine($"        // 事件绑定：{fieldName}.{ei.bindingTarget} → ViewModel.{ei.bindingSource}（捕获参数：{ei.captureParamExpr}）");
                        _sb.AppendLine($"        _binder.RegisterEvent<string>({fieldName}.{ei.bindingTarget}, \"{ei.bindingSource}\", () => {ei.captureParamExpr});");
                    }
                    else
                    {
                        // 无参：RegisterEvent(UnityEvent, string)
                        _sb.AppendLine($"        // 事件绑定：{fieldName}.{ei.bindingTarget} → ViewModel.{ei.bindingSource}");
                        _sb.AppendLine($"        _binder.RegisterEvent({fieldName}.{ei.bindingTarget}, \"{ei.bindingSource}\");");
                    }
                }
            }
        }
        else
        {
            _sb.AppendLine($"        // TODO: 请补充 {fieldName} 的点击事件绑定目标");
            _sb.AppendLine($"        // _binder.RegisterEvent({fieldName}.onClick, \"命令名\");");
        }
    }

    private void GenCustomPlaceholder(ComponentItem c)
    {
        string fieldName = $"{rule.fieldPrefix}{c.name}";
        _sb.AppendLine($"        // TODO: Custom 类型 {fieldName} 需手动编写绑定");
    }

    /// <summary>
    /// 根据控件类型和属性名，查找对应的 UnityEvent（如 text → onValueChanged）
    /// 找不到则返回 null，表示该属性没有自动变更通知
    /// </summary>
    private string GetChangeEventForProperty(string fieldName, UIComponentType compType, string propertyName)
    {
        switch (compType)
        {
            case UIComponentType.InputField:
                if (propertyName == "text") return $"{fieldName}.onValueChanged";
                break;
            case UIComponentType.Toggle:
                if (propertyName == "isOn") return $"{fieldName}.onValueChanged";
                break;
            case UIComponentType.Slider:
                if (propertyName == "value") return $"{fieldName}.onValueChanged";
                break;
        }
        return null;
    }

    private string CSharpTypeName(Type t)
    {
        if (t == typeof(string)) return "string";
        if (t == typeof(int)) return "int";
        if (t == typeof(float)) return "float";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(Sprite)) return "Sprite";
        return "object";
    }

    private void GenClassFooter()
    {
        _sb.AppendLine("}");
    }
}
