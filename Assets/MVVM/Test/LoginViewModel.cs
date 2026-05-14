using UnityEngine;
using System;

/// <summary>
/// 登录业务 ViewModel：管理用户名、密码、登录命令、生成方块命令
/// 只关心数据与业务逻辑，不依赖任何 UI 控件
/// </summary>
public class LoginViewModel1 : ViewModelBase
{
    public LoginViewModel1()
    {
        // 初始化用户名（BindInputField 会将初始值回显到 InputField）
        SetValue("UserName", "");

        // 初始化密码
        SetValue("Password", "");

        // 初始化登录命令：接收密码参数，写入 Password 属性
        // BindableProperty 会自动通知所有绑定者（DisplayPanel 实时更新）
        SetValue("LoginCommand", new Action<string>((password) =>
        {
            SetValue("Password", password);
            Debug.Log($"[LoginViewModel] 登录命令执行，密码设为: {password}");
        }));

        // 初始化生成方块命令：空 Action，由 TestLauncher 通过 Value += 注入实际生成逻辑
        SetValue("SpawnCubeCommand", new Action(() =>
        {
            // 占位，实际逻辑由外部订阅者追加
        }));
    }
}
