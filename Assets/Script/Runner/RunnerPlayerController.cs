using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RunnerPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float forwardSpeed = 8f;
    public float maxForwardSpeed = 18f;
    public float acceleration = 0.8f;
    public float laneOffset = 2.5f;
    public float laneChangeSpeed = 10f;

    [Header("Jump/Slide")]
    public float gravity = -30f;
    public float jumpHeight = 2.2f;
    public float slideDuration = 0.45f;
    public float slideHeight = 1.0f;
    [Tooltip("两次滑铲之间的最小间隔，避免连续触发。")]
    public float minSlideInterval = 0.35f;
    [Tooltip("滑铲时前进速度倍率。")]
    public float slideForwardSpeedMultiplier = 1.25f;

    [Header("Collect")]
    public float collectRadius = 1.2f;
    public LayerMask collectibleMask = ~0;

    [Header("Damage")]
    public float hitInvincibilityDuration = 1.0f;
    [Tooltip("Hit 动画加速倍率（>1 更快）。")]
    public float hitAnimationSpeedMultiplier = 1.6f;
    [Tooltip("受击期间最多锁定时长，防止动画状态异常时卡住。")]
    public float hitMaxLockDuration = 0.8f;

    [Header("Flow / 动画 (可选)")]
    [Tooltip("胜利时从跑步停下，可指定 run to stop.controller。")]
    public RuntimeAnimatorController runToStopController;

    [Header("Optional")]
    public Animator animator;

    private CharacterController m_Controller;
    private int m_CurrentLane;
    private float m_VerticalVelocity;
    private bool m_Sliding;
    private float m_SlideTimer;
    private float m_NextSlideAllowedTime;
    private float m_DefaultHeight;
    private Vector3 m_DefaultCenter;
    private float m_TravelDistance;
    private float m_HitInvincibleTimer;
    private readonly Collider[] m_CollectHits = new Collider[16];
    private bool m_VictorySequenceRunning;
    private bool m_HitSequenceRunning;
    private RuntimeAnimatorController m_CachedGameplayController;
    private bool m_CachedControllerStored;
    private RuntimeAnimatorController m_LastKnownAnimatorController;
    private bool m_IsInMenuDance;

    private static readonly int s_SpeedHash = Animator.StringToHash("Speed");
    private static readonly int s_GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int s_JumpHash = Animator.StringToHash("Jump");
    private static readonly int s_SlideHash = Animator.StringToHash("Slide");
    private static readonly int s_RollHash = Animator.StringToHash("Roll");
    private static readonly int s_HitHash = Animator.StringToHash("Hit");
    private static readonly int s_DieCarHash = Animator.StringToHash("DieCar");
    private static readonly int s_DieLowHash = Animator.StringToHash("DieLow");
    private HashSet<int> m_AvailableAnimatorParams;

    public float CurrentSpeed
    {
        get { return forwardSpeed; }
    }

    private void Awake()
    {
        m_Controller = GetComponent<CharacterController>();
        m_DefaultHeight = m_Controller.height;
        m_DefaultCenter = m_Controller.center;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        CacheAnimatorParameters();
    }

    private void Update()
    {
        if (m_HitInvincibleTimer > 0f)
        {
            m_HitInvincibleTimer -= Time.deltaTime;
        }

        if (RunnerGameManager.Instance == null)
        {
            return;
        }

        EnsureAnimatorReady();

        if (m_VictorySequenceRunning)
        {
            return;
        }

        if (m_HitSequenceRunning)
        {
            return;
        }

        if (!RunnerGameManager.Instance.CanPlayerMove())
        {
            if (RunnerGameManager.Instance.Phase == RunnerGameManager.GameFlowPhase.Countdown)
            {
                UpdateAnimator();
            }

            return;
        }

        HandleLaneInput();
        HandleJumpAndSlideInput();
        Move();
        CollectNearbyCoins();
        UpdateAnimator();

    }

    public void StartVictorySequence()
    {
        if (m_VictorySequenceRunning)
        {
            return;
        }

        if (runToStopController == null || animator == null)
        {
            NotifyVictoryToFlow();
            return;
        }

        StartCoroutine(CoVictoryRunToStop());
    }

    private IEnumerator CoVictoryRunToStop()
    {
        m_VictorySequenceRunning = true;
        if (!m_CachedControllerStored && animator.runtimeAnimatorController != null)
        {
            m_CachedGameplayController = animator.runtimeAnimatorController;
            m_CachedControllerStored = true;
        }

        animator.runtimeAnimatorController = runToStopController;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
        CacheAnimatorParameters();

        const int layer = 0;
        float t = 0f;
        const float maxWait = 8f;
        while (t < maxWait)
        {
            if (animator == null)
            {
                break;
            }

            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(layer);
            if (st.length > 0.01f && st.normalizedTime >= 0.99f)
            {
                break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        m_VictorySequenceRunning = false;
        NotifyVictoryToFlow();
    }

    public void BeginMenuDance(RuntimeAnimatorController danceController)
    {
        if (animator == null || danceController == null)
        {
            return;
        }

        if (!m_CachedControllerStored && animator.runtimeAnimatorController != null)
        {
            m_CachedGameplayController = animator.runtimeAnimatorController;
            m_CachedControllerStored = true;
        }

        m_IsInMenuDance = true;
        animator.runtimeAnimatorController = danceController;
        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
        animator.Play(0, 0, 0f);
    }

    public void EndMenuDance()
    {
        if (!m_IsInMenuDance || animator == null)
        {
            return;
        }

        if (m_CachedGameplayController != null)
        {
            animator.runtimeAnimatorController = m_CachedGameplayController;
        }

        animator.Rebind();
        animator.Update(0f);
        animator.speed = 1f;
        m_IsInMenuDance = false;
        CacheAnimatorParameters();
    }

    /// <summary>
    /// 射线向下贴合地面（忽略 Trigger）。用于生成赛道后立刻对齐，避免构建版首帧无碰撞时掉落。
    /// </summary>
    public void SnapToGroundBelow(float maxDistance = 120f)
    {
        if (m_Controller == null)
        {
            m_Controller = GetComponent<CharacterController>();
        }

        if (m_Controller == null)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 8f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            float bottomOffset = m_Controller.center.y - m_Controller.height * 0.5f;
            float targetY = hit.point.y - bottomOffset + m_Controller.skinWidth;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, targetY, p.z);
            m_VerticalVelocity = -2f;
        }
    }

    public void HardResetCharacterController()
    {
        if (m_Controller == null)
        {
            m_Controller = GetComponent<CharacterController>();
        }

        if (m_Controller == null)
        {
            return;
        }

        bool wasEnabled = m_Controller.enabled;
        m_Controller.enabled = false;
        m_Controller.enabled = wasEnabled;
    }

    private void NotifyVictoryToFlow()
    {
        if (RunnerGameManager.Instance != null)
        {
            RunnerGameManager.Instance.OnVictorySequenceFinished();
        }

        if (RunnerFlowUI.Instance != null)
        {
            RunnerFlowUI.Instance.ShowVictoryPanel();
        }
    }

    private void HandleLaneInput()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            m_CurrentLane = Mathf.Max(-1, m_CurrentLane - 1);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            m_CurrentLane = Mathf.Min(1, m_CurrentLane + 1);
        }
    }

    private void HandleJumpAndSlideInput()
    {
        bool grounded = m_Controller.isGrounded;

        if (grounded && m_VerticalVelocity < 0f)
        {
            m_VerticalVelocity = -2f;
        }

        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) && grounded && !m_Sliding)
        {
            m_VerticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            SetAnimatorTriggerIfExists(s_JumpHash);
        }

        if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) &&
            grounded &&
            !m_Sliding &&
            Time.time >= m_NextSlideAllowedTime)
        {
            StartSlide();
        }

        if (m_Sliding)
        {
            m_SlideTimer -= Time.deltaTime;
            if (m_SlideTimer <= 0f)
            {
                StopSlide();
            }
        }
    }

    private void Move()
    {
        forwardSpeed = Mathf.Min(maxForwardSpeed, forwardSpeed + acceleration * Time.deltaTime);

        Vector3 position = transform.position;
        float targetX = m_CurrentLane * laneOffset;
        float deltaX = Mathf.Lerp(position.x, targetX, laneChangeSpeed * Time.deltaTime) - position.x;

        m_VerticalVelocity += gravity * Time.deltaTime;
        float forwardMultiplier = m_Sliding ? slideForwardSpeedMultiplier : 1f;
        Vector3 move = new Vector3(deltaX, m_VerticalVelocity * Time.deltaTime, forwardSpeed * forwardMultiplier * Time.deltaTime);

        m_Controller.Move(move);
        m_TravelDistance += move.z;

        if (RunnerGameManager.Instance != null)
        {
            RunnerGameManager.Instance.ReportTravel(Mathf.Max(0f, move.z));
        }
    }

    private void CollectNearbyCoins()
    {
        Vector3 center = transform.position + Vector3.up * 1.0f;
        int count = Physics.OverlapSphereNonAlloc(center, collectRadius, m_CollectHits, collectibleMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = m_CollectHits[i];
            if (col == null)
            {
                continue;
            }

            RunnerCollectible collectible = col.GetComponentInParent<RunnerCollectible>();
            if (collectible != null)
            {
                collectible.Collect();
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (RunnerGameManager.Instance == null || !RunnerGameManager.Instance.IsPlaying)
        {
            return;
        }

        RunnerObstacle obstacle = hit.collider.GetComponentInParent<RunnerObstacle>();
        if (obstacle == null || !obstacle.lethal)
        {
            return;
        }

        if (m_HitInvincibleTimer > 0f)
        {
            return;
        }

        bool lostLastLife = RunnerGameManager.Instance.LoseLife();
        if (lostLastLife)
        {
            if (obstacle.deathAnim == RunnerObstacle.DeathAnimKind.NoJumpDeath)
            {
                SetAnimatorTriggerIfExists(s_DieLowHash);
            }
            else
            {
                SetAnimatorTriggerIfExists(s_DieCarHash);
            }

            StartCoroutine(CoAfterDeathOnceThenUi());
        }
        else
        {
            m_HitInvincibleTimer = hitInvincibilityDuration;
            transform.position += Vector3.up * 0.2f;
            DisableObstacleColliders(obstacle);
            StartCoroutine(CoPlayHitThenResume());
        }
    }

    private IEnumerator CoPlayHitThenResume()
    {
        if (m_HitSequenceRunning)
        {
            yield break;
        }

        m_HitSequenceRunning = true;
        SetAnimatorTriggerIfExists(s_HitHash);

        float oldAnimatorSpeed = 1f;
        if (animator != null)
        {
            oldAnimatorSpeed = animator.speed;
            animator.speed = Mathf.Max(1f, hitAnimationSpeedMultiplier);
        }

        const int layer = 0;
        float t = 0f;
        bool enteredHitState = false;
        float maxWait = Mathf.Max(0.15f, hitMaxLockDuration);

        while (t < maxWait)
        {
            if (animator == null)
            {
                break;
            }

            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(layer);
            if (st.IsName("Hit"))
            {
                enteredHitState = true;
                if (st.normalizedTime >= 0.98f)
                {
                    break;
                }
            }
            else if (!enteredHitState && t >= 0.2f)
            {
                // 兼容旧控制器无 Hit 状态，给一个很短的受击停顿后继续。
                break;
            }
            else if (enteredHitState)
            {
                // 已经进入过 Hit，离开后立即恢复移动。
                break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (animator != null)
        {
            animator.speed = oldAnimatorSpeed;
        }

        m_HitSequenceRunning = false;
    }

    private IEnumerator CoAfterDeathOnceThenUi()
    {
        const int layer = 0;
        float t = 0f;
        float maxWait = 3f;
        bool enteredDeathState = false;
        while (t < maxWait)
        {
            if (animator == null)
            {
                break;
            }

            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(layer);
            if (st.IsName("DeathCar") || st.IsName("DeathLow"))
            {
                enteredDeathState = true;
                if (st.length > 0.01f && st.normalizedTime >= 0.98f)
                {
                    animator.speed = 0f;
                    break;
                }
            }
            else if (!enteredDeathState && t >= 1.25f)
            {
                // 兼容旧控制器（没有 DeathCar/DeathLow）时，避免失败面板长时间延迟。
                break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (animator != null)
        {
            animator.speed = 0f;
        }

        if (RunnerGameManager.Instance != null)
        {
            RunnerGameManager.Instance.OnDefeatSequenceFinished();
        }

        if (RunnerFlowUI.Instance != null)
        {
            RunnerFlowUI.Instance.ShowDefeatPanel();
        }
    }

    private static void DisableObstacleColliders(RunnerObstacle obstacle)
    {
        Collider[] cols = obstacle.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = false;
        }
    }

    private void StartSlide()
    {
        m_Sliding = true;
        m_SlideTimer = slideDuration;
        m_NextSlideAllowedTime = Time.time + minSlideInterval;
        m_Controller.height = slideHeight;
        m_Controller.center = new Vector3(m_DefaultCenter.x, slideHeight * 0.5f, m_DefaultCenter.z);
        if (m_AvailableAnimatorParams != null && m_AvailableAnimatorParams.Contains(s_RollHash))
        {
            if (animator != null)
            {
                animator.ResetTrigger("Slide");
            }
            SetAnimatorTriggerIfExists(s_RollHash);
        }
        else
        {
            SetAnimatorTriggerIfExists(s_SlideHash);
        }
    }

    private void StopSlide()
    {
        m_Sliding = false;
        m_Controller.height = m_DefaultHeight;
        m_Controller.center = m_DefaultCenter;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorFloatIfExists(s_SpeedHash, forwardSpeed);
        SetAnimatorBoolIfExists(s_GroundedHash, m_Controller.isGrounded);
    }

    private void CacheAnimatorParameters()
    {
        m_AvailableAnimatorParams = new HashSet<int>();
        if (animator == null)
        {
            m_LastKnownAnimatorController = null;
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            m_AvailableAnimatorParams.Add(parameters[i].nameHash);
        }
        m_LastKnownAnimatorController = animator.runtimeAnimatorController;
    }

    private void EnsureAnimatorReady()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }
        }

        if (m_AvailableAnimatorParams == null ||
            m_AvailableAnimatorParams.Count == 0 ||
            m_LastKnownAnimatorController != animator.runtimeAnimatorController)
        {
            CacheAnimatorParameters();
        }
    }

    private void SetAnimatorFloatIfExists(int hash, float value)
    {
        if (animator != null && m_AvailableAnimatorParams.Contains(hash))
        {
            animator.SetFloat(hash, value);
        }
    }

    private void SetAnimatorBoolIfExists(int hash, bool value)
    {
        if (animator != null && m_AvailableAnimatorParams.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetAnimatorTriggerIfExists(int hash)
    {
        if (animator != null && m_AvailableAnimatorParams.Contains(hash))
        {
            animator.SetTrigger(hash);
        }
    }
}
