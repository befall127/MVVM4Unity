using System.Threading.Tasks;
using UnityEngine;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

/// <summary>
/// UI 资源加载器：封装 Addressables 异步加载，提供统一的加载/释放接口
/// 若项目未引入 Addressables 包，将自动回退到 Resources.Load
/// </summary>
public static class UILoader
{
    /// <summary>
    /// 异步加载 Prefab 并实例化到指定父节点下
    /// </summary>
    /// <param name="address">Addressables 地址（或 Resources 路径）</param>
    /// <param name="parent">父级 Transform</param>
    /// <returns>实例化的 GameObject，失败返回 null</returns>
    public static async Task<GameObject> LoadAsync(string address, Transform parent)
    {
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogError("[UILoader] 加载地址为空");
            return null;
        }

#if UNITY_ADDRESSABLES
        try
        {
            var handle = Addressables.InstantiateAsync(address, parent);
            var instance = await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded && instance != null)
            {
                return instance;
            }

            Debug.LogError($"[UILoader] Addressables 加载失败: {address}, Status: {handle.Status}");
            Addressables.Release(handle);
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UILoader] Addressables 加载异常: {address}\n{e}");
            return null;
        }
#else
        return LoadFromResources(address, parent);
#endif
    }

    /// <summary>
    /// 释放已加载的 UI 实例
    /// </summary>
    /// <param name="instance">要释放的 GameObject</param>
    public static void Release(GameObject instance)
    {
        if (instance == null) return;

#if UNITY_ADDRESSABLES
        Addressables.Release(instance);
#else
        Object.Destroy(instance);
#endif
    }

#if !UNITY_ADDRESSABLES
    /// <summary>
    /// Resources 回退加载（仅在未引入 Addressables 时使用）
    /// </summary>
    private static GameObject LoadFromResources(string path, Transform parent)
    {
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[UILoader] Resources 加载失败: {path}");
            return null;
        }

        var instance = Object.Instantiate(prefab, parent);
        instance.name = prefab.name;
        return instance;
    }
#endif
}
