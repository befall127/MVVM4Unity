using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 代码生成工具类
/// 提供：创建脚本文件、解析已有脚本、组件排序
/// </summary>
public static class GenCodeUtil
{
    /// <summary>支持的基类列表</summary>
    public static Type[] supportBaseTypes = new Type[] { typeof(ViewBase) };

    /// <summary>默认生成规则（指向 DefaultViewRule）</summary>
    public static GenCodeRule DefaultRule => GenCodeRule.DefaultViewRule;

    /// <summary>
    /// 从 GameObject + ComponentItem 列表生成脚本并写入文件
    /// </summary>
    /// <param name="go">目标 GameObject</param>
    /// <param name="components">组件配置列表</param>
    /// <param name="rule">生成规则</param>
    /// <param name="overrideClassName">覆盖自动类名（不为空时忽略 rule.classNameSuffix）</param>
    /// <returns>生成的脚本路径</returns>
    public static string CreateScript(GameObject go, List<ComponentItem> components, GenCodeRule rule = null, string overrideClassName = null)
    {
        rule ??= DefaultRule;
        var className = !string.IsNullOrEmpty(overrideClassName)
            ? overrideClassName
            : go.name.Replace(" ", "") + rule.classNameSuffix;

        var coder = new UICoder
        {
            className = className,
            components = components,
            rule = rule
        };

        var scriptContent = coder.Compile();

#if UNITY_EDITOR
        var dir = Path.Combine(Application.dataPath, rule.outputDirectory);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var scriptPath = Path.Combine(dir, className + ".cs");

        // 规范化路径
        var assetRelativePath = Path.Combine("Assets", rule.outputDirectory, className + ".cs")
            .Replace("\\", "/");

        File.WriteAllText(scriptPath, scriptContent);
        AssetDatabase.Refresh();

        // 延迟添加组件
        if (rule.autoAddComponent && go != null)
        {
            EditorApplication.delayCall += () =>
            {
                if (go == null) return;
                var baseType = supportBaseTypes.Length > rule.baseTypeIndex
                    ? supportBaseTypes[rule.baseTypeIndex]
                    : supportBaseTypes[0];
                var type = baseType.Assembly.GetType(className);
                if (type != null && go.GetComponent(type) == null)
                    go.AddComponent(type);
            };
        }

        return assetRelativePath;
#else
        Debug.Log($"[GenCodeUtil] 生成的脚本仅在 Editor 下写入文件。内容：\n{scriptContent}");
        return null;
#endif
    }

    /// <summary>
    /// 仅生成代码文本，不写文件（用于预览）
    /// </summary>
    public static string GenerateCodePreview(string className, List<ComponentItem> components, GenCodeRule rule = null)
    {
        rule ??= DefaultRule;
        var coder = new UICoder
        {
            className = className,
            components = components,
            rule = rule
        };
        return coder.Compile();
    }

    /// <summary>
    /// 给组件排序（按类型名字母序）
    /// </summary>
    public static List<Component> SortComponent(GameObject target)
    {
        var comps = new List<Component>(target.GetComponents<Component>());
        comps.Sort((a, b) => a.GetType().Name.CompareTo(b.GetType().Name));
        return comps;
    }
}
