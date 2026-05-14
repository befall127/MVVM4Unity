using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

/// <summary>
/// ViewModel 骨架生成器：读取 View 层 OnBinding() 代码，
/// 自动生成对应的 ViewModel 类（数据绑定 → SetValue 初始值，事件绑定 → 空 Action 占位）
/// </summary>
public static class ViewModelGenerator
{
    /// <summary>
    /// 从 .cs 文件路径解析 OnBinding() 并生成 ViewModel 代码
    /// </summary>
    public static string Generate(string viewScriptPath, out string viewModelClassName)
    {
        viewModelClassName = "";

        if (!File.Exists(viewScriptPath))
        {
            Debug.LogError($"[ViewModelGen] 文件不存在: {viewScriptPath}");
            return null;
        }

        var content = File.ReadAllText(viewScriptPath);

        // 提取类名
        var classMatch = Regex.Match(content, @"public\s+class\s+(\w+)\s*:\s*\w+Base");
        if (!classMatch.Success)
        {
            Debug.LogError("[ViewModelGen] 无法从文件中提取 ViewBase 子类类名");
            return null;
        }

        string viewClassName = classMatch.Groups[1].Value;
        viewModelClassName = DeriveViewModelName(viewClassName);

        // 提取 OnBinding 方法体
        var methodBody = ExtractOnBindingBody(content);
        if (string.IsNullOrEmpty(methodBody))
        {
            Debug.LogError("[ViewModelGen] 未找到 OnBinding() 方法体");
            return null;
        }

        // 解析绑定
        var dataBindings = ParseDataBindings(methodBody);
        var noParamEvents = ParseNoParamEvents(methodBody);
        var paramEvents = ParseParamEvents(methodBody);

        return CompileViewModel(viewModelClassName, dataBindings, noParamEvents, paramEvents);
    }

    #region ============ 解析 ============

    private static string ExtractOnBindingBody(string content)
    {
        // 找到 protected override void OnBinding() 到下一个同级方法之间
        var match = Regex.Match(content, @"override\s+void\s+OnBinding\s*\(\s*\)\s*\{([^}]*(\{[^}]*\}[^}]*)*)\s*\}", RegexOptions.Singleline);
        // 简化：匹配 OnBinding 之后的代码块
        var start = content.IndexOf("OnBinding");
        if (start < 0) return null;

        // 找到方法开始的 {
        var braceStart = content.IndexOf('{', start);
        if (braceStart < 0) return null;

        // 统计大括号深度找到方法结束
        int depth = 0;
        for (int i = braceStart; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return content.Substring(braceStart, i - braceStart + 1);
            }
        }
        return null;
    }

    /// <summary>
    /// 解析数据绑定：
    /// 1. BindTwoWay 中的 GetBindableProperty&lt;T&gt;("Name")
    /// 2. RegisterMember&lt;T&gt;("uiPath", "Name", ...)
    /// </summary>
    private static List<BindingItem> ParseDataBindings(string body)
    {
        var list = new List<BindingItem>();
        var seen = new HashSet<string>();

        // 方式1：BindTwoWay → _viewModel.GetBindableProperty<T>("Name")
        var gbpMatches = Regex.Matches(body, @"GetBindableProperty<([^>]+)>\s*\(\s*""([^""]+)""\s*\)");
        foreach (Match m in gbpMatches)
        {
            string typeName = m.Groups[1].Value;
            string propName = m.Groups[2].Value;
            if (seen.Add(propName))
                list.Add(new BindingItem { typeName = typeName, name = propName, isEvent = false });
        }

        // 方式2：RegisterMember<T>("uiPath", "Name", ...) — 第二个参数是 ViewModel 属性名
        var rmMatches = Regex.Matches(body, @"RegisterMember<([^>]+)>\s*\(\s*""[^""]*""\s*,\s*""([^""]+)""\s*,");
        foreach (Match m in rmMatches)
        {
            string typeName = m.Groups[1].Value;
            string propName = m.Groups[2].Value;
            if (seen.Add(propName))
                list.Add(new BindingItem { typeName = typeName, name = propName, isEvent = false });
        }

        return list;
    }

    /// <summary>
    /// 解析无参事件：RegisterEvent(..., "Name") — 没有尖括号泛型参数的
    /// </summary>
    private static List<BindingItem> ParseNoParamEvents(string body)
    {
        var list = new List<BindingItem>();

        // 先提取所有 RegisterEvent 调用行（去掉带尖括号的泛型版本）
        // 匹配：RegisterEvent( 而不是 RegisterEvent<
        var matches = Regex.Matches(body, @"RegisterEvent\s*\(\s*[^,]+,\s*""([^""]+)""\s*\)");
        var paramEventNames = new HashSet<string>();

        // 已被参数事件占用的名字不再生成无参版本
        var paramMatches = Regex.Matches(body, @"RegisterEvent<[^>]+>\s*\(\s*[^,]+,\s*""([^""]+)""\s*,");
        foreach (Match m in paramMatches)
            paramEventNames.Add(m.Groups[1].Value);

        var seen = new HashSet<string>();
        foreach (Match m in matches)
        {
            string name = m.Groups[1].Value;
            if (paramEventNames.Contains(name)) continue;
            if (seen.Add(name))
                list.Add(new BindingItem { typeName = "Action", name = name, isEvent = true, hasParam = false });
        }
        return list;
    }

    /// <summary>
    /// 解析有参事件：RegisterEvent&lt;T&gt;(..., "Name", ...)
    /// </summary>
    private static List<BindingItem> ParseParamEvents(string body)
    {
        var list = new List<BindingItem>();
        var matches = Regex.Matches(body, @"RegisterEvent<([^>]+)>\s*\(\s*[^,]+,\s*""([^""]+)""\s*,");

        var seen = new HashSet<string>();
        foreach (Match m in matches)
        {
            string typeName = m.Groups[1].Value;
            string name = m.Groups[2].Value;
            if (seen.Add(name))
                list.Add(new BindingItem { typeName = typeName, name = name, isEvent = true, hasParam = true });
        }
        return list;
    }

    #endregion

    #region ============ 编译 ============

    private static string CompileViewModel(string className, List<BindingItem> dataBindings,
        List<BindingItem> noParamEvents, List<BindingItem> paramEvents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"public class {className} : ViewModelBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public {className}()");
        sb.AppendLine("    {");

        // 数据绑定 → SetValue，连续紧凑无空行
        foreach (var db in dataBindings)
        {
            string defVal = GetDefaultValue(db.typeName);
            sb.AppendLine($"        SetValue(\"{db.name}\", {defVal});");
        }

        bool hasEvents = (noParamEvents != null && noParamEvents.Count > 0) ||
                         (paramEvents != null && paramEvents.Count > 0);
        if (hasEvents && dataBindings.Count > 0)
            sb.AppendLine(); // 数据绑定与事件之间空一行

        // 无参事件 → Action 占位，事件之间空一行
        if (noParamEvents != null && noParamEvents.Count > 0)
        {
            for (int i = 0; i < noParamEvents.Count; i++)
            {
                if (i > 0) sb.AppendLine();
                var ev = noParamEvents[i];
                sb.AppendLine($"        SetValue(\"{ev.name}\", new Action(() =>");
                sb.AppendLine("        {");
                sb.AppendLine($"            // TODO: 实现 {ev.name} 逻辑");
                sb.AppendLine("        }));");
            }
        }

        // 有参事件 → Action<T> 占位，事件之间空一行
        if (paramEvents != null && paramEvents.Count > 0)
        {
            if (noParamEvents != null && noParamEvents.Count > 0)
                sb.AppendLine(); // 无参与有参之间空行
            else if (dataBindings.Count > 0)
                sb.AppendLine();

            for (int i = 0; i < paramEvents.Count; i++)
            {
                if (i > 0) sb.AppendLine();
                var ev = paramEvents[i];
                string argName = "arg";
                sb.AppendLine($"        SetValue(\"{ev.name}\", new Action<{ev.typeName}>(({argName}) =>");
                sb.AppendLine("        {");
                sb.AppendLine($"            // TODO: 实现 {ev.name} 逻辑，参数类型为 {ev.typeName}");
                sb.AppendLine("        }));");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetDefaultValue(string typeName)
    {
        return typeName switch
        {
            "string" => "\"\"",
            "int" => "0",
            "float" => "0f",
            "bool" => "false",
            "double" => "0d",
            "long" => "0L",
            "byte" => "0",
            "short" => "0",
            "char" => "'\\0'",
            _ => "null"
        };
    }

    /// <summary>
    /// 从 View 类名推导 ViewModel 类名（LoginPanel → LoginViewModel）
    /// </summary>
    private static string DeriveViewModelName(string viewClassName)
    {
        // 移除 Generate / Panel 等后缀
        var baseName = viewClassName
            .Replace("Generate", "")
            .Replace("Panel", "")
            .Replace("View", "");
        return baseName + "ViewModel";
    }

    #endregion

    #region ============ 数据类 ============

    private class BindingItem
    {
        public string typeName;
        public string name;
        public bool isEvent;
        public bool hasParam;
    }

    #endregion
}
