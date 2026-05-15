using UnityEngine;

/// <summary>
/// BindablePropertyPool 全功能测试：包括事件集批量绑定与 Debug 日志
/// </summary>
public class PoolTest : MonoBehaviour
{
    private BindableProperty<int> m_count = new BindableProperty<int>();

    void Start()
    {
        // ═══════════ Step 1：注册到池 ═══════════
        m_count.AddToPool("Count", 0);

        // ═══════════ Step 2：创建多个事件并自动入集 ═══════════
        var evtLog = m_count.AddPoolEvent<int>("CountChanged");
        var evtUi  = m_count.AddPoolEvent<int>("CountUIUpdated");
        var evtNet = m_count.AddPoolEvent<int>("CountSynced");

        // ═══════════ Step 3：向事件注入业务逻辑 ═══════════
        evtLog.Value = (v) => Debug.Log($"[PoolTest] ★ Log: Count={v}");
        evtUi.Value  = (v) => Debug.Log($"[PoolTest] ★ UI:  更新显示为 {v}");
        evtNet.Value = (v) => Debug.Log($"[PoolTest] ★ Net: 同步到服务器 Count={v}");

        // ═══════════ Step 4：查看事件集 ═══════════
        BindablePropertyPool.LogEventSet("Count");

        // ═══════════ Step 5：一次性绑定全部事件 ═══════════
        m_count.AddPoolBinding<int>();  // 无参 ≡ 全部绑定

        // ═══════════ Step 6：验证 ═══════════
        Debug.Log("═══════ 修改 Count=10 ═══════");
        m_count.Value = 10;
        // 期望：3 条 ★ 事件日志全部触发

        Debug.Log("═══════ 池修改 Count=20 ═══════");
        BindablePropertyPool.Set("Count", 20);
        // 期望：3 条 ★ 事件日志再次触发
    }
}
