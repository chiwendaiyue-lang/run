using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 兼容旧版障碍脚本依赖的 TrackManager。
/// 如果场景里没有该组件，相关逻辑会使用默认值。
/// </summary>
public class TrackManager : MonoBehaviour
{
    public static TrackManager instance;

    public float laneOffset = 2.5f;
    public List<TrackSegment> segments = new List<TrackSegment>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            return;
        }

        instance = this;
    }
}

/// <summary>
/// 兼容旧版障碍脚本依赖的 TrackSegment。
/// 提供 GetPointAt/objectRoot/manager 等最小接口。
/// </summary>
public class TrackSegment : MonoBehaviour
{
    public Transform objectRoot;
    public TrackManager manager;
    public Transform entry;
    public Transform exit;

    private void Awake()
    {
        if (objectRoot == null)
        {
            objectRoot = transform;
        }

        if (manager == null)
        {
            manager = FindObjectOfType<TrackManager>();
        }
    }

    public void GetPointAt(float t, out Vector3 position, out Quaternion rotation)
    {
        t = Mathf.Clamp01(t);

        Vector3 start = entry != null ? entry.position : transform.position;
        Vector3 end = exit != null ? exit.position : transform.position + transform.forward * 10f;

        position = Vector3.Lerp(start, end, t);
        Vector3 forward = (end - start).sqrMagnitude > 0.0001f ? (end - start).normalized : transform.forward;
        rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
