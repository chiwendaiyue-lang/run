using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class RunnerTrackSpawner : MonoBehaviour
{
    private struct SpawnedObstacleInfo
    {
        public float distanceAlongSegment;
        public float lanePosition;
    }

    [Header("References")]
    public RunnerPlayerController player;
    public RunnerTrackSegment[] segmentPrefabs;
    public RunnerObstacle[] obstaclePrefabs;
    public RunnerCollectible coinPrefab;
    public Transform segmentRoot;

    [Header("Track")]
    public int initialSegmentCount = 6;
    public float despawnBehindDistance = 35f;

    [Header("Lane")]
    public float laneOffset = 2.5f;
    public float laneCenterOffset = 0f;
    [Tooltip("限制可玩车道宽度（障碍/金币都只刷在这个半宽内）。")]
    public float playableHalfWidth = 3.5f;

    [Header("Populate")]
    public int minObstaclesPerSegment = 1;
    public int maxObstaclesPerSegment = 3;
    public int minCoinsPerLine = 4;
    public int maxCoinsPerLine = 8;
    public int coinLinesPerSegment = 1;
    public float spawnPadding = 0.15f;
    public float obstacleHeightOffset = 0.9f;
    public float coinHeightOffset = 1.6f;
    [Tooltip("障碍之间最小前后间距（米）。")]
    public float minObstacleForwardGap = 8f;
    [Tooltip("金币与障碍在前后方向上的最小安全距离（米）。")]
    public float coinObstacleForwardClearance = 2.5f;
    [Tooltip("统一道路视觉宽度（按 prefab 实际宽度自动缩放）。")]
    public bool useFixedSegmentVisualWidth = true;
    public float targetSegmentVisualWidth = 12f;
    [Tooltip("关闭固定宽度时使用的乘法缩放。")]
    public float segmentWidthScale = 1.25f;
    public float obstacleSideLaneMultiplier = 0.9f;
    [Tooltip("玩家跑过该距离后才开始生成障碍，避免开局秒撞。")]
    public float obstacleSpawnStartDistance = 45f;

    [Header("Obstacle Tuning")]
    public float dogScaleMultiplier = 0.6f;
    public float ratScaleMultiplier = 0.5f;
    [Tooltip("横杆/路障类障碍（barrier/roadworks）的刷新权重。")]
    public float barrierObstacleWeight = 0.25f;
    [Tooltip("挡路类障碍（bin/dog/wheely）的刷新权重。")]
    public float blockingObstacleWeight = 1.35f;
    [Tooltip("其它障碍的刷新权重。")]
    public float otherObstacleWeight = 0.85f;

    [Header("Debug Visibility")]
    [Tooltip("开启后，即使美术资源丢失，也会强制显示调试障碍/硬币。")]
    public bool forceDebugVisuals = true;

    private readonly Queue<RunnerTrackSegment> m_ActiveSegments = new Queue<RunnerTrackSegment>();
    private Vector3 m_NextEntryPosition = Vector3.zero;
    private Quaternion m_NextEntryRotation = Quaternion.identity;
    private float m_TotalSpawnedTrackDistance;
    private RunnerTrackSegment m_FallbackSegmentPrefab;
    private RunnerObstacle m_FallbackObstaclePrefab;
    private RunnerCollectible m_FallbackCoinPrefab;
    private bool m_InitialSegmentsSpawned;

    private void Start()
    {
        if (segmentRoot == null)
        {
            segmentRoot = transform;
        }

        // 非主菜单开局：在首帧 Update 前生成赛道，避免独立构建里脚本顺序导致玩家先移动再生成碰撞体而掉穿地面。
        EnsureInitialSegmentsIfPlaying();
    }

    /// <summary>
    /// 在判定为 Playing 且尚未生成时，立刻生成首批赛道（应在 BeginRunFromMenu 等切换玩法状态时同步调用）。
    /// </summary>
    public void EnsureInitialSegmentsIfPlaying()
    {
        if (RunnerGameManager.Instance == null || !RunnerGameManager.Instance.ShouldSpawnTrack)
        {
            return;
        }

        if (m_InitialSegmentsSpawned)
        {
            return;
        }

        if (segmentRoot == null)
        {
            segmentRoot = transform;
        }

        for (int i = 0; i < initialSegmentCount; i++)
        {
            SpawnNextSegment();
        }

        m_InitialSegmentsSpawned = true;
        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (player == null || RunnerGameManager.Instance == null)
        {
            return;
        }

        if (!RunnerGameManager.Instance.ShouldSpawnTrack)
        {
            return;
        }

        if (!m_InitialSegmentsSpawned)
        {
            EnsureInitialSegmentsIfPlaying();
        }

        while (m_ActiveSegments.Count < initialSegmentCount)
        {
            SpawnNextSegment();
        }

        while (m_ActiveSegments.Count > 0)
        {
            RunnerTrackSegment oldest = m_ActiveSegments.Peek();
            Vector3 oldestExitWorld = oldest.transform.TransformPoint(oldest.ExitLocalPosition);
            if (player.transform.position.z - oldestExitWorld.z > despawnBehindDistance)
            {
                m_ActiveSegments.Dequeue();
                Destroy(oldest.gameObject);
            }
            else
            {
                break;
            }
        }
    }

    private void SpawnNextSegment()
    {
        RunnerTrackSegment prefab = GetRandomSegmentPrefab();
        RunnerTrackSegment segment = Instantiate(prefab, segmentRoot);
        segment.gameObject.SetActive(true);
        ApplySegmentWidthScale(segment);
        PrepareSegment(segment);

        float segmentStartDistance = m_TotalSpawnedTrackDistance;
        Vector3 entryWorldBeforeUpdate = m_NextEntryPosition;
        Quaternion rootRotation = m_NextEntryRotation * Quaternion.Inverse(segment.EntryLocalRotation);
        Vector3 rootPosition = m_NextEntryPosition - rootRotation * segment.EntryLocalPosition;

        segment.transform.SetPositionAndRotation(rootPosition, rootRotation);
        m_ActiveSegments.Enqueue(segment);

        m_NextEntryPosition = segment.transform.TransformPoint(segment.ExitLocalPosition);
        m_NextEntryRotation = segment.transform.rotation * segment.ExitLocalRotation;
        float segmentWorldLength = Vector3.Distance(entryWorldBeforeUpdate, m_NextEntryPosition);
        float segmentEndDistance = segmentStartDistance + segmentWorldLength;
        m_TotalSpawnedTrackDistance = segmentEndDistance;

        PopulateSegment(segment, segmentStartDistance, segmentEndDistance);
    }

    private void PrepareSegment(RunnerTrackSegment segment)
    {
        if (segment.entry == null || segment.exit == null)
        {
            segment.AutoAssignMarkers();
        }

        if (segment.entry == null || segment.exit == null)
        {
            // 缺失标记时，创建默认 entry/exit 防止玩法中断。
            Transform entry = new GameObject("Entry").transform;
            entry.SetParent(segment.transform, false);
            entry.localPosition = Vector3.zero;
            entry.localRotation = Quaternion.identity;

            Transform exit = new GameObject("Exit").transform;
            exit.SetParent(segment.transform, false);
            exit.localPosition = Vector3.forward * 30f;
            exit.localRotation = Quaternion.identity;

            segment.entry = entry;
            segment.exit = exit;
        }

        EnsureSegmentColliders(segment.gameObject);
    }

    private RunnerTrackSegment GetRandomSegmentPrefab()
    {
        if (segmentPrefabs != null && segmentPrefabs.Length > 0)
        {
            for (int attempt = 0; attempt < segmentPrefabs.Length * 4; attempt++)
            {
                int idx = Random.Range(0, segmentPrefabs.Length);
                if (segmentPrefabs[idx] != null)
                {
                    return segmentPrefabs[idx];
                }
            }

            for (int i = 0; i < segmentPrefabs.Length; i++)
            {
                if (segmentPrefabs[i] != null)
                {
                    return segmentPrefabs[i];
                }
            }
        }

        if (m_FallbackSegmentPrefab == null)
        {
            // 回退：临时生成一个纯地板段，保证游戏可运行。
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "FallbackSegmentPrefab";
            fallback.transform.localScale = new Vector3(12f, 0.5f, 30f);
            fallback.SetActive(false);

            RunnerTrackSegment segment = fallback.AddComponent<RunnerTrackSegment>();
            Transform entry = new GameObject("Entry").transform;
            entry.SetParent(fallback.transform, false);
            entry.localPosition = new Vector3(0f, 0.25f, -15f);

            Transform exit = new GameObject("Exit").transform;
            exit.SetParent(fallback.transform, false);
            exit.localPosition = new Vector3(0f, 0.25f, 15f);

            segment.entry = entry;
            segment.exit = exit;
            m_FallbackSegmentPrefab = segment;
        }

        return m_FallbackSegmentPrefab;
    }

    private void PopulateSegment(RunnerTrackSegment segment, float segmentStartDistance, float segmentEndDistance)
    {
        Vector3 start = segment.transform.TransformPoint(segment.EntryLocalPosition);
        Vector3 end = segment.transform.TransformPoint(segment.ExitLocalPosition);
        Vector3 forward = (end - start).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float segmentLength = Vector3.Distance(start, end);

        if (segmentLength < 6f)
        {
            return;
        }

        bool canSpawnObstacles = segmentEndDistance >= obstacleSpawnStartDistance;
        List<SpawnedObstacleInfo> spawnedObstacles = new List<SpawnedObstacleInfo>();

        if (canSpawnObstacles)
        {
            int obstacleCount = Random.Range(minObstaclesPerSegment, maxObstaclesPerSegment + 1);
            for (int i = 0; i < obstacleCount; i++)
            {
                const int maxAttempts = 20;
                bool spawned = false;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    float t = Random.Range(spawnPadding, 1f - spawnPadding);
                    float distanceAlong = t * segmentLength;
                    if (HasNearbyObstacle(distanceAlong, spawnedObstacles, minObstacleForwardGap))
                    {
                        continue;
                    }

                    RunnerObstacle obstaclePrefab = GetRandomObstaclePrefab();
                    int lane = GetLaneForObstacle(obstaclePrefab);
                    float lanePos = laneCenterOffset + lane * laneOffset * (lane == 0 ? 1f : obstacleSideLaneMultiplier);
                    lanePos = Mathf.Clamp(lanePos, -playableHalfWidth, playableHalfWidth);
                    Vector3 position = Vector3.Lerp(start, end, t) + right * lanePos + Vector3.up * obstacleHeightOffset;

                    RunnerObstacle obstacle = Instantiate(obstaclePrefab, position, Quaternion.LookRotation(forward, Vector3.up), segment.transform);
                    obstacle.gameObject.SetActive(true);
                    ApplyObstacleScaleTuning(obstacle);
                    EnsureSolidCollider(obstacle.gameObject);
                    EnsureObstacleVisible(obstacle.gameObject, forceDebugVisuals);

                    spawnedObstacles.Add(new SpawnedObstacleInfo
                    {
                        distanceAlongSegment = distanceAlong,
                        lanePosition = lanePos
                    });

                    spawned = true;
                    break;
                }

                if (!spawned)
                {
                    break;
                }
            }
        }

        for (int line = 0; line < coinLinesPerSegment; line++)
        {
            int lane = Random.Range(-1, 2);
            int coinCount = Random.Range(minCoinsPerLine, maxCoinsPerLine + 1);
            float startT = Random.Range(spawnPadding, 0.6f);
            float spacingT = 0.03f;

            for (int i = 0; i < coinCount; i++)
            {
                float t = Mathf.Clamp01(startT + i * spacingT);
                if (t >= 1f - spawnPadding)
                {
                    break;
                }

                float lanePos = laneCenterOffset + lane * laneOffset;
                lanePos = Mathf.Clamp(lanePos, -playableHalfWidth, playableHalfWidth);
                float distanceAlong = t * segmentLength;
                if (IsCoinBlockedByObstacle(distanceAlong, lanePos, spawnedObstacles))
                {
                    continue;
                }

                Vector3 position = Vector3.Lerp(start, end, t) + right * lanePos + Vector3.up * coinHeightOffset;
                RunnerCollectible coin = Instantiate(GetCoinPrefab(), position, Quaternion.identity, segment.transform);
                coin.gameObject.SetActive(true);
                EnsureCollectibleVisible(coin.gameObject, forceDebugVisuals);

                Collider coinCollider = coin.GetComponent<Collider>();
                if (coinCollider == null)
                {
                    SphereCollider sc = coin.gameObject.AddComponent<SphereCollider>();
                    sc.radius = 0.5f;
                    sc.isTrigger = true;
                }
            }
        }
    }

    private RunnerObstacle GetRandomObstaclePrefab()
    {
        if (obstaclePrefabs != null && obstaclePrefabs.Length > 0)
        {
            // 保险过滤：即使 Inspector 里手动放了老鼠，也不参与生成。
            List<RunnerObstacle> filtered = new List<RunnerObstacle>();
            for (int i = 0; i < obstaclePrefabs.Length; i++)
            {
                RunnerObstacle item = obstaclePrefabs[i];
                if (item == null)
                {
                    continue;
                }

                if (item.name.ToLowerInvariant().Contains("rat"))
                {
                    continue;
                }

                filtered.Add(item);
            }

            if (filtered.Count > 0)
            {
                return GetWeightedRandomObstacle(filtered);
            }
        }

        if (m_FallbackObstaclePrefab == null)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "FallbackObstaclePrefab";
            fallback.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
            fallback.SetActive(false);
            m_FallbackObstaclePrefab = fallback.AddComponent<RunnerObstacle>();
        }

        return m_FallbackObstaclePrefab;
    }

    private RunnerCollectible GetCoinPrefab()
    {
        if (coinPrefab != null)
        {
            return coinPrefab;
        }

        if (m_FallbackCoinPrefab == null)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.name = "FallbackCoinPrefab";
            fallback.transform.localScale = Vector3.one * 0.6f;
            fallback.SetActive(false);
            RunnerCollectible collectible = fallback.AddComponent<RunnerCollectible>();
            Collider col = fallback.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            m_FallbackCoinPrefab = collectible;
        }

        return m_FallbackCoinPrefab;
    }

    private static void EnsureSolidCollider(GameObject target)
    {
        Collider col = target.GetComponentInChildren<Collider>();
        if (col == null)
        {
            Renderer rend = target.GetComponentInChildren<Renderer>();
            BoxCollider box = target.AddComponent<BoxCollider>();
            if (rend != null)
            {
                Bounds worldBounds = rend.bounds;
                box.center = target.transform.InverseTransformPoint(worldBounds.center);
                box.size = worldBounds.size;
            }
            col = box;
        }

        col.isTrigger = false;
    }

    private static void EnsureObstacleVisible(GameObject target, bool forceDebugVisual)
    {
        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend != null && rend.enabled && !forceDebugVisual)
        {
            return;
        }

        Transform existing = target.transform.Find("FallbackObstacleVisual");
        if (existing == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "FallbackObstacleVisual";
            visual.transform.SetParent(target.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0f, 0f);
            visual.transform.localScale = new Vector3(1.4f, 1.8f, 1.4f);

            Renderer vr = visual.GetComponent<Renderer>();
            if (vr != null)
            {
                vr.material.color = new Color(1f, 0.3f, 0.3f, 1f);
            }

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }
        }
    }

    private static void EnsureCollectibleVisible(GameObject target, bool forceDebugVisual)
    {
        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend != null && rend.enabled && !forceDebugVisual)
        {
            return;
        }

        Transform existing = target.transform.Find("FallbackCoinVisual");
        if (existing == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "FallbackCoinVisual";
            visual.transform.SetParent(target.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 0.7f;

            Renderer vr = visual.GetComponent<Renderer>();
            if (vr != null)
            {
                vr.material.color = new Color(1f, 0.85f, 0.1f, 1f);
            }

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }
        }
    }

    private static bool HasWalkableSolidCollider(GameObject root)
    {
        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        if (cols == null)
        {
            return false;
        }

        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null || !c.enabled || c.isTrigger)
            {
                continue;
            }

            Vector3 s = c.bounds.size;
            if (s.x * s.y * s.z < 1e-4f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void AddSegmentBoundsFallbackCollider(GameObject segmentRoot)
    {
        Renderer[] rends = segmentRoot.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0)
        {
            return;
        }

        Bounds world = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            if (rends[i] != null && rends[i].enabled)
            {
                world.Encapsulate(rends[i].bounds);
            }
        }

        BoxCollider box = segmentRoot.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = segmentRoot.AddComponent<BoxCollider>();
        }

        Transform t = segmentRoot.transform;
        box.isTrigger = false;
        box.enabled = true;
        box.center = t.InverseTransformPoint(world.center);
        Vector3 ls = t.lossyScale;
        box.size = new Vector3(
            world.size.x / Mathf.Max(Mathf.Abs(ls.x), 1e-3f),
            world.size.y / Mathf.Max(Mathf.Abs(ls.y), 1e-3f),
            world.size.z / Mathf.Max(Mathf.Abs(ls.z), 1e-3f));
    }

    private static void EnsureSegmentColliders(GameObject segmentRoot)
    {
        // 以前只要有任意 Collider（含 Trigger）就跳过，会导致“只有触发器没有地板”的 prefab 在运行时掉穿。
        if (HasWalkableSolidCollider(segmentRoot))
        {
            return;
        }

        MeshFilter[] meshFilters = segmentRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null)
            {
                continue;
            }

            if (mf.GetComponent<MeshCollider>() != null)
            {
                continue;
            }

            MeshCollider meshCollider = mf.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mf.sharedMesh;
            meshCollider.convex = false;
            meshCollider.enabled = true;
        }

        if (!HasWalkableSolidCollider(segmentRoot))
        {
            AddSegmentBoundsFallbackCollider(segmentRoot);
        }
    }

    private void ApplySegmentWidthScale(RunnerTrackSegment segment)
    {
        Vector3 scale = segment.transform.localScale;

        if (useFixedSegmentVisualWidth && targetSegmentVisualWidth > 0.01f)
        {
            float currentWidth = EstimateSegmentVisualWidth(segment.gameObject);
            if (currentWidth > 0.01f)
            {
                float factor = targetSegmentVisualWidth / currentWidth;
                scale.x *= factor;
            }
        }
        else if (segmentWidthScale > 0.01f && !Mathf.Approximately(segmentWidthScale, 1f))
        {
            scale.x *= segmentWidthScale;
        }

        segment.transform.localScale = scale;
    }

    private static float EstimateSegmentVisualWidth(GameObject segmentRoot)
    {
        Renderer[] renderers = segmentRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return 0f;
        }

        bool hasBounds = false;
        Bounds combined = new Bounds(segmentRoot.transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = r.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(r.bounds);
            }
        }

        return hasBounds ? combined.size.x : 0f;
    }

    private int GetLaneForObstacle(RunnerObstacle obstaclePrefab)
    {
        if (obstaclePrefab == null)
        {
            return Random.Range(-1, 2);
        }

        string name = obstaclePrefab.name.ToLowerInvariant();
        if (IsBarrierObstacleName(name))
        {
            // 横杆类障碍优先放在中间道，减少被两侧建筑遮挡。
            return 0;
        }

        return Random.Range(-1, 2);
    }

    private void ApplyObstacleScaleTuning(RunnerObstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        string name = obstacle.name.ToLowerInvariant();
        if (name.Contains("dog"))
        {
            obstacle.transform.localScale *= dogScaleMultiplier;
        }
        else if (name.Contains("rat"))
        {
            obstacle.transform.localScale *= ratScaleMultiplier;
        }
    }

    private static bool HasNearbyObstacle(float distanceAlong, List<SpawnedObstacleInfo> spawnedObstacles, float minGap)
    {
        for (int i = 0; i < spawnedObstacles.Count; i++)
        {
            if (Mathf.Abs(spawnedObstacles[i].distanceAlongSegment - distanceAlong) < minGap)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsCoinBlockedByObstacle(float coinDistanceAlong, float coinLanePos, List<SpawnedObstacleInfo> spawnedObstacles)
    {
        const float sameLaneThreshold = 0.8f;

        for (int i = 0; i < spawnedObstacles.Count; i++)
        {
            SpawnedObstacleInfo info = spawnedObstacles[i];
            bool sameLane = Mathf.Abs(info.lanePosition - coinLanePos) <= sameLaneThreshold;
            bool tooClose = Mathf.Abs(info.distanceAlongSegment - coinDistanceAlong) <= coinObstacleForwardClearance;

            if (sameLane && tooClose)
            {
                return true;
            }
        }

        return false;
    }

    private RunnerObstacle GetWeightedRandomObstacle(List<RunnerObstacle> candidates)
    {
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += GetObstacleSpawnWeight(candidates[i]);
        }

        if (totalWeight <= 0.001f)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        float pick = Random.Range(0f, totalWeight);
        float running = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            running += GetObstacleSpawnWeight(candidates[i]);
            if (pick <= running)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private float GetObstacleSpawnWeight(RunnerObstacle obstacle)
    {
        if (obstacle == null)
        {
            return 0f;
        }

        string name = obstacle.name.ToLowerInvariant();
        if (IsBarrierObstacleName(name))
        {
            return Mathf.Max(0.01f, barrierObstacleWeight);
        }

        if (name.Contains("bin") || name.Contains("dog") || name.Contains("wheely"))
        {
            return Mathf.Max(0.01f, blockingObstacleWeight);
        }

        return Mathf.Max(0.01f, otherObstacleWeight);
    }

    private static bool IsBarrierObstacleName(string lowerName)
    {
        return lowerName.Contains("barrier") || lowerName.Contains("roadworks");
    }
}
