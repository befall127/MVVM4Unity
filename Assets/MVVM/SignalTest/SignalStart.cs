using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DNTC.Auto.Laucher;
public class SignalStart : MonoBehaviour
{
    // 信号id 与 信号值 ：
    private Dictionary<long, string> signal = new Dictionary<long, string>();

    //信号id 与 属性名
    private Dictionary<long, string> signalPropDic = new Dictionary<long, string>();

    private SignalView signalView;
    private SignalViewModel signalViewModel;
    void Start()
    {
        //模拟读取信号
        signal.Add(1001, "0");
        signal.Add(1002, "0");

        //信号命名
        signalPropDic.Add(1001, "LeftDoor");
        signalPropDic.Add(1002, "RightDoor");
        //在ViewModel中创建信号属性
        signalViewModel.SetValue("LeftDoor", signal[1001]);
        signalViewModel.SetValue("RightDoor", signal[1002]);

        //绑定View与ViewModel
        signalView.SetViewModel(signalViewModel);
    }

    //修改信号值唯一方法
    public void SetValue(long signalID, string value)
    {
        //通过ID获取信号属性名
        var signalName = signalPropDic[signalID];
        //获取包装后的属性
        var signalProp =  signalViewModel.GetBindableProperty<string>(signalName);

        //设置信号值，与之前不同时BindableProperty自动触发订阅事件
        signalProp.Value = value;
    }
}