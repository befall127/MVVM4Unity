/// <summary>
/// 代码生成规则配置
/// </summary>
[System.Serializable]
public class GenCodeRule
{
    /// <summary>基类索引（对应 GenCodeUtil.supportBaseTypes）</summary>
    public int baseTypeIndex;

    /// <summary>继承的基类名（如 "ViewBase"、"PanelBase"）</summary>
    public string baseClassName = "ViewBase";

    /// <summary>类名后缀，默认 "View"</summary>
    public string classNameSuffix = "View";

    /// <summary>字段名前缀，默认 "m_"</summary>
    public string fieldPrefix = "m_";

    /// <summary>是否生成双向绑定（对 InputField/Toggle/Slider 生效）</summary>
    public bool enableTwoWayBinding = true;

    /// <summary>是否在生成脚本后自动添加到 GameObject</summary>
    public bool autoAddComponent = true;

    /// <summary>脚本输出目录（相对于 Assets）</summary>
    public string outputDirectory = "MVVM/Generated";

    // ───── 静态预设 ─────

    /// <summary>View 脚本默认规则</summary>
    public static GenCodeRule DefaultViewRule => new GenCodeRule
    {
        baseTypeIndex = 0,
        baseClassName = "ViewBase",
        classNameSuffix = "View",
        fieldPrefix = "m_",
        enableTwoWayBinding = true,
        autoAddComponent = true,
        outputDirectory = "MVVM/Generated"
    };

    /// <summary>ViewModel 脚本默认规则</summary>
    public static GenCodeRule DefaultViewModelRule => new GenCodeRule
    {
        baseClassName = "ViewModelBase",
        classNameSuffix = "ViewModel",
        fieldPrefix = "m_",
        enableTwoWayBinding = true,
        autoAddComponent = false,
        outputDirectory = "MVVM/Generated"
    };

    public static GenCodeRule DefaultManagerRule => new GenCodeRule
    {
        baseClassName = "MonoBehaviour",
        classNameSuffix = "Manager",
        fieldPrefix = "m_",
        enableTwoWayBinding = true,
        autoAddComponent = false,
        outputDirectory = "MVVM/Generated"
    };

    /// <summary>浅拷贝一份新的实例，用于修改后不影响预设</summary>
    public GenCodeRule Clone()
    {
        return new GenCodeRule
        {
            baseTypeIndex = this.baseTypeIndex,
            baseClassName = this.baseClassName,
            classNameSuffix = this.classNameSuffix,
            fieldPrefix = this.fieldPrefix,
            enableTwoWayBinding = this.enableTwoWayBinding,
            autoAddComponent = this.autoAddComponent,
            outputDirectory = this.outputDirectory
        };
    }
}
