using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录面板：只声明 UI 控件与 ViewModel 的绑定关系，不直接操作数据
/// </summary>
public class LoginPanel : ViewBase
{
    [Header("组合1：登录界面（输入）")]
    [SerializeField] private InputField _inputUserName;
    [SerializeField] private InputField _inputPassword;
    [SerializeField] private Button _btnLogin;

    protected override void OnBinding()
    {
        // ============ 1. 双向绑定：InputField 包装属性 ↔ ViewModel 属性 ============
        _binder.BindTwoWay(_binder.Wrap(_inputUserName.text, _inputUserName.onValueChanged), _viewModel.GetBindableProperty<string>("UserName"));
        _binder.BindTwoWay(_binder.Wrap(_inputPassword.text, _inputPassword.onValueChanged), _viewModel.GetBindableProperty<string>("Password"));

        // ============ 2. 事件绑定：按钮点击 → ViewModel.LoginCommand（捕获密码参数） ============
        _binder.RegisterEvent<string>(_btnLogin.onClick, "LoginCommand", () => _inputPassword.text);

        // ============ 3. 事件绑定：按钮点击 → ViewModel.SpawnCubeCommand ============
        _binder.RegisterEvent(_btnLogin.onClick, "SpawnCubeCommand");

        Debug.Log("[LoginPanel] 绑定完成：所有控件通过 Binder 声明绑定关系，不直接操作 ViewModel 数据");
    }
}
