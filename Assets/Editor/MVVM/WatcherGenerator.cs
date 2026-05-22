using MVVM.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WatcherGenerator
{
    public static ScannedComponent GetSelectedWatcherComp(int _watcherSelectedCompIndex, List<ScannedComponent> _watcherComps)
    {
        if (_watcherSelectedCompIndex < 0 || _watcherSelectedCompIndex >= _watcherComps.Count)
            return null;
        return _watcherComps[_watcherSelectedCompIndex];
    }

    public static string BuildWatcherCode(int _watcherSelectedCompIndex, List<ScannedComponent> _watcherComps, List<WatcherPropertyEntry> _watcherProperties)
    {
        var sc = GetSelectedWatcherComp(_watcherSelectedCompIndex,_watcherComps);
        if (sc == null) return null;

        var activeProps = new List<(int idx, string name, System.Type type)>();
        foreach (var entry in _watcherProperties)
        {
            if (entry.sourcePropIndex < 0 || entry.sourcePropIndex >= sc.properties.Count)
                continue;
            var prop = sc.properties[entry.sourcePropIndex];
            string fieldName = string.IsNullOrEmpty(entry.customFieldName) ? prop.Name : entry.customFieldName;
            activeProps.Add((entry.sourcePropIndex, fieldName, prop.ValueType));
        }
        if (activeProps.Count == 0) return null;

        string compType = sc.component.GetType().Name;
        string className = $"{compType}Watcher";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// 自动生成：轮询 {compType} 属性变化，同步到 BindablePropertyPool");
        sb.AppendLine($"/// </summary>");
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

        // Pool Key 常量
        foreach (var ap in activeProps)
            sb.AppendLine($"    public const string POOL_KEY_{ap.name} = \"{compType}.{ap.name}\";");

        sb.AppendLine();

        // 轮询间隔
        sb.AppendLine("    [SerializeField] private float _checkInterval = 0.1f;");
        sb.AppendLine("    private float _timer;");
        sb.AppendLine();

        // Start
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

        // Update
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
}
