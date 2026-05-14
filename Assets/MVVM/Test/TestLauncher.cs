using UnityEngine;
using System;

/// <summary>
/// 测试启动器：挂载到场景 GameObject 上，负责初始化面板、ViewModel，
/// 并注入生成方块的逻辑来验证事件绑定能力
/// </summary>
public class TestMVVM : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private LoginPanelGenerate _loginPanel;
    [SerializeField] private DisplayPanel _displayPanel;

    private LoginViewModel _sharedViewModel;

    void Start()
    {
        _sharedViewModel = new LoginViewModel();

        _loginPanel.SetViewModel(_sharedViewModel);
        _displayPanel.SetViewModel(_sharedViewModel);

        // 向 ViewModel 的 SpawnCubeCommand 注入生成方块的逻辑
        // 当 LoginPanel 的按钮点击时，Binder.RegisterEvent 会触发该命令
        var spawnCmdProp = _sharedViewModel.GetBindableProperty<Action>("SpawnCubeCommand");
        spawnCmdProp.Value += () =>
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "MVVM_SpawnedCube";
            cube.transform.position = new Vector3(0, 2, 0);
            Debug.Log("[TestMVVM] === 方块已生成到场景中！ ===");
        };

        Debug.Log("===== MVVM 测试初始化完成 =====");
        Debug.Log("操作指南：");
        Debug.Log("1. 在左侧【用户名】输入框中输入文字 → 观察右侧【用户名】是否同步更新");
        Debug.Log("2. 在左侧【密码】输入框中输入密码 → 点击【登录】按钮");
        Debug.Log("3. 观察右侧【密码】是否同步更新为输入的密码");
        Debug.Log("4. 同时观察场景中是否生成了一个方块（事件绑定测试）");
        Debug.Log("   每次点击登录按钮，都会生成一个新的方块！");
    }
}
