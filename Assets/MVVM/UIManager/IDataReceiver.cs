/// <summary>
/// 数据传递接口：View 实现此接口后，UIManager.Open 的 data 参数会自动传递
/// </summary>
public interface IDataReceiver
{
    /// <summary>
    /// 接收外部传入的初始化数据
    /// </summary>
    /// <param name="data">任意数据对象</param>
    void OnReceiveData(object data);
}
