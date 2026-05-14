using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示面板：只读，验证数据绑定是否自动生效
/// </summary>
public class DisplayPanel : ViewBase
{
    [Header("组合2：数据显示（只读验证）")]
    [SerializeField] private Text _txtUserName;
    [SerializeField] private Text _txtPassword;

    protected override void OnBinding()
    {
        // ============ 1. 数据绑定：UserName → Text ============
        _binder.RegisterMember<string>("DisplayPanel.UserName", "UserName", (value) =>
        {
            _txtUserName.text = value;
            Debug.Log($"[DisplayPanel] 用户名自动更新: {value}");
        });

        // ============ 2. 数据绑定：Password → Text ============
        _binder.RegisterMember<string>("DisplayPanel.Password", "Password", (value) =>
        {
            _txtPassword.text = value;
            Debug.Log($"[DisplayPanel] 密码自动更新: {value}");
        });
    }
}