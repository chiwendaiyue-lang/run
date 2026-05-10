using UnityEngine;

public class RunnerHud : MonoBehaviour
{
    public KeyCode restartKey = KeyCode.R;

    private GUIStyle m_LabelStyle;
    private GUIStyle m_GameOverStyle;

    private void EnsureStyles()
    {
        if (m_LabelStyle != null && m_GameOverStyle != null)
        {
            return;
        }

        m_LabelStyle = new GUIStyle(GUI.skin.label);
        m_LabelStyle.fontSize = 24;
        m_LabelStyle.normal.textColor = Color.white;

        m_GameOverStyle = new GUIStyle(GUI.skin.label);
        m_GameOverStyle.fontSize = 36;
        m_GameOverStyle.alignment = TextAnchor.MiddleCenter;
        m_GameOverStyle.normal.textColor = Color.yellow;
    }

    private void Update()
    {
        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        if (RunnerGameManager.Instance.IsGameOver && Input.GetKeyDown(restartKey))
        {
            RunnerGameManager.Instance.RestartScene();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        RunnerGameManager gm = RunnerGameManager.Instance;
        if (gm.Phase != RunnerGameManager.GameFlowPhase.Playing)
        {
            return;
        }

        GUI.Label(new Rect(16, 12, 400, 30), "Distance: " + gm.Distance.ToString("0"), m_LabelStyle);
        GUI.Label(new Rect(16, 40, 400, 30), "Coins: " + gm.Coins, m_LabelStyle);
        GUI.Label(new Rect(16, 68, 400, 30), "Lives: " + gm.Lives, m_LabelStyle);
        GUI.Label(new Rect(16, 96, 400, 30), "Score: " + gm.Score, m_LabelStyle);

    }
}
