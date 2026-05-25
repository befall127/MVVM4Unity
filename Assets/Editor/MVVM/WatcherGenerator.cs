using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using MVVM.Editor;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Watcher 代码生成器：根据组件类型和属性名，生成轮询式数据变化监听脚本
/// 支持自定义输出目录、轮询间隔等配置
/// </summary>
public static class WatcherGenerator
{
    /// <summary>
    /// 从 Watcher 组件列表中获取当前选中的组件
    /// </summary>
    public static ScannedComponent GetSelectedWatcherComp(int index, List<ScannedComponent> comps)
    {
        if (index < 0 || index >= comps.Count) return null;
        return comps[index];
    }

    /// <summary>
    /// 构建 Watcher 代码：多属性轮询监听单一 Unity 组件
    /// </summary>
    /// <param name="gameObjectName">组件所属 GameObject 名（用于文件名和 Pool Key 前缀去重）</param>
    /// <param name="selectedCompIndex">选中的组件索引</param>
    /// <param name="comps">扫描到的组件列表</param>
    /// <param name="properties">要监听的属性列表</param>
    /// <returns>生成的 C# 代码，失败返回 null</returns>
    public static string BuildWatcherCode(string gameObjectName, int selectedCompIndex,
        List<ScannedComponent> comps, List<WatcherPropertyEntry> properties)
    {
        var sc = GetSelectedWatcherComp(selectedCompIndex, comps);
        if (sc == null) return null;

        var activeProps = new List<(int idx, string name, System.Type type)>();
        foreach (var entry in properties)
        {
            if (entry.sourcePropIndex < 0 || entry.sourcePropIndex >= sc.properties.Count)
                continue;
            var prop = sc.properties[entry.sourcePropIndex];
            string fieldName = string.IsNullOrEmpty(entry.customFieldName) ? prop.Name : entry.customFieldName;
            activeProps.Add((entry.sourcePropIndex, fieldName, prop.ValueType));
        }
        if (activeProps.Count == 0) return null;

        string compType = sc.component.GetType().Name;
        string prefix = string.IsNullOrEmpty(gameObjectName) ? compType : $"{gameObjectName}_{compType}";
        string className = $"{prefix}Watcher";

        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// 自动生成：轮询 {gameObjectName}.{compType} 属性变化，同步到 BindablePropertyPool");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[RequireComponent(typeof({compType}))]");
        sb.AppendLine($"public class {className} : MonoBehaviour");
        sb.AppendLine("{");
        sb.AppendLine($"    private {compType} _target;");
        sb.AppendLine();

        // BindableProperty 变量
        foreach (var ap in activeProps)
            sb.AppendLine($"    public BindableProperty<{ap.type.Name}> m_{ap.name} = new BindableProperty<{ap.type.Name}>();");

        sb.AppendLine();

        // 值快照
        foreach (var ap in activeProps)
            sb.AppendLine($"    private {ap.type.Name} _last_{ap.name};");

        sb.AppendLine();

        // Pool Key 常量（含 GameObject 名称前缀防重名）
        foreach (var ap in activeProps)
            sb.AppendLine($"    public const string POOL_KEY_{ap.name} = \"{gameObjectName}_{compType}.{ap.name}\";");

        sb.AppendLine();
        sb.AppendLine("    [SerializeField] private float _checkInterval = 0.1f;");
        sb.AppendLine("    private float _timer;");
        sb.AppendLine();
        sb.AppendLine("    void Start()");
        sb.AppendLine("    {");
        sb.AppendLine($"        _target = GetComponent<{compType}>();");
        foreach (var ap in activeProps)
        {
            sb.AppendLine($"        m_{ap.name}.Value = _target.{ap.name};");
            sb.AppendLine($"        _last_{ap.name} = _target.{ap.name};");
        }
        sb.AppendLine();
        foreach (var ap in activeProps)
            sb.AppendLine($"        m_{ap.name}.AddToPool(POOL_KEY_{ap.name}, m_{ap.name}.Value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    void Update()");
        sb.AppendLine("    {");
        sb.AppendLine("        _timer += Time.deltaTime;");
        sb.AppendLine("        if (_timer < _checkInterval) return;");
        sb.AppendLine("        _timer = 0;");
        sb.AppendLine();
        foreach (var ap in activeProps)
        {
            sb.AppendLine($"        if (!Equals(_target.{ap.name}, _last_{ap.name}))");
            sb.AppendLine("        {");
            sb.AppendLine($"            _last_{ap.name} = _target.{ap.name};");
            sb.AppendLine($"            m_{ap.name}.Value = _target.{ap.name};");
            sb.AppendLine("        }");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
    /// <summary>
    /// 一条 Watcher 生成配置
    /// </summary>
    public class WatcherConfig
    {
        /// <summary>Unity 组件类型名，如 "CanvasRenderer"</summary>
        public string componentType;
        /// <summary>监听的属性名，如 "color"</summary>
        public string propertyName;
        /// <summary>属性的 C# 类型名，如 "Color"、"float"、"bool"</summary>
        public string propertyType;
        /// <summary>Pool 键名（为空时自动生成为 {ComponentType}.{PropertyName}）</summary>
        public string poolKey;
        /// <summary>轮询间隔秒数（为 0 时使用模板默认值 0.1f）</summary>
        public float checkInterval;
        /// <summary>输出目录（相对于 Assets，默认 "MVVM/Generated"）</summary>
        public string outputDirectory = "MVVM/Generated";
    }

    /// <summary>
    /// 根据配置生成 Watcher 代码文本
    /// </summary>
    public static string GenerateCode(WatcherConfig config)
    {
        string poolKey = string.IsNullOrEmpty(config.poolKey)
            ? $"{config.componentType}.{config.propertyName}"
            : config.poolKey;

        string code = WatcherTemplate.Template
            .Replace(WatcherTemplate.PLACEHOLDER_COMPONENT, config.componentType)
            .Replace(WatcherTemplate.PLACEHOLDER_PROPERTY, config.propertyName)
            .Replace(WatcherTemplate.PLACEHOLDER_PROPTYPE, config.propertyType)
            .Replace(WatcherTemplate.PLACEHOLDER_POOLKEY, poolKey);

        // 可选：覆盖轮询间隔
        if (config.checkInterval > 0f)
        {
            code = code.Replace("0.1f", $"{config.checkInterval}f");
        }

        return code;
    }

    /// <summary>
    /// 根据配置生成 Watcher 代码文本（字符串参数版本，便于从 UI 调用）
    /// </summary>
    public static string GenerateCode(string componentType, string propertyName, string propertyType,
        string poolKey = null, float checkInterval = 0f)
    {
        return GenerateCode(new WatcherConfig
        {
            componentType = componentType,
            propertyName = propertyName,
            propertyType = propertyType,
            poolKey = poolKey,
            checkInterval = checkInterval
        });
    }

    /// <summary>
    /// 批量生成 Watcher 脚本并写入文件
    /// </summary>
    /// <param name="configs">配置列表</param>
    /// <returns>成功生成的文件路径列表</returns>
    public static List<string> GenerateAndSave(List<WatcherConfig> configs)
    {
        var generated = new List<string>();
#if UNITY_EDITOR
        foreach (var config in configs)
        {
            string code = GenerateCode(config);
            string className = $"{config.componentType}Watcher";
            string dir = Path.Combine(Application.dataPath, config.outputDirectory);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string filePath = Path.Combine(dir, className + ".cs");
            File.WriteAllText(filePath, code);
            generated.Add(filePath);
        }
        AssetDatabase.Refresh();
#endif
        return generated;
    }

    /// <summary>
    /// 从已扫描的组件列表生成 Watcher 配置
    /// 为每个缺少 OnValueChanged 事件的属性生成一条配置
    /// </summary>
    /// <param name="components">已扫描的 ComponentItem 列表</param>
    /// <returns>WatcherConfig 列表</returns>
    public static List<WatcherConfig> BuildConfigsFromComponents(List<ComponentItem> components)
    {
        var configs = new List<WatcherConfig>();
        var seen = new HashSet<string>();

        foreach (var item in components)
        {
            if (item.viewItems == null) continue;
            foreach (var vi in item.viewItems)
            {
                // 跳过已知有 OnValueChanged 事件的属性组合
                if (HasBuiltInEvent(item.componentType, vi.bindingTarget, vi.bindingTargetType))
                    continue;

                string configKey = $"{item.componentTypeName}.{vi.bindingTarget}";
                if (!seen.Add(configKey)) continue;

                configs.Add(new WatcherConfig
                {
                    componentType = item.componentTypeName,
                    propertyName = vi.bindingTarget,
                    propertyType = TypeToCSharpName(vi.bindingTargetType),
                    poolKey = $"{item.componentTypeName}.{vi.bindingTarget}"
                });
            }
        }

        return configs;
    }

    /// <summary>
    /// 判断组件类型的属性是否已有内置 OnValueChanged 事件
    /// </summary>
    private static bool HasBuiltInEvent(UIComponentType compType, string propName, System.Type propType)
    {
        switch (compType)
        {
            case UIComponentType.InputField:
                if (propName == "text") return true;
                break;
            case UIComponentType.Toggle:
                if (propName == "isOn") return true;
                break;
            case UIComponentType.Slider:
                if (propName == "value") return true;
                break;
        }
        return false;
    }

    private static string TypeToCSharpName(System.Type t)
    {
        if (t == null) return "object";
        if (t == typeof(string)) return "string";
        if (t == typeof(int)) return "int";
        if (t == typeof(float)) return "float";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(double)) return "double";
        if (t == typeof(long)) return "long";
        return t.Name;
    }
}
