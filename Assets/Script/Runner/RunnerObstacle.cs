using UnityEngine;

public class RunnerObstacle : MonoBehaviour
{
    public enum DeathAnimKind
    {
        [Tooltip("垃圾桶、高横杆、狗等需跳/躲的高障碍，播 car death。")]
        CarDeath = 0,
        [Tooltip("需滑铲过的低横杆，播 no jump death。")]
        NoJumpDeath = 1
    }

    [Tooltip("撞到该障碍时是否扣命。")]
    public bool lethal = true;

    [Tooltip("死亡动画类型。选 Auto 时按资源名在 Awake 中推断。")]
    public DeathAnimKind deathAnim = DeathAnimKind.CarDeath;

    [Tooltip("为 true 时根据对象名含 lowbarrier、low 等自动设为低杆死亡。")]
    public bool autoClassifyByName = true;

    private void Awake()
    {
        if (!autoClassifyByName)
        {
            return;
        }

        string n = name.ToLowerInvariant();
        if (n.Contains("lowbarrier") || (n.Contains("low") && n.Contains("barrier")))
        {
            deathAnim = DeathAnimKind.NoJumpDeath;
        }
        else if (n.Contains("highbarrier") || n.Contains("bin") || n.Contains("wheely") || n.Contains("dog") || n.Contains("road"))
        {
            deathAnim = DeathAnimKind.CarDeath;
        }
    }
}
