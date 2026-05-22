using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MVVM.Editor
{
    /// <summary>
    /// 可绑定属性的描述信息
    /// </summary>
    public struct BindablePropInfo
    {
        public string Name;
        public Type ValueType;
        public string DisplayName; // 如 "text (string)"
    }

    /// <summary>
    /// 可绑定事件的描述信息
    /// </summary>
    public struct BindableEventInfo
    {
        public string Name;
        public Type EventType; // UnityEvent 或 UnityEvent<T>
    }

    /// <summary>
    /// 扫描到的组件信息
    /// </summary>
    public class ScannedComponent
    {
        public GameObject gameObject;
        public Component component;
        public string displayName;          // Path/ComponentName(ComponentType)
        public List<BindablePropInfo> properties = new List<BindablePropInfo>();
        public List<BindableEventInfo> events = new List<BindableEventInfo>();
    }

    /// <summary>
    /// 一条数据绑定配置
    /// </summary>
    [System.Serializable]
    public class DataBindingEntry
    {
        public int sourceIndex = -1;             // ScannedComponent 索引
        public int sourcePropIndex = -1;         // properties 索引
        public int preselectPathIndex = -1;      // 创建时的预选路径索引，之后不受全局预选路径变化影响
        public string customFieldName = "";      // 自定义变量名（不为空时替代自动生成的名字）
        public string vmPropertyName = "";       // ViewModel 属性名
        public bool isTwoWay = true;
    }

    /// <summary>
    /// 一条事件绑定配置
    /// </summary>
    [System.Serializable]
    public class EventBindingEntry
    {
        public int sourceIndex = -1;
        public int sourceEventIndex = -1;
        public int preselectPathIndex = -1;      // 创建时的预选路径索引
        public string customFieldName = "";      // 自定义变量名
        public string vmCommandName = "";
        public string captureParamExpr = "";    // 可选：传递捕获参数，如 "m_InputField.text"
    }

    /// <summary>
    /// 一条事件绑定配置
    /// </summary>
    //[System.Serializable]
    //public class EventBindingEntry
    //{
    //    public int sourceIndex = -1;
    //    public int sourceEventIndex = -1;
    //    public string vmCommandName = "";
    //    public string captureParamExpr = "";     // 如 "m_PasswordInput.text"
    //}

    /// <summary>
    /// 组件属性/事件扫描器
    /// </summary>
    public static class ComponentScanner
    {
        /// <summary>
        /// 扫描目标 GameObject 及其子物体的 UI 组件，返回可绑定列表
        /// </summary>
        public static List<ScannedComponent> Scan(GameObject root)
        {
            var result = new List<ScannedComponent>();
            if (root == null) return result;

            ScanRecursive(root, result, "");
            return result;
        }

        /// <summary>
        /// 仅扫描目标 GameObject 自身组件（不递归子物体），用于 Watcher 等单对象场景
        /// </summary>
        public static List<ScannedComponent> ScanSelf(GameObject go)
        {
            var result = new List<ScannedComponent>();
            if (go == null) return result;

            foreach (var comp in go.GetComponents<Component>())
            {
                var sc = ToScannedComponent(go, comp, go.name);
                if (sc != null) result.Add(sc);
            }
            return result;
        }

        private static void ScanRecursive(GameObject go, List<ScannedComponent> result, string parentPath)
        {
            // 构造当前节点的完整路径（根节点时 parentPath 为空）
            string currentPath = string.IsNullOrEmpty(parentPath)
                ? go.name
                : parentPath + "/" + go.name;

            // 扫描自身
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    Debug.LogError($"{currentPath} 存在 Missing 组件");
                    continue;
                }
                var sc = ToScannedComponent(go, comp, currentPath);
                if (sc != null) result.Add(sc);
            }

            // 扫描子物体
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                ScanRecursive(child, result, currentPath);
            }
        }

        private static ScannedComponent ToScannedComponent(GameObject go, Component comp, string path)
        {
            var props = GetBindableProps(comp);
            var evts = GetBindableEvents(comp);

            if (props.Count == 0 && evts.Count == 0) return null;

            return new ScannedComponent
            {
                gameObject = go,
                component = comp,
                displayName = $"{path}/{comp.GetType().Name}",
                properties = props,
                events = evts
            };
        }

        /// <summary>
        /// 获取组件上可绑定的数据属性（公开、有 getter 和 setter、非 Event 类型）
        /// </summary>
        public static List<BindablePropInfo> GetBindableProps(Component comp)
        {
            var list = new List<BindablePropInfo>();
            var type = comp.GetType();
            // 跳过一些基础组件，避免全是 transform.position 之类的
            if (type == typeof(Transform) || type == typeof(RectTransform))
                return list;

            var flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (typeof(UnityEventBase).IsAssignableFrom(prop.PropertyType)) continue;
                if (prop.PropertyType.IsSubclassOf(typeof(Component))) continue;
                if (!IsSupportedBindingType(prop.PropertyType)) continue;

                list.Add(new BindablePropInfo
                {
                    Name = prop.Name,
                    ValueType = prop.PropertyType,
                    DisplayName = $"{prop.Name} ({TypeToKeyword(prop.PropertyType)})"
                });
            }
            return list;
        }

        /// <summary>
        /// 获取组件上可绑定的事件（UnityEvent / UnityEvent<T>）
        /// </summary>
        public static List<BindableEventInfo> GetBindableEvents(Component comp)
        {
            var list = new List<BindableEventInfo>();
            var type = comp.GetType();

            var flags = BindingFlags.Public | BindingFlags.Instance;

            // 属性形式的 UnityEvent
            foreach (var prop in type.GetProperties(flags))
            {
                if (typeof(UnityEventBase).IsAssignableFrom(prop.PropertyType))
                {
                    list.Add(new BindableEventInfo { Name = prop.Name, EventType = prop.PropertyType });
                }
            }

            // 字段形式的 UnityEvent
            foreach (var field in type.GetFields(flags))
            {
                if (typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                {
                    // 避免重复
                    if (!list.Exists(e => e.Name == field.Name))
                        list.Add(new BindableEventInfo { Name = field.Name, EventType = field.FieldType });
                }
            }

            return list;
        }

        private static bool IsSupportedBindingType(Type t)
        {
            return t == typeof(string) || t == typeof(int) || t == typeof(float) ||
                   t == typeof(bool) || t == typeof(double) || t == typeof(Color) ||
                   t == typeof(Sprite) || t == typeof(Vector2) || t == typeof(Vector3) ||
                   t == typeof(Vector4) || t == typeof(Vector2Int) || t == typeof(Vector3Int);
            //return true;
        }

        private static string TypeToKeyword(Type t)
        {
            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            return t.Name;
        }

        /// <summary>
        /// 清理字符串中的非法 C# 标识字符（括号、空格、横杠等），用于生成合法的变量名
        /// </summary>
        public static string CleanFieldName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Unknown";
            var sb = new System.Text.StringBuilder();
            foreach (char c in rawName)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            var result = sb.ToString();
            // 确保不以数字开头
            if (result.Length > 0 && char.IsDigit(result[0]))
                result = "_" + result;
            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }
    }
}
