using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RunnerAnimatorSetup
{
    private const string k_ControllerPath = "Assets/Animation/controller/Player.controller";

    private const string k_RunClipPath = "Assets/Animation/animation/fast run.anim";
    private const string k_IdleClipPath = "Assets/Animation/animation/idle.anim";
    private const string k_JumpClipPath = "Assets/Animation/animation/running jump.anim";
    private const string k_RollClipPath = "Assets/Animation/animation/running roll.anim";
    private const string k_HitClipPath = "Assets/Animation/animation/hit.anim";
    private const string k_DeathCarPath = "Assets/Animation/animation/car death.anim";
    private const string k_DeathLowPath = "Assets/Animation/animation/no jump death.anim";

    [MenuItem("Tools/Runner/Create Or Update Player.controller")]
    public static void CreateOrUpdatePlayerController()
    {
        AnimatorController controller = RecreateControllerAsset();
        if (controller == null)
        {
            Debug.LogError("Failed to create/load Player.controller.");
            return;
        }

        AnimationClip runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_RunClipPath);
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_IdleClipPath);
        AnimationClip jumpClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_JumpClipPath);
        AnimationClip rollClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_RollClipPath);
        AnimationClip hitClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_HitClipPath);
        AnimationClip deathCarClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_DeathCarPath);
        AnimationClip deathLowClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_DeathLowPath);
        if (deathLowClip == null)
        {
            deathLowClip = deathCarClip;
        }
        if (hitClip == null)
        {
            Debug.LogWarning("Missing hit.anim; Hit 受击动作将跳过。请将 hit.anim 放在 Assets/Animation/animation/。");
        }

        if (runClip == null || idleClip == null || jumpClip == null || rollClip == null || deathCarClip == null)
        {
            Debug.LogError("Missing required animation clips. Check Assets/Animation/animation/.");
            return;
        }

        if (deathLowClip == null)
        {
            deathLowClip = deathCarClip;
        }

        SyncParameters(controller);
        BuildStateMachine(controller, runClip, idleClip, jumpClip, rollClip, hitClip, deathCarClip, deathLowClip);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = controller;
        Debug.Log("Player.controller created/updated successfully.");
    }

    [MenuItem("Tools/Runner/Create+Assign Player.controller")]
    public static void CreateAndAssignPlayerController()
    {
        CreateOrUpdatePlayerController();

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_ControllerPath);
        if (controller == null)
        {
            return;
        }

        RunnerPlayerController runner = Object.FindObjectOfType<RunnerPlayerController>();
        if (runner == null)
        {
            Debug.LogWarning("RunnerPlayerController not found in scene. Controller asset was still created.");
            return;
        }

        Animator animator = runner.animator;
        if (animator == null)
        {
            animator = runner.GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("Animator not found on RunnerPlayer or children. Please add Animator first.");
            return;
        }

        Undo.RecordObject(animator, "Assign Player Controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Undo.RecordObject(runner, "Assign Runner Animator");
        runner.animator = animator;

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(runner);
        Debug.Log("Player.controller assigned to RunnerPlayer.");
    }

    private static AnimatorController RecreateControllerAsset()
    {
        string dir = System.IO.Path.GetDirectoryName(k_ControllerPath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            EnsureFolders(dir);
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(k_ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(k_ControllerPath);
            AssetDatabase.Refresh();
        }

        return AnimatorController.CreateAnimatorControllerAtPath(k_ControllerPath);
    }

    private static void EnsureFolders(string assetFolder)
    {
        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static void SyncParameters(AnimatorController controller)
    {
        Dictionary<string, AnimatorControllerParameterType> wanted = new Dictionary<string, AnimatorControllerParameterType>
        {
            { "Speed", AnimatorControllerParameterType.Float },
            { "Grounded", AnimatorControllerParameterType.Bool },
            { "Jump", AnimatorControllerParameterType.Trigger },
            { "Roll", AnimatorControllerParameterType.Trigger },
            { "Slide", AnimatorControllerParameterType.Trigger },
            { "Hit", AnimatorControllerParameterType.Trigger },
            { "DieCar", AnimatorControllerParameterType.Trigger },
            { "DieLow", AnimatorControllerParameterType.Trigger }
        };

        List<AnimatorControllerParameter> toRemove = new List<AnimatorControllerParameter>();
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (!wanted.ContainsKey(p.name))
            {
                toRemove.Add(p);
            }
            else if (p.type != wanted[p.name])
            {
                toRemove.Add(p);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            controller.RemoveParameter(toRemove[i]);
        }

        foreach (KeyValuePair<string, AnimatorControllerParameterType> kv in wanted)
        {
            if (!HasParameter(controller, kv.Key, kv.Value))
            {
                controller.AddParameter(kv.Key, kv.Value);
            }
        }
    }

    private static bool HasParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == name && p.type == type)
            {
                return true;
            }
        }
        return false;
    }

    private static void BuildStateMachine(
        AnimatorController controller,
        AnimationClip runClip,
        AnimationClip idleClip,
        AnimationClip jumpClip,
        AnimationClip rollClip,
        AnimationClip hitClip,
        AnimationClip deathCarClip,
        AnimationClip deathLowClip)
    {
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState run = sm.AddState("Run", new Vector3(300, 160, 0));
        AnimatorState idle = sm.AddState("Idle", new Vector3(60, 160, 0));
        AnimatorState jump = sm.AddState("Jump", new Vector3(300, 40, 0));
        AnimatorState roll = sm.AddState("Roll", new Vector3(300, 280, 0));
        AnimatorState hit = sm.AddState("Hit", new Vector3(300, 320, 0));
        AnimatorState deathCar = sm.AddState("DeathCar", new Vector3(520, 80, 0));
        AnimatorState deathLow = sm.AddState("DeathLow", new Vector3(520, 240, 0));

        run.motion = runClip;
        idle.motion = idleClip;
        jump.motion = jumpClip;
        roll.motion = rollClip;
        if (hitClip != null)
        {
            hit.motion = hitClip;
        }
        else
        {
            hit.motion = runClip;
        }
        deathCar.motion = deathCarClip;
        deathLow.motion = deathLowClip;

        sm.defaultState = run;

        AnimatorStateTransition idleToRun = idle.AddTransition(run);
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.1f;
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition runToIdle = run.AddTransition(idle);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.1f;
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AnimatorStateTransition runToJump = run.AddTransition(jump);
        runToJump.hasExitTime = false;
        runToJump.duration = 0.05f;
        runToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        AnimatorStateTransition jumpToRun = jump.AddTransition(run);
        jumpToRun.hasExitTime = true;
        jumpToRun.exitTime = 0.9f;
        jumpToRun.duration = 0.05f;

        AnimatorStateTransition runToRoll = run.AddTransition(roll);
        runToRoll.hasExitTime = false;
        runToRoll.duration = 0.05f;
        runToRoll.AddCondition(AnimatorConditionMode.If, 0f, "Roll");

        AnimatorStateTransition runToSlide = run.AddTransition(roll);
        runToSlide.hasExitTime = false;
        runToSlide.duration = 0.05f;
        runToSlide.AddCondition(AnimatorConditionMode.If, 0f, "Slide");

        AnimatorStateTransition rollToRun = roll.AddTransition(run);
        rollToRun.hasExitTime = true;
        rollToRun.exitTime = 0.95f;
        rollToRun.duration = 0.05f;

        AnimatorStateTransition anyToHit = sm.AddAnyStateTransition(hit);
        anyToHit.hasExitTime = false;
        anyToHit.duration = 0.05f;
        anyToHit.canTransitionToSelf = false;
        anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

        AnimatorStateTransition hitToRunAny = hit.AddTransition(run);
        hitToRunAny.hasExitTime = true;
        hitToRunAny.exitTime = 0.92f;
        hitToRunAny.duration = 0.05f;

        AnimatorStateTransition anyToDeathCar = sm.AddAnyStateTransition(deathCar);
        anyToDeathCar.hasExitTime = false;
        anyToDeathCar.duration = 0.03f;
        anyToDeathCar.canTransitionToSelf = false;
        anyToDeathCar.AddCondition(AnimatorConditionMode.If, 0f, "DieCar");

        AnimatorStateTransition anyToDeathLow = sm.AddAnyStateTransition(deathLow);
        anyToDeathLow.hasExitTime = false;
        anyToDeathLow.duration = 0.03f;
        anyToDeathLow.canTransitionToSelf = false;
        anyToDeathLow.AddCondition(AnimatorConditionMode.If, 0f, "DieLow");
    }
}
