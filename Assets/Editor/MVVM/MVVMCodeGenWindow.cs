using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using MVVM.Editor;

/// <summary>
/// MVVM 代码生成可视化编辑器窗口
/// 扫描目标 GameObject 的子 UI 组件，配置数据/事件绑定后一键生成 PanelBase 子类脚本
/// </summary>
public class MVVMCodeGenWindow : EditorWindow
{
    // ===== 通用配置 =====
    private GameObject _targetObject;
    private string _className = "";
    private GenCodeRule _viewRule = GenCodeRule.DefaultViewRule.Clone();

    // ===== 扫描结果 =====
    private List<ScannedComponent> _scannedComponents = new List<ScannedComponent>();
    private string[] _scannedNames = new string[0];
    private string[] _gameObjectPaths = new string[0];             // 所有 GameObject 路径（不含组件名），用于预选路径
    private int _dataPreselectIndex = -1;                           // 数据绑定预选路径索引
    private int _eventPreselectIndex = -1;                          // 事件绑定预选路径索引

    // ===== 数据绑定 =====
    private List<DataBindingEntry> _dataBindings = new List<DataBindingEntry>();

    // ===== 事件绑定 =====
    private List<EventBindingEntry> _eventBindings = new List<EventBindingEntry>();

    // ===== ViewModel 批量生成 =====
    private List<MonoScript> _viewScripts = new List<MonoScript>();
    private Vector2 _vmScrollPos;

    // ===== Manager 批量生成 =====
    private List<ManagerEntry> _managerEntries = new List<ManagerEntry>();
    private Vector2 _managerScrollPos;
    private string _managerFileName = "";

    // ===== UI 滚动位置 =====
    private Vector2 _dataScrollPos;
    private Vector2 _eventScrollPos;
    private Vector2 _previewScrollPos;
    private string _previewCode = "";
    private bool _showPreview;

    // ===== 常量 =====
    private const float BUTTON_WIDTH = 24f;
    private const float LABEL_WIDTH = 100f;

    [MenuItem("MVVM/Code Generator Window")]
    public static void Open()
    {
        var win = GetWindow<MVVMCodeGenWindow>("MVVM Code Gen");
        win.minSize = new Vector2(600, 500);
        if (win._viewScripts.Count == 0)
            win._viewScripts.Add(null);
        if (win._managerEntries.Count == 0)
            win._managerEntries.Add(new ManagerEntry());
    }

    [MenuItem("Assets/MVVM/Generate ViewModel from View Script", false, 1100)]
    private static void GenerateViewModelFromSelection()
    {
        var selected = Selection.activeObject;
        var path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs"))
        {
            EditorUtility.DisplayDialog("提示", "请在 Project 窗口中选中一个生成的 View .cs 脚本", "确定");
            return;
        }

        string vmCode = ViewModelGenerator.Generate(path, out string vmClassName);
        if (string.IsNullOrEmpty(vmCode))
        {
            EditorUtility.DisplayDialog("失败", "无法从选中文件中提取 OnBinding 信息", "确定");
            return;
        }

        var dir = Path.GetDirectoryName(path);
        var vmPath = Path.Combine(dir, vmClassName + ".cs");
        File.WriteAllText(vmPath, vmCode);
        AssetDatabase.Refresh();

        Debug.Log($"[MVVMCodeGen] ViewModel 已生成至: {vmPath}");
        EditorUtility.DisplayDialog("完成", $"ViewModel 已生成:\n{vmPath}", "确定");
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);

        // ====== 头部：目标对象 + 类名 ======
        EditorGUILayout.LabelField("基础配置", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _targetObject = (GameObject)EditorGUILayout.ObjectField("目标 GameObject", _targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            _className = _targetObject != null
                ? _targetObject.name.Replace(" ", "") + _viewRule.classNameSuffix
                : "";
        }
        _className = EditorGUILayout.TextField("类名", _className);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("扫描子物体组件", GUILayout.Height(24)))
            ScanComponents();
        if (_scannedComponents.Count > 0)
            EditorGUILayout.LabelField($"已扫描 {_scannedComponents.Count} 个可绑定组件", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        if (_scannedComponents.Count == 0)
        {
            EditorGUILayout.HelpBox("请先指定目标 GameObject 并点击 扫描子物体组件，以启用 View 脚本生成功能", MessageType.Info);
        }
        else
        {
            // ====== 数据绑定区域 ======
            DrawDataBindingSection();

            EditorGUILayout.Space(8);

            // ====== 事件绑定区域 ======
            DrawEventBindingSection();

            EditorGUILayout.Space(12);

            // ====== 生成按钮 ======
            DrawGenerateButtons();
        }

        // ====== ViewModel 批量生成（始终可见） ======
        EditorGUILayout.Space(8);
        DrawViewModelSection();

        // ====== Manager 批量生成（始终可见） ======
        EditorGUILayout.Space(8);
        DrawManagerSection();
    }

    #region ============ 扫描 ============

    private void ScanComponents()
    {
        if (_targetObject == null)
        {
            EditorUtility.DisplayDialog("提示", "请先拖入目标 GameObject", "确定");
            return;
        }
        _scannedComponents = ComponentScanner.Scan(_targetObject);
        _scannedNames = _scannedComponents.Select(s => s.displayName).ToArray();

        // 构建 GameObject 路径列表（仅路径，不含组件类型名）
        var pathSet = new HashSet<string>();
        foreach (var sc in _scannedComponents)
        {
            var parts = sc.displayName.Split('/');
            var current = "";
            for (int i = 0; i < parts.Length - 1; i++)  // 最后一段是组件类型名，去掉
            {
                current = i == 0 ? parts[i] : current + "/" + parts[i];
                pathSet.Add(current);
            }
        }
        _gameObjectPaths = pathSet.OrderBy(p => p.Count(c => c == '/'))
                                  .ThenBy(p => p).ToArray();

        // 清空旧绑定与预选路径
        _dataBindings.Clear();
        _eventBindings.Clear();
        _dataPreselectIndex = -1;
        _eventPreselectIndex = -1;
        _previewCode = "";
        _showPreview = false;

        Debug.Log($"[MVVMCodeGen] 扫描完成：找到 {_scannedComponents.Count} 个可绑定组件");
    }

    #endregion

    #region ============ 过滤 ============

    /// <summary>
    /// 根据预选路径索引，从 _scannedNames 过滤并裁剪前缀
    /// 返回裁剪后的显示名称数组 + 映射回 _scannedComponents 原始索引的数组
    /// </summary>
    private void GetFilteredScannedNames(int preselectIndex, out string[] filteredNames, out int[] originalIndices)
    {
        if (preselectIndex < 0 || preselectIndex >= _gameObjectPaths.Length)
        {
            // 无预选路径 → 全部显示
            filteredNames = _scannedNames;
            originalIndices = Enumerable.Range(0, _scannedComponents.Count).ToArray();
            return;
        }

        var prefix = _gameObjectPaths[preselectIndex] + "/";
        var namesList = new List<string>();
        var indicesList = new List<int>();

        for (int i = 0; i < _scannedComponents.Count; i++)
        {
            var name = _scannedNames[i];
            if (name.StartsWith(prefix))
            {
                namesList.Add(name.Substring(prefix.Length));
                indicesList.Add(i);
            }
        }

        filteredNames = namesList.ToArray();
        originalIndices = indicesList.ToArray();
    }

    #endregion

    #region ============ 数据绑定 UI ============

    private void DrawDataBindingSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("数据绑定（Wrap + BindTwoWay / RegisterMember）", EditorStyles.boldLabel);
        if (GUILayout.Button("+", GUILayout.Width(BUTTON_WIDTH)))
            _dataBindings.Add(new DataBindingEntry { preselectPathIndex = _dataPreselectIndex });
        if (GUILayout.Button("-", GUILayout.Width(BUTTON_WIDTH)))
        {
            if (_dataBindings.Count > 0) _dataBindings.RemoveAt(_dataBindings.Count - 1);
        }
        EditorGUILayout.EndHorizontal();

        // 预选路径：仅显示 GameObject 层级，通过 GenericMenu 的 / 实现级联展开
        _dataPreselectIndex = EditorGUILayout.Popup("预选路径", _dataPreselectIndex, _gameObjectPaths);

        if (_dataBindings.Count == 0)
        {
            EditorGUILayout.HelpBox("点击 + 添加数据绑定。左侧选择 UI 组件和属性，右侧填入 ViewModel 属性名", MessageType.Info);
            return;
        }

        _dataScrollPos = EditorGUILayout.BeginScrollView(_dataScrollPos, _dataBindings.Count == 1 ? GUILayout.Height(160) : GUILayout.Height(320));
        for (int i = 0; i < _dataBindings.Count; i++)
        {
            DrawDataBindingRow(i);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawDataBindingRow(int index)
    {
        var entry = _dataBindings[index];

        EditorGUILayout.BeginVertical("box");

        // 行 1：组件选择 + 删除
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(24));
        int prevIndex = entry.sourceIndex;
        GetFilteredScannedNames(entry.preselectPathIndex, out string[] filterNames, out int[] mapping);

        // 将原始索引映射到过滤列表中的索引，默认为 0
        int displayIndex = System.Array.IndexOf(mapping, entry.sourceIndex);
        displayIndex = displayIndex >= 0 ? displayIndex : 0;
        if (filterNames.Length == 0) filterNames = new string[] { "(无匹配组件)" };

        int selected = EditorGUILayout.Popup("UI 组件", displayIndex, filterNames);
        // 将过滤列表索引还原为原始未修剪前缀 _scannedComponents 索引
        if (selected < mapping.Length)
            entry.sourceIndex = mapping[selected];

        if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
        {
            _dataBindings.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        // 行 2：属性选择 + VM 属性名
        if (entry.sourceIndex >= 0 && entry.sourceIndex < _scannedComponents.Count)
        {
            var sc = _scannedComponents[entry.sourceIndex];
            var propNames = sc.properties.Select(p => p.DisplayName).ToArray();

            if (propNames.Length > 0)
            {
                entry.sourcePropIndex = EditorGUILayout.Popup("UI 属性", entry.sourcePropIndex, propNames);
            }

            // 变量命名：切换组件时自动填充清理后的名字
            if (entry.sourceIndex != prevIndex || string.IsNullOrEmpty(entry.customFieldName))
            {
                entry.customFieldName = ComponentScanner.CleanFieldName(sc.gameObject.name);
            }
            entry.customFieldName = EditorGUILayout.TextField("变量命名", entry.customFieldName);
            EditorGUILayout.LabelField($"  → 将声明为 [SerializeField] private {GetFieldTypeName(MapComponentToType(sc.component))} m_{entry.customFieldName};",
                EditorStyles.miniLabel);

            entry.vmPropertyName = EditorGUILayout.TextField("VM 属性名", entry.vmPropertyName);
            entry.isTwoWay = EditorGUILayout.Toggle("双向绑定", entry.isTwoWay);

            // 预览
            if (entry.sourcePropIndex >= 0 && entry.sourcePropIndex < sc.properties.Count && !string.IsNullOrEmpty(entry.vmPropertyName))
            {
                var prop = sc.properties[entry.sourcePropIndex];
                string mode = entry.isTwoWay ? "BindTwoWay(Wrap<> ↔ VM)" : "RegisterMember(VM → UI)";
                EditorGUILayout.LabelField($"  → {sc.gameObject.name}.{prop.Name} ({prop.DisplayName}) <=> [{entry.vmPropertyName}]  [{mode}]",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    #endregion

    #region ============ 事件绑定 UI ============

    private void DrawEventBindingSection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("事件绑定（RegisterEvent）", EditorStyles.boldLabel);
        if (GUILayout.Button("+", GUILayout.Width(BUTTON_WIDTH)))
            _eventBindings.Add(new EventBindingEntry { preselectPathIndex = _eventPreselectIndex });
        if (GUILayout.Button("-", GUILayout.Width(BUTTON_WIDTH)))
        {
            if (_eventBindings.Count > 0) _eventBindings.RemoveAt(_eventBindings.Count - 1);
        }
        EditorGUILayout.EndHorizontal();

        // 预选路径：仅显示 GameObject 层级
        _eventPreselectIndex = EditorGUILayout.Popup("预选路径", _eventPreselectIndex, _gameObjectPaths);

        if (_eventBindings.Count == 0)
        {
            EditorGUILayout.HelpBox("点击 + 添加事件绑定。选择组件的 UnityEvent，填入 ViewModel 命令名", MessageType.Info);
            return;
        }

        _eventScrollPos = EditorGUILayout.BeginScrollView(_eventScrollPos, GUILayout.Height(160));
        for (int i = 0; i < _eventBindings.Count; i++)
        {
            DrawEventBindingRow(i);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawEventBindingRow(int index)
    {
        var entry = _eventBindings[index];

        EditorGUILayout.BeginVertical("box");

        // 行 1：组件选择 + 删除
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(24));
        int prevIndex = entry.sourceIndex;
        GetFilteredScannedNames(entry.preselectPathIndex, out string[] filterNames, out int[] mapping);

        int displayIndex = System.Array.IndexOf(mapping, entry.sourceIndex);
        displayIndex = displayIndex >= 0 ? displayIndex : 0;
        if (filterNames.Length == 0) filterNames = new string[] { "(无匹配组件)" };

        int selected = EditorGUILayout.Popup("UI 组件", displayIndex, filterNames);
        if (selected < mapping.Length)
            entry.sourceIndex = mapping[selected];

        if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
        {
            _eventBindings.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        // 行 2：事件选择 + VM 命令名 + 可选参数
        if (entry.sourceIndex >= 0 && entry.sourceIndex < _scannedComponents.Count)
        {
            var sc = _scannedComponents[entry.sourceIndex];
            var eventNames = sc.events.Select(e => $"{e.Name} ({GetEventTypeName(e.EventType)})").ToArray();

            if (eventNames.Length > 0)
            {
                entry.sourceEventIndex = EditorGUILayout.Popup("UI 事件", entry.sourceEventIndex, eventNames);
            }
            else
            {
                EditorGUILayout.HelpBox("该组件无可绑定 UnityEvent", MessageType.Warning);
            }

            // 变量命名：切换组件时自动填充清理后的名字
            if (entry.sourceIndex != prevIndex || string.IsNullOrEmpty(entry.customFieldName))
            {
                entry.customFieldName = ComponentScanner.CleanFieldName(sc.gameObject.name);
            }
            entry.customFieldName = EditorGUILayout.TextField("变量命名", entry.customFieldName);
            EditorGUILayout.LabelField($"  → 将声明为 [SerializeField] private Button m_{entry.customFieldName};",
                EditorStyles.miniLabel);

            entry.vmCommandName = EditorGUILayout.TextField("VM 命令名", entry.vmCommandName);
            entry.captureParamExpr = EditorGUILayout.TextField("捕获参数(可选)", entry.captureParamExpr);

            // 预览
            if (entry.sourceEventIndex >= 0 && entry.sourceEventIndex < sc.events.Count && !string.IsNullOrEmpty(entry.vmCommandName))
            {
                var evt = sc.events[entry.sourceEventIndex];
                string cap = string.IsNullOrEmpty(entry.captureParamExpr) ? "" : $" + 参数 [{entry.captureParamExpr}]";
                EditorGUILayout.LabelField($"  → {sc.gameObject.name}.{evt.Name} => VM.{entry.vmCommandName}{cap}",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    #endregion

    #region ============ 生成 ============

    private void DrawGenerateButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成预览", GUILayout.Height(32)))
        {
            _previewCode = BuildCode();
            _showPreview = true;
        }
        if (GUILayout.Button("生成并保存到文件", GUILayout.Height(32)))
            SaveToFile();
        EditorGUILayout.EndHorizontal();

        if (_showPreview && !string.IsNullOrEmpty(_previewCode))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("代码预览", EditorStyles.boldLabel);
            _previewScrollPos = EditorGUILayout.BeginScrollView(_previewScrollPos, GUILayout.Height(200));
            EditorGUILayout.TextArea(_previewCode, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawViewModelSection()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("批量生成 ViewModel", EditorStyles.boldLabel);
        if (GUILayout.Button("+", GUILayout.Width(BUTTON_WIDTH)))
            _viewScripts.Add(null);
        if (GUILayout.Button("-", GUILayout.Width(BUTTON_WIDTH)))
        {
            if (_viewScripts.Count > 1) _viewScripts.RemoveAt(_viewScripts.Count - 1);
        }
        if (GUILayout.Button("生成全部", GUILayout.Height(24), GUILayout.Width(72)))
            GenerateViewModelsBatch();
        EditorGUILayout.EndHorizontal();

        _vmScrollPos = EditorGUILayout.BeginScrollView(_vmScrollPos, GUILayout.MaxHeight(100));
        for (int i = 0; i < _viewScripts.Count; i++)
        {
            _viewScripts[i] = (MonoScript)EditorGUILayout.ObjectField(
                $"#{i + 1}", _viewScripts[i], typeof(MonoScript), false);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawManagerSection()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("生成 Manager", EditorStyles.boldLabel);
        if (GUILayout.Button("+", GUILayout.Width(BUTTON_WIDTH)))
            _managerEntries.Add(new ManagerEntry());
        if (GUILayout.Button("-", GUILayout.Width(BUTTON_WIDTH)))
        {
            if (_managerEntries.Count > 1) _managerEntries.RemoveAt(_managerEntries.Count - 1);
        }
        if (GUILayout.Button("生成", GUILayout.Height(24), GUILayout.Width(56)))
            GenerateManagersBatch();
        EditorGUILayout.EndHorizontal();

        // 多组时显示文件名输入，默认从第一对推导
        if (_managerEntries.Count > 1)
        {
            if (string.IsNullOrEmpty(_managerFileName))
                _managerFileName = DeriveManagerFileName();
            _managerFileName = EditorGUILayout.TextField("Manager 文件名", _managerFileName);
        }
        else
        {
            _managerFileName = "";
        }

        _managerScrollPos = EditorGUILayout.BeginScrollView(_managerScrollPos, GUILayout.MaxHeight(130));
        for (int i = 0; i < _managerEntries.Count; i++)
        {
            var entry = _managerEntries[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(24));
            entry.viewScript = (MonoScript)EditorGUILayout.ObjectField("View", entry.viewScript, typeof(MonoScript), false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(24));
            entry.viewModelScript = (MonoScript)EditorGUILayout.ObjectField("ViewModel", entry.viewModelScript, typeof(MonoScript), false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndScrollView();
    }

    private void GenerateManagersBatch()
    {
        var validPairs = new List<ManagerGenerator.PairInfo>();
        foreach (var entry in _managerEntries)
        {
            if (entry.viewScript == null || entry.viewModelScript == null) continue;
            var viewPath = AssetDatabase.GetAssetPath(entry.viewScript);
            var vmPath   = AssetDatabase.GetAssetPath(entry.viewModelScript);
            if (string.IsNullOrEmpty(viewPath) || string.IsNullOrEmpty(vmPath)) continue;
            validPairs.Add(new ManagerGenerator.PairInfo { viewScriptPath = viewPath, viewModelScriptPath = vmPath });
        }

        if (validPairs.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请至少拖入一组有效的 View + ViewModel 脚本", "确定");
            return;
        }

        var rule = GenCodeRule.DefaultManagerRule;
        string className = string.IsNullOrEmpty(_managerFileName) ? DeriveManagerFileName() : _managerFileName;
        string code = ManagerGenerator.Generate(validPairs, rule, className);

        if (string.IsNullOrEmpty(code))
        {
            EditorUtility.DisplayDialog("失败", "无法生成 Manager，请检查输入文件", "确定");
            return;
        }

        var dir = System.IO.Path.Combine(Application.dataPath, rule.outputDirectory);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var filePath = System.IO.Path.Combine(dir, className + ".cs");
        File.WriteAllText(filePath, code);
        AssetDatabase.Refresh();

        Debug.Log($"[MVVMCodeGen] Manager 已生成: {filePath}");
        EditorUtility.DisplayDialog("完成", $"Manager 已生成:\n{filePath}", "确定");
    }

    /// <summary>
    /// 从第一组有效 View 脚本推导默认 Manager 文件名
    /// </summary>
    private string DeriveManagerFileName()
    {
        foreach (var entry in _managerEntries)
        {
            if (entry.viewScript == null) continue;
            var path = AssetDatabase.GetAssetPath(entry.viewScript);
            if (string.IsNullOrEmpty(path)) continue;
            string viewClass = ManagerGenerator.ExtractClassName(path);
            if (string.IsNullOrEmpty(viewClass)) continue;
            string prefix = ManagerGenerator.StripSuffix(viewClass, GenCodeRule.DefaultViewRule.classNameSuffix);
            return prefix + GenCodeRule.DefaultManagerRule.classNameSuffix;
        }
        return "GeneratedManager";
    }
    private string BuildCode()
    {
        var components = ConvertToComponentItems();
        if (components.Count == 0)
        {
            Debug.LogWarning("[MVVMCodeGen] 没有配置任何绑定");
            EditorUtility.DisplayDialog("提示", "没有配置任何绑定，请先添加数据绑定或事件绑定", "确定");
            return "";
        }
        return GenCodeUtil.GenerateCodePreview(_className, components, _viewRule);
    }

    /// <summary>
    /// 批量生成：遍历 _viewScripts 列表中每个非空 MonoScript，生成对应 ViewModel
    /// </summary>
    private void GenerateViewModelsBatch()
    {
        if (_viewScripts.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先拖入至少一个 View .cs 脚本", "确定");
            return;
        }

        int success = 0;
        int failed = 0;
        var failedPaths = new System.Collections.Generic.List<string>();

        foreach (var script in _viewScripts)
        {
            if (script == null) continue;

            var assetPath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".cs"))
            {
                failed++;
                failedPaths.Add(script.name);
                continue;
            }

            string vmCode = ViewModelGenerator.Generate(assetPath, out string vmClassName);
            if (string.IsNullOrEmpty(vmCode))
            {
                failed++;
                failedPaths.Add(script.name);
                continue;
            }

            var dir = System.IO.Path.GetDirectoryName(assetPath);
            var vmPath = System.IO.Path.Combine(dir, vmClassName + ".cs");
            System.IO.File.WriteAllText(vmPath, vmCode);
            success++;
        }

        AssetDatabase.Refresh();

        string msg = $"生成完成：成功 {success} 个, 失败 {failed} 个";
        if (failedPaths.Count > 0)
            msg += $"\n失败：{string.Join(", ", failedPaths)}";
        Debug.Log($"[MVVMCodeGen] {msg}");
        EditorUtility.DisplayDialog("批量生成 ViewModel", msg, "确定");
    }

    private void SaveToFile()
    {
        if (_targetObject == null)
        {
            EditorUtility.DisplayDialog("提示", "请先指定目标 GameObject", "确定");
            return;
        }
        if (string.IsNullOrEmpty(_className))
        {
            EditorUtility.DisplayDialog("提示", "请输入类名", "确定");
            return;
        }

        var components = ConvertToComponentItems();
        if (components.Count == 0)
        {
            Debug.LogWarning("[MVVMCodeGen] 没有配置任何绑定，无法生成脚本");
            EditorUtility.DisplayDialog("提示", "没有配置任何绑定，请先添加数据绑定或事件绑定", "确定");
            return;
        }

        string path = GenCodeUtil.CreateScript(_targetObject, components, _viewRule, _className);
        Debug.Log($"[MVVMCodeGen] 脚本已生成至: {path}");
        EditorUtility.DisplayDialog("完成", $"View 脚本已生成:\n{path}", "确定");
    }

    private List<ComponentItem> ConvertToComponentItems()
    {
        var components = new List<ComponentItem>();

        foreach (var entry in _dataBindings)
        {
            if (entry.sourceIndex < 0 || entry.sourcePropIndex < 0 ||
                entry.sourceIndex >= _scannedComponents.Count) continue;
            if (string.IsNullOrEmpty(entry.vmPropertyName)) continue;

            var sc = _scannedComponents[entry.sourceIndex];
            var prop = sc.properties[entry.sourcePropIndex];

            var item = new ComponentItem
            {
                name = ResolveFieldName(entry, sc),
                componentType = MapComponentToType(sc.component),
                componentTypeName = sc.component.GetType().Name,
                target = sc.gameObject,
                autoFindPath = ComputeAutoFindPath(sc),
                components = new List<Component> { sc.component },
                forceOneWay = !entry.isTwoWay
            };

            item.viewItems.Add(new BindingShow
            {
                bindingSource = entry.vmPropertyName,
                bindingTarget = prop.Name,
                bindingTargetType = prop.ValueType
            });

            components.Add(item);
        }

        foreach (var entry in _eventBindings)
        {
            if (entry.sourceIndex < 0 || entry.sourceEventIndex < 0 ||
                entry.sourceIndex >= _scannedComponents.Count) continue;
            if (string.IsNullOrEmpty(entry.vmCommandName)) continue;

            var sc = _scannedComponents[entry.sourceIndex];

            var item = new ComponentItem
            {
                name = ResolveFieldNameEvt(entry, sc),
                componentType = UIComponentType.Button,
                componentTypeName = sc.component.GetType().Name,
                target = sc.gameObject,
                autoFindPath = ComputeAutoFindPath(sc),
                components = new List<Component> { sc.component }
            };

            item.eventItems.Add(new BindingEvent
            {
                runtime = false,
                bindingSource = entry.vmCommandName,
                bindingTarget = sc.events[entry.sourceEventIndex].Name,
                bindingTargetType = sc.events[entry.sourceEventIndex].EventType,
                captureParamExpr = entry.captureParamExpr
            });

            components.Add(item);
        }

        return components;
    }

    #endregion

    #region ============ 辅助方法 ============

    private UIComponentType MapComponentToType(Component comp)
    {
        var t = comp.GetType();
        if (typeof(InputField).IsAssignableFrom(t)) return UIComponentType.InputField;
        if (typeof(Button).IsAssignableFrom(t)) return UIComponentType.Button;
        if (typeof(Toggle).IsAssignableFrom(t)) return UIComponentType.Toggle;
        if (typeof(Slider).IsAssignableFrom(t)) return UIComponentType.Slider;
        if (typeof(Text).IsAssignableFrom(t)) return UIComponentType.Text;
        if (typeof(Image).IsAssignableFrom(t)) return UIComponentType.Image;
        return UIComponentType.Custom;
    }

    private string GetEventTypeName(System.Type t)
    {
        if (t == typeof(UnityEvent)) return "无参";
        if (t.IsGenericType)
        {
            var args = t.GetGenericArguments();
            if (args.Length > 0) return args[0].Name;
        }
        return t.Name;
    }

    /// <summary>
    /// 解析数据绑定条目的字段名：优先使用自定义命名，否则用清理后的 GameObject 名
    /// </summary>
    private string ResolveFieldName(DataBindingEntry entry, ScannedComponent sc)
    {
        if (!string.IsNullOrEmpty(entry.customFieldName))
            return entry.customFieldName;
        return ComponentScanner.CleanFieldName(sc.gameObject.name);
    }

    /// <summary>
    /// 解析事件绑定条目的字段名
    /// </summary>
    private string ResolveFieldNameEvt(EventBindingEntry entry, ScannedComponent sc)
    {
        if (!string.IsNullOrEmpty(entry.customFieldName))
            return entry.customFieldName;
        return ComponentScanner.CleanFieldName(sc.gameObject.name);
    }

    /// <summary>
    /// 将 UIComponentType 映射为 C# 类型名（用于 miniLabel 预览）
    /// </summary>
    private string GetFieldTypeName(UIComponentType t)
    {
        return t switch
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
    /// 计算组件 GameObject 相对于扫描根节点的路径，用于生成 Start() 中的 transform.Find
    /// </summary>
    private string ComputeAutoFindPath(ScannedComponent sc)
    {
        if (_targetObject == null || sc.gameObject == null) return null;
        if (sc.gameObject == _targetObject) return ".";

        var root = _targetObject.transform;
        var target = sc.gameObject.transform;
        var parts = new System.Collections.Generic.List<string>();

        while (target != null && target != root)
        {
            parts.Insert(0, target.name);
            target = target.parent;
        }

        if (target == null) return null; // sc.gameObject 不在 _targetObject 子树中
        return string.Join("/", parts);
    }

    #endregion
}

[System.Serializable]
public class ManagerEntry
{
    public MonoScript viewScript;
    public MonoScript viewModelScript;
}
