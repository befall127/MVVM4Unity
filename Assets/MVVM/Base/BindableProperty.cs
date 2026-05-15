using UnityEngine;
using UnityEngine.Events;

public interface IBindableProperty
{
    object ValueBoxed { get; set; }
}

public class BindableProperty<T> : IBindableProperty
{
    public event UnityAction<T> onValueChanged = delegate { };

    private T _value = default(T);
    public T Value
    {
        get
        {
            return _value;
        }
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                ValueChanged(_value);
            }
        }
    }

    public object ValueBoxed
    {
        get { return Value; }
        set { Value = (T)value; }
    }

    private void ValueChanged(T value)
    {
        if (onValueChanged != null)
            onValueChanged.Invoke(value);
    }

    /// <summary>
    /// 注册数据变化监听（之后当数据变更时不仅Value.set可直接触发更新事件）
    /// </summary>
    public void RegistValueChanged(UnityAction<T> OnValueChanged)
    {
        this.onValueChanged += OnValueChanged;
    }

    public void RemoveValueChanged(UnityAction<T> OnValueChanged)
    {
        this.onValueChanged -= OnValueChanged;
    }

    public override string ToString()
    {
        return (Value != null ? Value.ToString() : "null");
    }

    /// <summary>
    /// 清除当前值（重置为默认值，不会触发onValueChanged事件）
    /// </summary>
    public void Clear()
    {
        _value = default(T);
    }

    public BindableProperty(){ }
    public BindableProperty(T value)
    {
        _value = value;
    }

    public static implicit operator T(BindableProperty<T> prop)
    {
        return prop != null ? prop.Value : default(T);
    }
}
