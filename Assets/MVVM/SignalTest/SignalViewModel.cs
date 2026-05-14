using DNTC.Auto.Laucher;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class SignalViewModel : ViewModelBase
{
    public SignalViewModel()
    {
        //注册事件实现
        SetValue<Action<string>>("DebugLeft", isOpen => Debug.Log($"左门状态更改为 {isOpen}"));
        SetValue<Action<string>>("SetLeftDoor", isOpen => LaucherCarController.Instance.SetCarDoor(1, isOpen.Equals("1")));

        SetValue<Action<string>>("DebugRight", isOpen => Debug.Log($"右门状态更改为 {isOpen}"));
    }
}
