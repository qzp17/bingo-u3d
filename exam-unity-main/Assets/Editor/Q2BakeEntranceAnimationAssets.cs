#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 将 <see cref="Q2EntranceAnimationMath"/> 采样为 AnimationClip，并生成 AnimatorController；同时把 PlayButton 绑上 Animator。
/// 在 Unity 菜单执行一次即可（或 batchmode 调用 <see cref="BakeFromCommandLine"/>）。
/// </summary>
public static class Q2BakeEntranceAnimationAssets
{
    public const string MainClipPath = "Assets/Q2/ButtonEntrance.anim";
    public const string IdleClipPath = "Assets/Q2/ButtonEntranceIdle.anim";
    public const string PressedClipPath = "Assets/Q2/ButtonPressed.anim";
    public const string ControllerPath = "Assets/Q2/PlayButtonEntrance.controller";
    public const string Q2ScenePath = "Assets/Q2/Q2.unity";

    private const float SampleFps = 60f;

    [MenuItem("Tools/Q2/Bake Entrance Animation Assets")]
    public static void BakeFromMenu()
    {
        BakeInternal();
    }

    /// <summary>供 Unity 命令行：<c>-executeMethod Q2BakeEntranceAnimationAssets.BakeFromCommandLine</c></summary>
    public static void BakeFromCommandLine()
    {
        BakeInternal();
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    private static void BakeInternal()
    {
        BakeMainEntranceClip();
        BakeIdleClip();
        BakePressedClip();
        BuildOrReplaceAnimatorController();
        EnsurePlayButtonAnimator();
        AssetDatabase.SaveAssets();
        Debug.Log(
            "Q2: 已生成出场 / 呼吸 Idle / 按下 Pressed 剪辑与 AnimatorController，并已尝试挂到 PlayButton。");
    }

    private static void BakeMainEntranceClip()
    {
        float duration = Q2EntranceAnimationMath.Duration;
        int frameCount = Mathf.Max(3, Mathf.CeilToInt(duration * SampleFps) + 1);

        var ksx = new Keyframe[frameCount];
        var ksy = new Keyframe[frameCount];
        var ksz = new Keyframe[frameCount];
        var kqx = new Keyframe[frameCount];
        var kqy = new Keyframe[frameCount];
        var kqz = new Keyframe[frameCount];
        var kqw = new Keyframe[frameCount];
        var kcr = new Keyframe[frameCount];
        var kcg = new Keyframe[frameCount];
        var kcb = new Keyframe[frameCount];
        var kca = new Keyframe[frameCount];

        const float imgR = 1f;
        const float imgG = 1f;
        const float imgB = 1f;
        const float imgA = 1f;

        for (int i = 0; i < frameCount; i++)
        {
            float u = frameCount == 1 ? 0f : i / (float)(frameCount - 1);
            float elapsed = duration * u;
            Q2EntranceAnimationMath.Evaluate(elapsed, out Vector3 scale, out Quaternion q);

            ksx[i] = new Keyframe(elapsed, scale.x);
            ksy[i] = new Keyframe(elapsed, scale.y);
            ksz[i] = new Keyframe(elapsed, scale.z);
            kqx[i] = new Keyframe(elapsed, q.x);
            kqy[i] = new Keyframe(elapsed, q.y);
            kqz[i] = new Keyframe(elapsed, q.z);
            kqw[i] = new Keyframe(elapsed, q.w);
            kcr[i] = new Keyframe(elapsed, imgR);
            kcg[i] = new Keyframe(elapsed, imgG);
            kcb[i] = new Keyframe(elapsed, imgB);
            kca[i] = new Keyframe(elapsed, imgA);
        }

        var clip = new AnimationClip { name = "ButtonEntrance", frameRate = SampleFps };

        void BindScale(string axis, AnimationCurve c)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), axis),
                c);
        }

        void BindRot(string axis, AnimationCurve c)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), axis),
                c);
        }

        BindScale("m_LocalScale.x", new AnimationCurve(ksx));
        BindScale("m_LocalScale.y", new AnimationCurve(ksy));
        BindScale("m_LocalScale.z", new AnimationCurve(ksz));
        BindRot("m_LocalRotation.x", new AnimationCurve(kqx));
        BindRot("m_LocalRotation.y", new AnimationCurve(kqy));
        BindRot("m_LocalRotation.z", new AnimationCurve(kqz));
        BindRot("m_LocalRotation.w", new AnimationCurve(kqw));
        BindImageColor(new AnimationCurve(kcr), new AnimationCurve(kcg), new AnimationCurve(kcb), new AnimationCurve(kca), clip);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        SaveClipAsset(MainClipPath, clip);
    }

    private static void BakeIdleClip()
    {
        float cycle = Q2EntranceAnimationMath.IdleBreathCycleDuration;
        float ampX = Q2EntranceAnimationMath.IdleBreathScaleAmplitudeX;
        float ampY = ampX * Q2EntranceAnimationMath.IdleBreathVerticalEmphasis;

        int n = Mathf.Max(4, Mathf.CeilToInt(cycle * SampleFps));
        var ksx = new Keyframe[n + 1];
        var ksy = new Keyframe[n + 1];
        var ksz = new Keyframe[n + 1];
        var kqx = new Keyframe[n + 1];
        var kqy = new Keyframe[n + 1];
        var kqz = new Keyframe[n + 1];
        var kqw = new Keyframe[n + 1];
        var kcr = new Keyframe[n + 1];
        var kcg = new Keyframe[n + 1];
        var kcb = new Keyframe[n + 1];
        var kca = new Keyframe[n + 1];

        var id = Quaternion.identity;

        for (int i = 0; i <= n; i++)
        {
            float t = (i / (float)n) * cycle;
            // 2π 整周期：t=0 与 t=cycle 缩放相同，配合 loopTime 无跳变。
            float phase = (2f * Mathf.PI * t) / cycle;
            float wobble = Mathf.Sin(phase);

            float sx = 1f + ampX * wobble;
            float sy = 1f + ampY * wobble;
            float sz = 1f;

            ksx[i] = new Keyframe(t, sx);
            ksy[i] = new Keyframe(t, sy);
            ksz[i] = new Keyframe(t, sz);
            kqx[i] = new Keyframe(t, id.x);
            kqy[i] = new Keyframe(t, id.y);
            kqz[i] = new Keyframe(t, id.z);
            kqw[i] = new Keyframe(t, id.w);
            // 待机全程保持纯白，便于从按下态回到 Idle 时颜色被剪辑正确写回。
            kcr[i] = new Keyframe(t, 1f);
            kcg[i] = new Keyframe(t, 1f);
            kcb[i] = new Keyframe(t, 1f);
            kca[i] = new Keyframe(t, 1f);
        }

        var clip = new AnimationClip { name = "ButtonEntranceIdle", frameRate = SampleFps };

        void BindCurve(string prop, AnimationCurve c)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), prop),
                c);
        }

        BindCurve("m_LocalScale.x", new AnimationCurve(ksx));
        BindCurve("m_LocalScale.y", new AnimationCurve(ksy));
        BindCurve("m_LocalScale.z", new AnimationCurve(ksz));
        BindCurve("m_LocalRotation.x", new AnimationCurve(kqx));
        BindCurve("m_LocalRotation.y", new AnimationCurve(kqy));
        BindCurve("m_LocalRotation.z", new AnimationCurve(kqz));
        BindCurve("m_LocalRotation.w", new AnimationCurve(kqw));
        BindImageColor(new AnimationCurve(kcr), new AnimationCurve(kcg), new AnimationCurve(kcb), new AnimationCurve(kca), clip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        SaveClipAsset(IdleClipPath, clip);
    }

    /// <summary>
    /// 按下：先 Ease-out-back 压入略过冲再稳定在目标缩小 + 「哑黑」；<c>loopTime=false</c> 最后一帧保持，便于按住态静止。
    /// </summary>
    private static void BakePressedClip()
    {
        const float popDuration = 0.11f;
        float m = Q2EntranceAnimationMath.TouchPressedScaleMultiplierXY;
        float cf = Q2EntranceAnimationMath.TouchPressedColorRgbFactor;
        float back = Q2EntranceAnimationMath.BackOvershoot;

        int frameCount = Mathf.Max(4, Mathf.CeilToInt(popDuration * SampleFps) + 1);
        var ksx = new Keyframe[frameCount];
        var ksy = new Keyframe[frameCount];
        var ksz = new Keyframe[frameCount];
        var kqx = new Keyframe[frameCount];
        var kqy = new Keyframe[frameCount];
        var kqz = new Keyframe[frameCount];
        var kqw = new Keyframe[frameCount];
        var kcr = new Keyframe[frameCount];
        var kcg = new Keyframe[frameCount];
        var kcb = new Keyframe[frameCount];
        var kca = new Keyframe[frameCount];

        var id = Quaternion.identity;
        const float imgR = 1f;
        const float imgG = 1f;
        const float imgB = 1f;
        const float imgA = 1f;

        for (int i = 0; i < frameCount; i++)
        {
            float u = frameCount == 1 ? 0f : i / (float)(frameCount - 1);
            float t = popDuration * u;
            float k = EaseOutBack01(u, back);
            float s = Mathf.LerpUnclamped(1f, m, k);
            float ck = Mathf.Clamp01(k);
            float r = Mathf.LerpUnclamped(imgR, imgR * cf, ck);
            float g = Mathf.LerpUnclamped(imgG, imgG * cf, ck);
            float b = Mathf.LerpUnclamped(imgB, imgB * cf, ck);

            ksx[i] = new Keyframe(t, s);
            ksy[i] = new Keyframe(t, s);
            ksz[i] = new Keyframe(t, 1f);
            kqx[i] = new Keyframe(t, id.x);
            kqy[i] = new Keyframe(t, id.y);
            kqz[i] = new Keyframe(t, id.z);
            kqw[i] = new Keyframe(t, id.w);
            kcr[i] = new Keyframe(t, r);
            kcg[i] = new Keyframe(t, g);
            kcb[i] = new Keyframe(t, b);
            kca[i] = new Keyframe(t, imgA);
        }

        var clip = new AnimationClip { name = "ButtonPressed", frameRate = SampleFps };

        void BindRf(string prop, AnimationCurve c)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), prop),
                c);
        }

        BindRf("m_LocalScale.x", new AnimationCurve(ksx));
        BindRf("m_LocalScale.y", new AnimationCurve(ksy));
        BindRf("m_LocalScale.z", new AnimationCurve(ksz));
        BindRf("m_LocalRotation.x", new AnimationCurve(kqx));
        BindRf("m_LocalRotation.y", new AnimationCurve(kqy));
        BindRf("m_LocalRotation.z", new AnimationCurve(kqz));
        BindRf("m_LocalRotation.w", new AnimationCurve(kqw));
        BindImageColor(new AnimationCurve(kcr), new AnimationCurve(kcg), new AnimationCurve(kcb), new AnimationCurve(kca), clip);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        SaveClipAsset(PressedClipPath, clip);
    }

    private static float EaseOutBack01(float tNorm, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(tNorm - 1f, 3f) + c1 * Mathf.Pow(tNorm - 1f, 2f);
    }

    private static void BindImageColor(
        AnimationCurve r,
        AnimationCurve g,
        AnimationCurve b,
        AnimationCurve a,
        AnimationClip clip)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Color.r"),
            r);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Color.g"),
            g);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Color.b"),
            b);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Color.a"),
            a);
    }

    private static void SaveClipAsset(string path, AnimationClip clip)
    {
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(clip, path);
    }

    private static void BuildOrReplaceAnimatorController()
    {
        var entrance = AssetDatabase.LoadAssetAtPath<AnimationClip>(MainClipPath);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        var pressed = AssetDatabase.LoadAssetAtPath<AnimationClip>(PressedClipPath);
        if (entrance == null || idle == null || pressed == null)
        {
            Debug.LogError("Q2 Bake: Entrance / Idle / Pressed 任一剪辑缺失，无法创建 AnimatorController。");
            return;
        }

        if (File.Exists(ControllerPath))
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Pressed", AnimatorControllerParameterType.Bool);

        var rootStateMachine = controller.layers[0].stateMachine;

        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idle;
        idleState.writeDefaultValues = false;

        var entranceState = rootStateMachine.AddState("Entrance");
        entranceState.motion = entrance;
        entranceState.writeDefaultValues = false;

        var pressedState = rootStateMachine.AddState("Pressed");
        pressedState.motion = pressed;
        pressedState.writeDefaultValues = false;

        rootStateMachine.defaultState = idleState;

        var entranceToIdle = entranceState.AddTransition(idleState);
        entranceToIdle.hasExitTime = true;
        entranceToIdle.exitTime = 1f;
        entranceToIdle.duration = 0f;
        entranceToIdle.offset = 0f;
        entranceToIdle.interruptionSource = TransitionInterruptionSource.None;
        entranceToIdle.canTransitionToSelf = false;
        entranceToIdle.orderedInterruption = true;

        // 任意阶段按下：切入 Pressed，Idle 的呼吸循环被打断（不再播放 Idle Motion）。
        AnimatorStateTransition anyToPressed = rootStateMachine.AddAnyStateTransition(pressedState);
        anyToPressed.canTransitionToSelf = false;
        anyToPressed.hasExitTime = false;
        anyToPressed.duration = 0.05f;
        anyToPressed.offset = 0f;
        anyToPressed.interruptionSource = TransitionInterruptionSource.None;
        anyToPressed.AddCondition(AnimatorConditionMode.If, 0f, "Pressed");

        AnimatorStateTransition pressedToIdle = pressedState.AddTransition(idleState);
        pressedToIdle.hasExitTime = false;
        pressedToIdle.duration = 0.08f;
        pressedToIdle.offset = 0f;
        pressedToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Pressed");

        AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>在开启的 Q2 场景里查找 PlayButton，挂载 Animator + Controller。</summary>
    private static void EnsurePlayButtonAnimator()
    {
        var controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Q2 Bake: AnimatorController 未生成。");
            return;
        }

        string sceneAssetPath = null;
        if (SceneManager.GetActiveScene().IsValid())
            sceneAssetPath = SceneManager.GetActiveScene().path;

        if (sceneAssetPath != Q2ScenePath)
        {
            if (!File.Exists(Q2ScenePath))
            {
                Debug.LogWarning("Q2 Bake: 未找到 " + Q2ScenePath + "，跳过自动挂 Animator。请手动打开 Q2 场景后再执行一次烘焙。");
                return;
            }

            EditorSceneManager.OpenScene(Q2ScenePath);
        }

        GameObject playButton = null;
        foreach (Button b in Object.FindObjectsOfType<Button>(true))
        {
            if (b != null && b.gameObject.scene.IsValid() && b.name == "PlayButton")
            {
                playButton = b.gameObject;
                break;
            }
        }

        if (playButton == null)
        {
            Debug.LogWarning("Q2 Bake: 场景中未找到名为 PlayButton 的 Button，请手动挂 Animator。");
            return;
        }

        var animator = playButton.GetComponent<Animator>();
        if (animator == null)
            animator = playButton.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EditorSceneManager.MarkSceneDirty(playButton.scene);
        EditorSceneManager.SaveScene(playButton.scene);
    }
}

#endif
