using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public static class RunnerQuickSetup
{
    private const string k_BodyGuard01Path = "Assets/BodyGuards/Meshes/SkelMesh_Bodyguard_01.fbx";
    private const string k_FallbackCatPath = "Assets/models/Cat.fbx";
    private const string k_PlayerControllerPath = "Assets/Animation/controller/Player.controller";
    private const string k_RunControllerPath = "Assets/Animation/controller/game/fast run.controller";
    private const string k_DanceControllerPath = "Assets/Animation/controller/win/dance.controller";
    private const string k_RunToStopControllerPath = "Assets/Animation/controller/win/run to stop.controller";
    private static readonly string[] k_ObstaclePaths =
    {
        "Assets/download/env/Obstacles/ObstacleBin.prefab",
        "Assets/download/env/Obstacles/ObstacleHighBarrier.prefab",
        "Assets/download/env/Obstacles/ObstacleLowBarrier.prefab",
        "Assets/download/env/Obstacles/ObstacleRoadworksBarrier.prefab",
        "Assets/download/env/Obstacles/ObstacleRoadworksCone.prefab",
        "Assets/download/env/Obstacles/ObstacleWheelyBin.prefab",
        "Assets/download/env/Obstacles/ObstacleDog.prefab"
    };
    private static readonly string[] k_CoinVisualPaths =
    {
        "Assets/models/Heart.fbx",
        "Assets/models/Clover.fbx",
        "Assets/models/Magnet.fbx",
        "Assets/models/Sardines.fbx"
    };
    private const string k_GeneratedCoinPrefabPath = "Assets/Generated/RunnerCoin.prefab";
    private const string k_GeneratedCoinMaterialPath = "Assets/Generated/RunnerCoin.mat";

    [MenuItem("Tools/Runner/Quick Setup Current Scene")]
    public static void SetupCurrentScene()
    {
        CreateGameManagerIfNeeded();
        RunnerPlayerController player = CreatePlayerIfNeeded();
        ConfigurePlayerVisual(player);
        RunnerTrackSpawner spawner = CreateSpawnerIfNeeded(player);
        CreateHudIfNeeded();
        CreateFlowUIRootIfNeeded(player);
        CreateCameraIfNeeded(player.transform);

        Selection.activeObject = spawner.gameObject;
        Debug.Log("Runner quick setup complete. Check RunnerSpawner, RunnerFlowUI (menu/winner) and adjust prefabs if needed.");
    }

    [MenuItem("Tools/Runner/Attach Preferred Player Model")]
    public static void AttachPreferredPlayerModel()
    {
        RunnerPlayerController player = Object.FindObjectOfType<RunnerPlayerController>();
        if (player == null)
        {
            Debug.LogError("RunnerPlayerController not found in current scene.");
            return;
        }

        ConfigurePlayerVisual(player);
        Selection.activeObject = player.gameObject;
        Debug.Log("Player model and animator configured.");
    }

    private static void CreateGameManagerIfNeeded()
    {
        if (Object.FindObjectOfType<RunnerGameManager>() != null)
        {
            return;
        }

        GameObject go = new GameObject("RunnerGameManager");
        go.AddComponent<RunnerGameManager>();
    }

    private static RunnerPlayerController CreatePlayerIfNeeded()
    {
        RunnerPlayerController existing = Object.FindObjectOfType<RunnerPlayerController>();
        if (existing != null)
        {
            EnsureCharacterControllerShape(existing.GetComponent<CharacterController>());
            return existing;
        }

        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "RunnerPlayer";
        player.transform.position = new Vector3(0f, 1f, 0f);

        Collider primitiveCollider = player.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            Object.DestroyImmediate(primitiveCollider);
        }

        CharacterController cc = player.AddComponent<CharacterController>();
        EnsureCharacterControllerShape(cc);

        RunnerPlayerController controller = player.AddComponent<RunnerPlayerController>();
        return controller;
    }

    private static void ConfigurePlayerVisual(RunnerPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        GameObject preferredModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_BodyGuard01Path);
        if (preferredModelPrefab == null)
        {
            preferredModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_FallbackCatPath);
        }

        if (preferredModelPrefab == null)
        {
            Debug.LogWarning("No player model prefab found to attach.");
            return;
        }

        // 清理 RunnerPlayer 根节点上遗留的猫模型组件，避免“双模型叠加”
        CleanupRootMeshComponents(player.gameObject);

        Transform oldVisual = player.transform.Find("PlayerVisual");
        if (oldVisual != null)
        {
            Object.DestroyImmediate(oldVisual.gameObject);
        }

        Transform visualRoot;
        {
            GameObject visualInstance = PrefabUtility.InstantiatePrefab(preferredModelPrefab) as GameObject;
            if (visualInstance == null)
            {
                return;
            }

            visualInstance.name = "PlayerVisual";
            visualInstance.transform.SetParent(player.transform, false);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;
            visualRoot = visualInstance.transform;
        }

        visualRoot.localScale = Vector3.one * 1.25f;

        Animator animator = visualRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visualRoot.GetComponentInChildren<Animator>();
        }
        if (animator == null)
        {
            animator = visualRoot.gameObject.AddComponent<Animator>();
        }

        RuntimeAnimatorController runController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_PlayerControllerPath);
        if (runController == null)
        {
            runController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_RunControllerPath);
        }
        if (runController != null)
        {
            animator.runtimeAnimatorController = runController;
        }
        animator.applyRootMotion = false;

        player.animator = animator;
        EnsureCharacterControllerShape(player.GetComponent<CharacterController>());
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(animator);
    }

    private static void CleanupRootMeshComponents(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        MeshFilter mf = root.GetComponent<MeshFilter>();
        if (mf != null)
        {
            Object.DestroyImmediate(mf);
        }

        MeshRenderer mr = root.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Object.DestroyImmediate(mr);
        }

        SkinnedMeshRenderer smr = root.GetComponent<SkinnedMeshRenderer>();
        if (smr != null)
        {
            Object.DestroyImmediate(smr);
        }
    }

    private static void EnsureCharacterControllerShape(CharacterController cc)
    {
        if (cc == null)
        {
            return;
        }

        cc.height = 2.3f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1.15f, 0f);
    }

    private static RunnerTrackSpawner CreateSpawnerIfNeeded(RunnerPlayerController player)
    {
        RunnerTrackSpawner existing = Object.FindObjectOfType<RunnerTrackSpawner>();
        if (existing != null)
        {
            if (existing.player == null)
            {
                existing.player = player;
            }
            existing.obstaclePrefabs = LoadObstaclePrefabs();
            existing.coinPrefab = EnsureCoinPrefabAsset();
            existing.forceDebugVisuals = false;
            existing.useFixedSegmentVisualWidth = true;
            existing.targetSegmentVisualWidth = 13.5f;
            existing.segmentWidthScale = 1.0f;
            existing.laneOffset = 1.95f;
            existing.laneCenterOffset = 0f;
            existing.playableHalfWidth = 2.2f;
            existing.coinHeightOffset = 1.6f;
            existing.minObstacleForwardGap = 9f;
            existing.coinObstacleForwardClearance = 2.8f;
            existing.obstacleSideLaneMultiplier = 0.82f;
            existing.barrierObstacleWeight = 0.22f;
            existing.blockingObstacleWeight = 1.45f;
            existing.otherObstacleWeight = 0.85f;
            existing.dogScaleMultiplier = 0.55f;
            existing.ratScaleMultiplier = 0.45f;
            if (player != null)
            {
                player.laneOffset = existing.laneOffset;
            }
            EditorUtility.SetDirty(existing);
            return existing;
        }

        GameObject spawnerGo = new GameObject("RunnerSpawner");
        RunnerTrackSpawner spawner = spawnerGo.AddComponent<RunnerTrackSpawner>();
        spawner.player = player;

        RunnerTrackSegment[] segments = new RunnerTrackSegment[]
        {
            LoadSegment("Assets/download/env/Industrial/IndustrialWarehouse01.prefab"),
            LoadSegment("Assets/download/env/Industrial/IndustrialWarehouse02.prefab"),
            LoadSegment("Assets/download/env/Industrial/IndustrialWarehouse03.prefab"),
            LoadSegment("Assets/download/env/Urban/UrbanBuilding01.prefab"),
            LoadSegment("Assets/download/env/Urban/UrbanBuilding02.prefab"),
            LoadSegment("Assets/download/env/Urban/UrbanBuilding03.prefab")
        };

        spawner.segmentPrefabs = FilterNull(segments);
        spawner.obstaclePrefabs = LoadObstaclePrefabs();
        spawner.coinPrefab = EnsureCoinPrefabAsset();
        spawner.forceDebugVisuals = false;
        spawner.useFixedSegmentVisualWidth = true;
        spawner.targetSegmentVisualWidth = 13.5f;
        spawner.segmentWidthScale = 1.0f;
        spawner.laneOffset = 1.95f;
        spawner.laneCenterOffset = 0f;
        spawner.playableHalfWidth = 2.2f;
        spawner.coinHeightOffset = 1.6f;
        spawner.minObstacleForwardGap = 9f;
        spawner.coinObstacleForwardClearance = 2.8f;
        spawner.obstacleSideLaneMultiplier = 0.82f;
        spawner.barrierObstacleWeight = 0.22f;
        spawner.blockingObstacleWeight = 1.45f;
        spawner.otherObstacleWeight = 0.85f;
        spawner.dogScaleMultiplier = 0.55f;
        spawner.ratScaleMultiplier = 0.45f;
        if (player != null)
        {
            player.laneOffset = spawner.laneOffset;
        }
        return spawner;
    }

    private static void CreateHudIfNeeded()
    {
        if (Object.FindObjectOfType<RunnerHud>() != null)
        {
            return;
        }

        GameObject hud = new GameObject("RunnerHUD");
        hud.AddComponent<RunnerHud>();
    }

    [MenuItem("Tools/Runner/Create Flow UI (Canvas)")]
    public static void CreateFlowUIRootFromMenu()
    {
        RunnerPlayerController player = Object.FindObjectOfType<RunnerPlayerController>();
        CreateGameManagerIfNeeded();
        if (player == null)
        {
            player = CreatePlayerIfNeeded();
        }
        CreateFlowUIRootIfNeeded(player);
        Debug.Log("RunnerFlowUI canvas created. Link menu showcase / fonts if needed.");
    }

    private static void CreateFlowUIRootIfNeeded(RunnerPlayerController player)
    {
        if (Object.FindObjectOfType<RunnerFlowUI>() != null)
        {
            AssignFlowControllersToPlayerIfNeeded(player);
            return;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<StandaloneInputModule>();
        }

        GameObject root = new GameObject("RunnerFlowUI");
        Canvas cv = root.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler sc = root.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        RunnerFlowUI flow = root.AddComponent<RunnerFlowUI>();

        GameObject mainPanel = new GameObject("Panel_MainMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mainPanel.transform.SetParent(root.transform, false);
        RectTransform mrt = mainPanel.GetComponent<RectTransform>();
        _StretchToFull(mrt);
        mainPanel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.9f);

        GameObject startBtn = new GameObject("ButtonStart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        startBtn.transform.SetParent(mainPanel.transform, false);
        RectTransform brt = startBtn.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.4f);
        brt.anchorMax = new Vector2(0.5f, 0.4f);
        brt.sizeDelta = new Vector2(400, 80);
        brt.anchoredPosition = Vector2.zero;
        startBtn.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.35f, 1f);
        _CreateLabelUnder(startBtn.transform, "开始游戏", 32);
        GameObject mainTitle = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        mainTitle.transform.SetParent(mainPanel.transform, false);
        RectTransform tMain = mainTitle.GetComponent<RectTransform>();
        tMain.anchorMin = new Vector2(0.5f, 0.58f);
        tMain.anchorMax = new Vector2(0.5f, 0.58f);
        tMain.pivot = new Vector2(0.5f, 0.5f);
        tMain.sizeDelta = new Vector2(1000, 200);
        tMain.anchoredPosition = Vector2.zero;
        Text tMainT = mainTitle.GetComponent<Text>();
        tMainT.text = "跑酷";
        tMainT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tMainT.font == null)
        {
            tMainT.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        tMainT.alignment = TextAnchor.MiddleCenter;
        tMainT.color = Color.white;
        tMainT.fontSize = 56;

        GameObject vicPanel = new GameObject("Panel_Victory", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        vicPanel.transform.SetParent(root.transform, false);
        _StretchToFull(vicPanel.GetComponent<RectTransform>());
        vicPanel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.9f);
        vicPanel.SetActive(false);
        _CreateLabelUnder(vicPanel.transform, "你赢了！\n(分数达到 1000)", 40);
        GameObject vBtn = new GameObject("ButtonVicRestart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        vBtn.transform.SetParent(vicPanel.transform, false);
        RectTransform vbr = vBtn.GetComponent<RectTransform>();
        vbr.anchorMin = new Vector2(0.5f, 0.35f);
        vbr.anchorMax = new Vector2(0.5f, 0.35f);
        vbr.sizeDelta = new Vector2(400, 80);
        vBtn.GetComponent<Image>().color = new Color(0.25f, 0.4f, 0.6f, 1f);
        _CreateLabelUnder(vBtn.transform, "再来一局", 28);

        GameObject defPanel = new GameObject("Panel_Defeat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        defPanel.transform.SetParent(root.transform, false);
        _StretchToFull(defPanel.GetComponent<RectTransform>());
        defPanel.GetComponent<Image>().color = new Color(0.12f, 0.07f, 0.08f, 0.9f);
        defPanel.SetActive(false);
        _CreateLabelUnder(defPanel.transform, "再试一次", 40);
        GameObject dBtn = new GameObject("ButtonDefeatRestart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dBtn.transform.SetParent(defPanel.transform, false);
        RectTransform dbr = dBtn.GetComponent<RectTransform>();
        dbr.anchorMin = new Vector2(0.5f, 0.35f);
        dbr.anchorMax = new Vector2(0.5f, 0.35f);
        dbr.sizeDelta = new Vector2(400, 80);
        dBtn.GetComponent<Image>().color = new Color(0.5f, 0.25f, 0.25f, 1f);
        _CreateLabelUnder(dBtn.transform, "重新挑战", 28);

        flow.mainMenuRoot = mainPanel;
        flow.startButton = startBtn.GetComponent<Button>();
        flow.victoryPanel = vicPanel;
        flow.defeatPanel = defPanel;
        flow.victoryRestartButton = vBtn.GetComponent<Button>();
        flow.defeatRestartButton = dBtn.GetComponent<Button>();

        GameObject cdPanel = new GameObject("Panel_Countdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cdPanel.transform.SetParent(root.transform, false);
        _StretchToFull(cdPanel.GetComponent<RectTransform>());
        cdPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);
        cdPanel.SetActive(false);
        GameObject cdNumGo = new GameObject("CountdownNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        cdNumGo.transform.SetParent(cdPanel.transform, false);
        _StretchToFull(cdNumGo.GetComponent<RectTransform>());
        Text cdTxt = cdNumGo.GetComponent<Text>();
        cdTxt.text = "3";
        cdTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cdTxt.font == null)
        {
            cdTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        cdTxt.alignment = TextAnchor.MiddleCenter;
        cdTxt.color = Color.white;
        cdTxt.fontSize = 180;
        cdTxt.resizeTextForBestFit = true;
        cdTxt.resizeTextMinSize = 80;
        cdTxt.resizeTextMaxSize = 220;
        flow.countdownRoot = cdPanel;
        flow.countdownNumberText = cdTxt;

        RuntimeAnimatorController dance = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_DanceControllerPath);
        flow.danceController = dance;

        RunnerGameManager gm = Object.FindObjectOfType<RunnerGameManager>();
        if (gm != null)
        {
            gm.startInMainMenu = true;
        }

        AssignFlowControllersToPlayerIfNeeded(player);
        EditorUtility.SetDirty(flow);
    }

    private static void _StretchToFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.offsetMin = Vector2.zero;
    }

    private static void _CreateLabelUnder(Transform parent, string text, int fontSize)
    {
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(parent, false);
        _StretchToFull(textGo.GetComponent<RectTransform>());
        Text t = textGo.GetComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null)
        {
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = fontSize;
        t.resizeTextForBestFit = false;
    }

    private static void AssignFlowControllersToPlayerIfNeeded(RunnerPlayerController player)
    {
        if (player == null)
        {
            return;
        }
        RuntimeAnimatorController rs = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_RunToStopControllerPath);
        if (rs != null)
        {
            Undo.RecordObject(player, "assign run to stop");
            player.runToStopController = rs;
            EditorUtility.SetDirty(player);
        }
    }

    private static void CreateCameraIfNeeded(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        RunnerCameraFollow follow = cam.GetComponent<RunnerCameraFollow>();
        if (follow == null)
        {
            follow = cam.gameObject.AddComponent<RunnerCameraFollow>();
        }

        follow.target = target;
        cam.transform.position = target.position + new Vector3(0f, 5.5f, -8f);
        cam.transform.LookAt(target.position + Vector3.forward * 4f);
    }

    private static RunnerTrackSegment LoadSegment(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            return null;
        }

        RunnerTrackSegment segment = prefab.GetComponent<RunnerTrackSegment>();
        if (segment != null)
        {
            return segment;
        }

        // 如果原 prefab 没有 RunnerTrackSegment，自动加到 prefab 资产上。
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        RunnerTrackSegment added = root.GetComponent<RunnerTrackSegment>();
        if (added == null)
        {
            added = root.AddComponent<RunnerTrackSegment>();
            added.AutoAssignMarkers();
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        GameObject updated = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return updated != null ? updated.GetComponent<RunnerTrackSegment>() : null;
    }

    private static RunnerTrackSegment[] FilterNull(RunnerTrackSegment[] input)
    {
        int validCount = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] != null)
            {
                validCount++;
            }
        }

        RunnerTrackSegment[] output = new RunnerTrackSegment[validCount];
        int idx = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] != null)
            {
                output[idx++] = input[i];
            }
        }
        return output;
    }

    private static RunnerObstacle[] LoadObstaclePrefabs()
    {
        List<RunnerObstacle> results = new List<RunnerObstacle>();
        for (int i = 0; i < k_ObstaclePaths.Length; i++)
        {
            RunnerObstacle obstacle = EnsureObstacleComponentOnPrefab(k_ObstaclePaths[i]);
            if (obstacle != null)
            {
                results.Add(obstacle);
            }
        }

        return results.ToArray();
    }

    private static RunnerObstacle EnsureObstacleComponentOnPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            return null;
        }

        RunnerObstacle existing = prefab.GetComponent<RunnerObstacle>();
        if (existing != null)
        {
            return existing;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        RunnerObstacle added = root.GetComponent<RunnerObstacle>();
        if (added == null)
        {
            added = root.AddComponent<RunnerObstacle>();
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        GameObject updated = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return updated != null ? updated.GetComponent<RunnerObstacle>() : null;
    }

    private static RunnerCollectible EnsureCoinPrefabAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(k_GeneratedCoinPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(k_GeneratedCoinPrefabPath);
            AssetDatabase.Refresh();
        }

        EnsureGeneratedFolder();

        GameObject root = new GameObject("RunnerCoin");
        RunnerCollectible collectible = root.AddComponent<RunnerCollectible>();
        collectible.coinValue = 1;
        collectible.rotateSpeed = 180f;

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.radius = 0.45f;
        trigger.isTrigger = true;

        GameObject visualInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualInstance.name = "Visual";
        visualInstance.transform.SetParent(root.transform, false);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visualInstance.transform.localScale = new Vector3(0.6f, 0.08f, 0.6f);

        Renderer renderer = visualInstance.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = EnsureCoinMaterialAsset();
        }
        Collider col = visualInstance.GetComponent<Collider>();
        if (col != null)
        {
            Object.DestroyImmediate(col);
        }

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, k_GeneratedCoinPrefabPath);
        Object.DestroyImmediate(root);

        return prefabAsset != null ? prefabAsset.GetComponent<RunnerCollectible>() : null;
    }

    private static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
        {
            AssetDatabase.CreateFolder("Assets", "Generated");
        }
    }

    private static Material EnsureCoinMaterialAsset()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(k_GeneratedCoinMaterialPath);
        if (mat != null)
        {
            return mat;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        mat = new Material(shader);

        Color coinColor = new Color(1f, 0.82f, 0.08f, 1f);
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", coinColor);
        }
        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", coinColor);
        }
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", coinColor * 0.8f);
        }

        AssetDatabase.CreateAsset(mat, k_GeneratedCoinMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
