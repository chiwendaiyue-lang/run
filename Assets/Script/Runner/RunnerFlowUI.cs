using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RunnerFlowUI : MonoBehaviour
{
    public static RunnerFlowUI Instance { get; private set; }

    [Tooltip("倒计时结束显示 GO 后，人物跑动多少秒（游戏时间）再隐藏整块倒计时层。")]
    public float hideCountdownOverlayAfterRunSeconds = 3f;

    private Coroutine m_HideCountdownAfterRunCoroutine;

    [Header("主菜单 / 片头")]
    public GameObject mainMenuRoot;
    public Button startButton;
    [Tooltip("用于主菜单/胜利界面展示的跳舞用 Animator（可拖场景里的展示人物）。")]
    public Animator menuShowcaseAnimator;
    [Tooltip("dance.controller")]
    public RuntimeAnimatorController danceController;

    [Header("开局倒计时")]
    public GameObject countdownRoot;
    public Text countdownNumberText;

    [Header("胜利 / 失败")]
    public GameObject victoryPanel;
    public GameObject defeatPanel;
    public Button victoryRestartButton;
    public Button defeatRestartButton;
    [Tooltip("与主界面一致：胜利后再显示跳舞展示。")]
    public bool playDanceOnVictoryPanel = true;

    [Header("可选文案")]
    public Text startTitleText;
    public Text victoryTitleText;
    public Text defeatTitleText;
    [Header("Fallback")]
    [Tooltip("未指定展示角色时，自动让玩家本体在主菜单跳舞。")]
    public bool fallbackToPlayerAnimatorDance = true;
    [Header("UI 背景遮罩")]
    public Color mainMenuOverlayColor = new Color(0.08f, 0.1f, 0.12f, 0.45f);
    public Color victoryOverlayColor = new Color(0.08f, 0.1f, 0.12f, 0.4f);
    public Color defeatOverlayColor = new Color(0.12f, 0.07f, 0.08f, 0.5f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
        if (victoryRestartButton != null)
        {
            victoryRestartButton.onClick.AddListener(OnRestartFromVictory);
        }
        if (defeatRestartButton != null)
        {
            defeatRestartButton.onClick.AddListener(OnRestartFromDefeat);
        }
    }

    private void Start()
    {
        ApplyOverlayColors();

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
        if (defeatPanel != null)
        {
            defeatPanel.SetActive(false);
        }
        if (countdownRoot != null)
        {
            countdownRoot.SetActive(false);
        }

        CancelHideCountdownAfterRun();

        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        if (RunnerGameManager.Instance.startInMainMenu)
        {
            ShowMainMenuOnly();
        }
        else
        {
            if (mainMenuRoot != null)
            {
                mainMenuRoot.SetActive(false);
            }
        }
    }

    private void ApplyOverlayColors()
    {
        ApplyOverlayColor(mainMenuRoot, mainMenuOverlayColor);
        ApplyOverlayColor(victoryPanel, victoryOverlayColor);
        ApplyOverlayColor(defeatPanel, defeatOverlayColor);
    }

    private static void ApplyOverlayColor(GameObject panelRoot, Color color)
    {
        if (panelRoot == null)
        {
            return;
        }

        Image img = panelRoot.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
        }
    }

    public void ShowMainMenuOnly()
    {
        CancelHideCountdownAfterRun();
        if (countdownRoot != null)
        {
            countdownRoot.SetActive(false);
        }
        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(true);
        }
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
        if (defeatPanel != null)
        {
            defeatPanel.SetActive(false);
        }
        if (menuShowcaseAnimator != null)
        {
            menuShowcaseAnimator.gameObject.SetActive(true);
        }
        PlayDanceOnShowcase();
    }

    public IEnumerator PlayStartCountdown(int seconds)
    {
        seconds = Mathf.Max(1, seconds);
        if (countdownRoot != null)
        {
            countdownRoot.SetActive(true);
        }

        for (int i = seconds; i > 0; i--)
        {
            if (countdownNumberText != null)
            {
                countdownNumberText.text = i.ToString();
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        if (countdownNumberText != null)
        {
            countdownNumberText.text = "GO!";
        }

        // GO 在人物开始跑动后保留，由 ScheduleHideCountdownAfterRunSeconds 在若干秒后隐藏。
    }

    /// <summary>正式进入 Playing 且人物可走后调用：GO / 半透明层在跑动一段时间后消失。</summary>
    public void ScheduleHideCountdownAfterRunSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            HideCountdownOverlayImmediate();
            return;
        }

        if (m_HideCountdownAfterRunCoroutine != null)
        {
            StopCoroutine(m_HideCountdownAfterRunCoroutine);
        }

        m_HideCountdownAfterRunCoroutine = StartCoroutine(CoHideCountdownOverlayAfterGameplaySeconds(seconds));
    }

    private IEnumerator CoHideCountdownOverlayAfterGameplaySeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        HideCountdownOverlayImmediate();
        m_HideCountdownAfterRunCoroutine = null;
    }

    private void HideCountdownOverlayImmediate()
    {
        if (countdownRoot != null)
        {
            countdownRoot.SetActive(false);
        }
    }

    private void CancelHideCountdownAfterRun()
    {
        if (m_HideCountdownAfterRunCoroutine != null)
        {
            StopCoroutine(m_HideCountdownAfterRunCoroutine);
            m_HideCountdownAfterRunCoroutine = null;
        }
    }

    public void HideMainMenu()
    {
        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(false);
        }
        if (menuShowcaseAnimator != null)
        {
            menuShowcaseAnimator.gameObject.SetActive(false);
        }
        RunnerPlayerController player = Object.FindObjectOfType<RunnerPlayerController>();
        if (player != null)
        {
            player.EndMenuDance();
        }
    }

    public void ShowVictoryPanel()
    {
        CancelHideCountdownAfterRun();
        if (countdownRoot != null)
        {
            countdownRoot.SetActive(false);
        }
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
        if (defeatPanel != null)
        {
            defeatPanel.SetActive(false);
        }
        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(false);
        }
        if (playDanceOnVictoryPanel && menuShowcaseAnimator != null)
        {
            menuShowcaseAnimator.gameObject.SetActive(true);
            PlayDanceOnShowcase();
        }
    }

    public void ShowDefeatPanel()
    {
        CancelHideCountdownAfterRun();
        if (countdownRoot != null)
        {
            countdownRoot.SetActive(false);
        }
        if (defeatPanel != null)
        {
            defeatPanel.SetActive(true);
        }
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(false);
        }
    }

    private void OnStartClicked()
    {
        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        HideMainMenu();
        RunnerGameManager.Instance.BeginRunFromMenu();
    }

    private void OnRestartFromVictory()
    {
        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        RunnerGameManager.Instance.RestartScene();
    }

    private void OnRestartFromDefeat()
    {
        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        RunnerGameManager.Instance.RestartScene();
    }

    private void PlayDanceOnShowcase()
    {
        if (menuShowcaseAnimator == null)
        {
            if (fallbackToPlayerAnimatorDance)
            {
                RunnerPlayerController player = Object.FindObjectOfType<RunnerPlayerController>();
                if (player != null && danceController != null)
                {
                    player.BeginMenuDance(danceController);
                }
            }
            return;
        }

        if (danceController != null)
        {
            menuShowcaseAnimator.runtimeAnimatorController = danceController;
        }

        menuShowcaseAnimator.Rebind();
        menuShowcaseAnimator.Update(0f);
        menuShowcaseAnimator.speed = 1f;
        menuShowcaseAnimator.Play(0, 0, 0f);
    }
}
