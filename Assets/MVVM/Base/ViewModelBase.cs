using System;
using System.Collections.Generic;
using UnityEngine;

public class ViewModelBase : IDisposable
{
    private Dictionary<string, IBindableProperty> _properties = new Dictionary<string, IBindableProperty>();

    /// <summary>
    /// 获取可绑定属性（如不存在则自动创建）
    /// </summary>
    public BindableProperty<T> GetBindableProperty<T>(string propertyName)
    {
        if (!_properties.ContainsKey(propertyName))
        {
            var prop = new BindableProperty<T>();
            _properties.Add(propertyName, prop);
        }
        return _properties[propertyName] as BindableProperty<T>;
    }

    /// <summary>
    /// 设置BindableProperty的Value，无则创建BindableProperty并赋初值
    /// </summary>
    public void SetValue<T>(string propertyName, T value)
    {   
        GetBindableProperty<T>(propertyName).Value = value; 
    }

    /// <summary>
    /// 获取BindableProperty的Value，无则创建BindableProperty并赋default值
    /// </summary>
    public T GetValue<T>(string propertyName)
    {
        return GetBindableProperty<T>(propertyName).Value;
    }

    /// <summary>
    /// 清除所有属性绑定的数据（清空字典），不会触发属性变更事件
    /// </summary>
    public void Clear()
    {
        _properties.Clear();
    }

    /// <summary>
    /// 释放资源，清除所有属性数据
    /// </summary>
    public void Dispose()
    {
        Clear();
    }
}
