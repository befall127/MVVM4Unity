using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SignalView : ViewBase
{
    protected override void OnBinding()
    {
        base.OnBinding();
        //绑定属性与事件
        _binder.RegisterMember<string>("LeftDoor", "DebugLeft");
        _binder.RegisterMember<string>("LeftDoor", "SetLeftDoor");
        _binder.RegisterMember<string>("RightDoor", "DebugRight");
    }
}
