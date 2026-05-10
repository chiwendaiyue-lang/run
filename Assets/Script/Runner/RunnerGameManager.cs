using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunnerGameManager : MonoBehaviour
{
    public static RunnerGameManager Instance { get; private set; }

    public enum GameFlowPhase
    {
        MainMenu = 0,
        Playing = 1,
        Victory = 2,
        Defeat = 3,
        /// <summary>已开始一局：生成赛道与预热物理，倒计时结束前玩家不能移动。</summary>
        Countdown = 4
    }

    [Header("Flow")]
    [Tooltip("为 true 时开局显示开始界面，点开始后才开始跑。")]
    public bool startInMainMenu = true;

    [Tooltip("点击开始后倒计时秒数（期间赛道已生成，人物仍不可动）。")]
    public int startCountdownSeconds = 3;

    [Tooltip("分数（含距离与金币）达到该值时胜利。")]
    public int winScoreThreshold = 10000;

    [Header("Scoring")]
    public float distanceScoreMultiplier = 1f;
    public int coinScoreMultiplier = 10;

    [Header("Survival")]
    public int startingLives = 3;

    public int Coins { get; private set; }
    public float Distance { get; private set; }
    public int Lives { get; private set; }
    public GameFlowPhase Phase { get; private set; }

    [Tooltip("胜利或失败后是否立即显示 UI 流程（由 RunnerFlowUI 使用）。")]
    public bool AwaitingDefeatOrVictoryEvent { get; set; }

    /// <summary>
    /// 赛道实例化后等待若干次 FixedUpdate，再允许玩家 Move。
    /// 独立构建里常见：首帧 Update 早于新 MeshCollider 进入物理世界，CharacterController 会穿透下落；编辑器帧序更“宽松”。
    /// </summary>
    public bool BlockPlayerMovementForPhysicsWarmup { get; private set; }

    private Coroutine m_PhysicsWarmupCoroutine;
    private Coroutine m_StartupSequenceCoroutine;

    public bool IsPlaying
    {
        get { return Phase == GameFlowPhase.Playing; }
    }

    /// <summary>主菜单后的倒计时阶段与正式跑步阶段都会刷赛道。</summary>
    public bool ShouldSpawnTrack
    {
        get
        {
            return Phase == GameFlowPhase.Countdown || Phase == GameFlowPhase.Playing;
        }
    }

    public bool CanPlayerMove()
    {
        return Phase == GameFlowPhase.Playing && !BlockPlayerMovementForPhysicsWarmup;
    }

    public bool IsGameOver
    {
        get { return Phase == GameFlowPhase.Defeat; }
    }

    public int Score
    {
        get
        {
            int distanceScore = Mathf.FloorToInt(Distance * distanceScoreMultiplier);
            return distanceScore + Coins * coinScoreMultiplier;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Lives = Mathf.Max(1, startingLives);
        if (startInMainMenu)
        {
            Phase = GameFlowPhase.MainMenu;
        }
        else
        {
            Phase = GameFlowPhase.MainMenu;
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        if (!startInMainMenu)
        {
            BeginRunFromMenu();
        }
        else if (Phase == GameFlowPhase.MainMenu && Object.FindObjectOfType<RunnerFlowUI>() == null)
        {
            BeginRunFromMenu();
        }
    }

    private void Update()
    {
        if (Phase == GameFlowPhase.Playing)
        {
            if (Score >= winScoreThreshold)
            {
                EnterVictory();
            }
        }
    }

    public void ReportTravel(float deltaDistance)
    {
        if (Phase != GameFlowPhase.Playing)
        {
            return;
        }

        Distance += Mathf.Max(0f, deltaDistance);
    }

    public void AddCoin(int amount)
    {
        if (Phase != GameFlowPhase.Playing)
        {
            return;
        }

        Coins += Mathf.Max(0, amount);
    }

    public void BeginRunFromMenu()
    {
        StopStartupCoroutines();

        Lives = Mathf.Max(1, startingLives);
        Coins = 0;
        Distance = 0f;
        Phase = GameFlowPhase.Countdown;
        AwaitingDefeatOrVictoryEvent = false;
        Time.timeScale = 1f;

        RunnerTrackSpawner spawner = Object.FindObjectOfType<RunnerTrackSpawner>();
        if (spawner != null)
        {
            spawner.EnsureInitialSegmentsIfPlaying();
        }

        RunnerPlayerController pl = Object.FindObjectOfType<RunnerPlayerController>();
        if (pl != null)
        {
            pl.EndMenuDance();
        }

        RequestPhysicsWarmupAfterTrackSpawn();
        m_StartupSequenceCoroutine = StartCoroutine(CoStartupSequenceAfterTrackSpawn());
    }

    private void StopStartupCoroutines()
    {
        if (m_StartupSequenceCoroutine != null)
        {
            StopCoroutine(m_StartupSequenceCoroutine);
            m_StartupSequenceCoroutine = null;
        }

        if (m_PhysicsWarmupCoroutine != null)
        {
            StopCoroutine(m_PhysicsWarmupCoroutine);
            m_PhysicsWarmupCoroutine = null;
        }

        BlockPlayerMovementForPhysicsWarmup = false;
    }

    public void RequestPhysicsWarmupAfterTrackSpawn()
    {
        if (m_PhysicsWarmupCoroutine != null)
        {
            StopCoroutine(m_PhysicsWarmupCoroutine);
        }

        m_PhysicsWarmupCoroutine = StartCoroutine(CoPhysicsWarmupAfterTrackSpawn());
    }

    private IEnumerator CoPhysicsWarmupAfterTrackSpawn()
    {
        BlockPlayerMovementForPhysicsWarmup = true;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Physics.SyncTransforms();
        RunnerPlayerController pl = Object.FindObjectOfType<RunnerPlayerController>();
        if (pl != null)
        {
            pl.SnapToGroundBelow(120f);
            pl.HardResetCharacterController();
        }

        BlockPlayerMovementForPhysicsWarmup = false;
        m_PhysicsWarmupCoroutine = null;
    }

    private IEnumerator CoStartupSequenceAfterTrackSpawn()
    {
        yield return StartCoroutine(CoWaitUntilPhysicsWarmupDone());

        RunnerFlowUI flow = RunnerFlowUI.Instance;
        if (flow != null)
        {
            yield return StartCoroutine(flow.PlayStartCountdown(Mathf.Max(1, startCountdownSeconds)));
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.75f);
        }

        Phase = GameFlowPhase.Playing;
        if (flow != null)
        {
            flow.ScheduleHideCountdownAfterRunSeconds(Mathf.Max(0f, flow.hideCountdownOverlayAfterRunSeconds));
        }

        m_StartupSequenceCoroutine = null;
    }

    private IEnumerator CoWaitUntilPhysicsWarmupDone()
    {
        float waited = 0f;
        const float timeout = 6f;
        while (BlockPlayerMovementForPhysicsWarmup && waited < timeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void EnterVictory()
    {
        if (Phase != GameFlowPhase.Playing)
        {
            return;
        }

        Phase = GameFlowPhase.Victory;
        Time.timeScale = 1f;
        AwaitingDefeatOrVictoryEvent = true;
        RunnerPlayerController pl = Object.FindObjectOfType<RunnerPlayerController>();
        if (pl != null)
        {
            pl.StartVictorySequence();
        }
        else
        {
            AwaitingDefeatOrVictoryEvent = false;
            if (RunnerFlowUI.Instance != null)
            {
                RunnerFlowUI.Instance.ShowVictoryPanel();
            }
        }
    }

    public void GameOver()
    {
        if (Phase == GameFlowPhase.Defeat)
        {
            return;
        }

        bool wasPlay = Phase == GameFlowPhase.Playing;
        if (!wasPlay)
        {
            return;
        }

        Phase = GameFlowPhase.Defeat;
        Time.timeScale = 1f;
        AwaitingDefeatOrVictoryEvent = true;
    }

    public bool LoseLife()
    {
        if (Phase != GameFlowPhase.Playing)
        {
            return true;
        }

        Lives = Mathf.Max(0, Lives - 1);
        if (Lives <= 0)
        {
            GameOver();
            return true;
        }

        return false;
    }

    public void OnDefeatSequenceFinished()
    {
        AwaitingDefeatOrVictoryEvent = false;
    }

    public void OnVictorySequenceFinished()
    {
        AwaitingDefeatOrVictoryEvent = false;
    }

    public void ReturnToMenuWithoutReload()
    {
        Lives = Mathf.Max(1, startingLives);
        Coins = 0;
        Distance = 0f;
        Phase = GameFlowPhase.MainMenu;
        AwaitingDefeatOrVictoryEvent = false;
        Time.timeScale = 1f;
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }
}
