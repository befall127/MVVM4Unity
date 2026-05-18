using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Manager 代码生成器：读取 View 和 ViewModel 脚本，生成注入 Manager
/// </summary>
public static class ManagerGenerator
{
    /// <summary>
    /// 一对待注入的 View-ViewModel 组合
    /// </summary>
    public class PairInfo
    {
        public string viewScriptPath;
        public string viewModelScriptPath;
    }

    /// <summary>
    /// 解析 .cs 文件，提取第一个 public class 的类名
    /// </summary>
    public static string ExtractClassName(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var content = File.ReadAllText(filePath);
        var match = Regex.Match(content, @"public\s+class\s+(\w+)\s*:");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 从完整类名中去掉后缀（仅当类名以 suffix 结尾时去除）
    /// </summary>
    public static string StripSuffix(string className, string suffix)
    {
        if (string.IsNullOrEmpty(suffix)) return className;
        if (className.EndsWith(suffix))
            return className.Substring(0, className.Length - suffix.Length);
        return className;
    }

    /// <summary>
    /// 从多组 View-ViewModel 对生成单一 Manager 文件
    /// </summary>
    /// <param name="pairs">多组 View-ViewModel 对</param>
    /// <param name="rule">生成规则</param>
    /// <param name="managerClassName">类名（若为空则从第一对推导）</param>
    /// <returns>生成的 C# 代码，失败返回 null</returns>
    public static string Generate(IEnumerable<PairInfo> pairs, GenCodeRule rule, string managerClassName)
    {
        var pairList = new List<PairInfo>();
        foreach (var p in pairs)
        {
            if (p == null || string.IsNullOrEmpty(p.viewScriptPath) || string.IsNullOrEmpty(p.viewModelScriptPath))
                continue;
            pairList.Add(p);
        }
        if (pairList.Count == 0)
        {
            Debug.LogError("[ManagerGen] 没有有效的 View-ViewModel 对");
            return null;
        }

        var resolved = new List<ResolvedPair>();
        string fallbackPrefix = null;

        foreach (var p in pairList)
        {
            string viewClass = ExtractClassName(p.viewScriptPath);
            string vmClass   = ExtractClassName(p.viewModelScriptPath);

            if (string.IsNullOrEmpty(viewClass) || string.IsNullOrEmpty(vmClass))
            {
                Debug.LogWarning($"[ManagerGen] 跳过无效路径: {p.viewScriptPath} / {p.viewModelScriptPath}");
                continue;
            }

            string viewPrefix = StripSuffix(viewClass, GenCodeRule.DefaultViewRule.classNameSuffix);
            string vmPrefix   = StripSuffix(vmClass, GenCodeRule.DefaultViewModelRule.classNameSuffix);
            string prefix = viewPrefix == vmPrefix ? viewPrefix : viewPrefix;
            string varName = char.ToLower(prefix[0]) + prefix.Substring(1);

            resolved.Add(new ResolvedPair
            {
                viewClassName = viewClass,
                vmClassName   = vmClass,
                viewFieldName = $"{rule.fieldPrefix}{varName}View",
                vmFieldName   = $"{rule.fieldPrefix}{varName}ViewModel"
            });

            if (fallbackPrefix == null) fallbackPrefix = prefix;
        }

        if (resolved.Count == 0) return null;

        // 类名：优先用户指定，否则从第一对推导
        if (string.IsNullOrEmpty(managerClassName))
            managerClassName = fallbackPrefix + rule.classNameSuffix;

        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"public class {managerClassName} : {rule.baseClassName}");
        sb.AppendLine("{");

        // View 变量
        foreach (var r in resolved)
            sb.AppendLine($"    [SerializeField] private {r.viewClassName} {r.viewFieldName};");

        sb.AppendLine();

        // ViewModel 变量
        foreach (var r in resolved)
            sb.AppendLine($"    private {r.vmClassName} {r.vmFieldName};");

        sb.AppendLine("    void Start()");
        sb.AppendLine("    {");

        // 初始化 ViewModel
        foreach (var r in resolved)
            sb.AppendLine($"        {r.vmFieldName} = new {r.vmClassName}();");

        sb.AppendLine();

        // 注入
        foreach (var r in resolved)
            sb.AppendLine($"        {r.viewFieldName}.SetViewModel({r.vmFieldName});");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #region 辅助结构

    private class ResolvedPair
    {
        public string viewClassName;
        public string vmClassName;
        public string viewFieldName;
        public string vmFieldName;
    }

    #endregion
}
