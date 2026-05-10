using UnityEditor;
using UnityEngine;

public static class RunnerSceneCleanup
{
    [MenuItem("Tools/Runner/Cleanup Extra Moving Players")]
    public static void CleanupExtraMovingPlayers()
    {
        RunnerPlayerController runner = Object.FindObjectOfType<RunnerPlayerController>();
        if (runner == null)
        {
            Debug.LogWarning("RunnerPlayerController not found in scene.");
            return;
        }

        Transform runnerRoot = runner.transform;
        int disabledCount = 0;

        Animator[] animators = Object.FindObjectsOfType<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            Transform t = animator.transform;
            bool underRunner = t == runnerRoot || t.IsChildOf(runnerRoot);
            if (underRunner)
            {
                continue;
            }

            string n = t.name.ToLowerInvariant();
            bool looksLikeCharacter =
                n.Contains("cat") ||
                n.Contains("racoon") ||
                n.Contains("raccoon") ||
                n.Contains("skelmesh_bodyguard") ||
                n.Contains("bodyguard") ||
                n.Contains("player");

            if (!looksLikeCharacter)
            {
                continue;
            }

            if (t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Disable Extra Moving Player");
                t.gameObject.SetActive(false);
                disabledCount++;
            }
        }

        Debug.Log("Cleanup done. Disabled extra moving players: " + disabledCount);
    }
}
