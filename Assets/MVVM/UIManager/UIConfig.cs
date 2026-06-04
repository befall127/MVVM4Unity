using System.Collections.Generic;

/// <summary>
/// 界面配置表：注册所有界面的 Addressables 地址和默认层级
/// 使用前需要在此处添加项目的界面配置
/// </summary>
public static class UIConfig
{
    /// <summary>
    /// 界面注册表：key = 界面标识，value = (地址, 层级)
    /// </summary>
    public static readonly Dictionary<string, UIEntry> Entries = new Dictionary<string, UIEntry>
    {
        // 示例配置（按项目实际情况修改）：
        // ["LoginPanel"]    = new UIEntry("UI/LoginPanel",    UILayer.Normal),
        // ["SettingsPanel"] = new UIEntry("UI/SettingsPanel", UILayer.Popup),
        // ["ToastPanel"]    = new UIEntry("UI/ToastPanel",    UILayer.Toast),
    };
}

/// <summary>
/// 界面配置项
/// </summary>
public class UIEntry
{
    /// <summary>Addressables 加载地址</summary>
    public string Address;

    /// <summary>默认层级</summary>
    public UILayer Layer;

    public UIEntry(string address, UILayer layer = UILayer.Normal)
    {
        Address = address;
        Layer = layer;
    }
}
