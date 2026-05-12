using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/*
录屏路径：`Assets/Q2/CandyCrush.MP4`

请仔细观察根目录中提供的知名消除游戏 Candy Crush 录屏中，选关界面对话框 Play 按钮的动画效果，请复刻这一效果，使用代码实现或者 Animation 均可，动画包括：
1. 按钮出现
2. 按钮按下
3. 按钮弹起
*/

/// <summary>
/// Q2：复刻 Candy Crush 风格按钮动效（Animation + 少量驱动代码；无 Animation 时纯代码驱动）。
/// </summary>
public class Q2 : MonoBehaviour
{
    private static readonly int EntranceStateHash = Animator.StringToHash("Entrance");
    private static readonly int PressedBoolHash = Animator.StringToHash("Pressed");

    [SerializeField]
    private Button button = null;

    /// <summary>
    /// 勾选：由 PlayButton 上 Animator + Controller 驱动；取消：运行时关闭该 Animator，出场/待机/Touch 回弹全由脚本写 RectTransform（避免「挂着 Controller 却只 SetBool」看不到协程回弹）。
    /// </summary>
    [SerializeField]
    private bool usePlayButtonAnimator = false;

    [SerializeField]
    private Animator playButtonAnimator = null;

    [SerializeField]
    private RectTransform playButtonRect = null;

    [SerializeField]
    private Graphic playButtonGraphic = null;

    private Color _graphicBaseColor = Color.white;

    private Coroutine _entranceCoroutine = null;

    private Coroutine _touchCoroutine = null;

    private bool _entranceActive = false;

    private bool _pressed = false;

    /// <summary>代码路径：按下/松开短时弹跳进行中，避免与呼吸待机抢 Transform。</summary>
    private bool _touchAnimating = false;

    /// <summary>代码路径下仅在完成至少一次出场后进入呼吸待机，与 Animator 流程一致。</summary>
    private bool _codeIdleEnabled = false;

    private const float TouchPressPopDuration = 0.16f;

    /// <summary>按下前半段占比：先略放大再压入，形成可感知的「弹」感。</summary>
    private const float TouchPressAnticipationPhase = 0.3f;

    /// <summary>预备阶段相对当前缩放的放大系数（&gt;1 微胀）。</summary>
    private const float TouchPressAnticipationScaleBoost = 1.09f;

    /// <summary>按下压入时 OutBack 过冲系数（越大“多压一截再弹回”越明显）。</summary>
    private const float TouchPressBackOvershoot = 2.05f;

    /// <summary>松开回弹 OutBack 过冲（越大放手时“顶出去再落回”越明显）。</summary>
    private const float TouchReleaseBackOvershoot = 2.85f;

    private const float TouchReleasePopDuration = 0.21f;

    private void Awake()
    {
        if (button == null)
            return;

        if (playButtonRect == null)
            playButtonRect = button.transform as RectTransform;

        if (playButtonGraphic == null)
            playButtonGraphic = button.targetGraphic;

        if (playButtonAnimator == null)
            playButtonAnimator = button.GetComponent<Animator>();

        if (playButtonAnimator != null)
            playButtonAnimator.enabled = usePlayButtonAnimator;

        if (playButtonGraphic != null)
            _graphicBaseColor = playButtonGraphic.color;
    }

    private void OnDisable()
    {
        if (_entranceCoroutine != null)
        {
            StopCoroutine(_entranceCoroutine);
            _entranceCoroutine = null;
        }

        StopTouchCoroutine();

        _entranceActive = false;
    }

    private void Update()
    {
        if (UseAnimatorPath() || !CanUseCodePath() || !_codeIdleEnabled || _entranceActive || _pressed ||
            _touchAnimating)
            return;

        ApplyIdleBreath();
    }

    public void OnShowBtnClick()
    {
        if (UseAnimatorPath())
        {
            var anim = playButtonAnimator;
            anim.SetBool(PressedBoolHash, false);
            anim.Update(0f);
            anim.CrossFade(EntranceStateHash, 0f, 0, 0f);
            return;
        }

        if (!CanUseCodePath())
        {
            Debug.LogWarning(
                "Q2：请指定 PlayButton；或先执行 Tools / Q2 / Bake Entrance Animation Assets 以使用 Animator。");
            return;
        }

        if (_entranceCoroutine != null)
        {
            StopCoroutine(_entranceCoroutine);
            _entranceCoroutine = null;
        }

        StopTouchCoroutine();
        _pressed = false;
        _entranceCoroutine = StartCoroutine(EntranceRoutine());
    }

    /// <summary>
    /// Touch 按下： Animator 参数 <c>Pressed=true</c>，经 AnyState → Pressed，缩小、压暗并固定（呼吸停止）。
    /// </summary>
    public void OnTouchDownBtnClick()
    {
        if (UseAnimatorPath())
        {
            playButtonAnimator.SetBool(PressedBoolHash, true);
            return;
        }

        if (!CanUseCodePath())
            return;

        if (_entranceCoroutine != null)
        {
            StopCoroutine(_entranceCoroutine);
            _entranceCoroutine = null;
        }

        StopTouchCoroutine();

        _entranceActive = false;
        _pressed = true;
        _touchAnimating = true;
        _touchCoroutine = StartCoroutine(TouchPressPopRoutine());
    }

    /// <summary>
    /// Touch 松开：<c>Pressed=false</c>，Pressed → Idle，恢复呼吸待机。
    /// </summary>
    public void OnTouchUpBtnClick()
    {
        if (UseAnimatorPath())
        {
            playButtonAnimator.SetBool(PressedBoolHash, false);
            return;
        }

        if (!CanUseCodePath())
            return;

        StopTouchCoroutine();

        _pressed = false;
        _touchAnimating = true;
        _touchCoroutine = StartCoroutine(TouchReleasePopRoutine());
    }

    private bool UseAnimatorPath()
    {
        return usePlayButtonAnimator &&
               button != null &&
               playButtonAnimator != null &&
               playButtonAnimator.enabled &&
               playButtonAnimator.runtimeAnimatorController != null;
    }

    private bool CanUseCodePath()
    {
        return button != null && playButtonRect != null;
    }

    private void StopTouchCoroutine()
    {
        if (_touchCoroutine == null)
            return;

        StopCoroutine(_touchCoroutine);
        _touchCoroutine = null;
        _touchAnimating = false;
    }

    private static float EaseOutBack01(float tNorm, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(tNorm - 1f, 3f) + c1 * Mathf.Pow(tNorm - 1f, 2f);
    }

    private IEnumerator TouchPressPopRoutine()
    {
        float duration = TouchPressPopDuration;
        float t0 = Time.time;
        float s0 = AverageXYScale(playButtonRect.localScale);
        float m = Q2EntranceAnimationMath.TouchPressedScaleMultiplierXY;
        float cf = Q2EntranceAnimationMath.TouchPressedColorRgbFactor;
        Color pressedRgb = new Color(
            _graphicBaseColor.r * cf,
            _graphicBaseColor.g * cf,
            _graphicBaseColor.b * cf,
            _graphicBaseColor.a);

        float antEnd = Mathf.Clamp(TouchPressAnticipationPhase, 0.08f, 0.45f);
        float sPeak = s0 * TouchPressAnticipationScaleBoost;

        while (Time.time - t0 < duration)
        {
            float u = Mathf.Clamp01((Time.time - t0) / duration);
            float s;
            if (u < antEnd)
            {
                float v = antEnd > 1e-4f ? Mathf.Clamp01(u / antEnd) : 1f;
                v = v * v * (3f - 2f * v);
                s = Mathf.LerpUnclamped(s0, sPeak, v);
            }
            else
            {
                float v = Mathf.Clamp01((u - antEnd) / Mathf.Max(1e-4f, 1f - antEnd));
                float k = EaseOutBack01(v, TouchPressBackOvershoot);
                s = Mathf.LerpUnclamped(sPeak, m, k);
            }

            playButtonRect.localScale = new Vector3(s, s, 1f);
            playButtonRect.localRotation = Quaternion.identity;

            if (playButtonGraphic != null)
            {
                float darkenStart = antEnd * 0.35f;
                float dt = Mathf.Clamp01((u - darkenStart) / Mathf.Max(1e-4f, 1f - darkenStart));
                dt = dt * dt * (3f - 2f * dt);
                playButtonGraphic.color = Color.LerpUnclamped(_graphicBaseColor, pressedRgb, dt);
            }

            yield return null;
        }

        ApplyPressedVisual();
        _touchAnimating = false;
        _touchCoroutine = null;
    }

    private IEnumerator TouchReleasePopRoutine()
    {
        float duration = TouchReleasePopDuration;
        float t0 = Time.time;
        float s0 = AverageXYScale(playButtonRect.localScale);
        Color startCol = playButtonGraphic != null ? playButtonGraphic.color : _graphicBaseColor;

        while (Time.time - t0 < duration)
        {
            float u = Mathf.Clamp01((Time.time - t0) / duration);
            float k = EaseOutBack01(u, TouchReleaseBackOvershoot);
            Vector3 idleNow = GetIdleScaleAt(Time.time);
            float nowAvg = AverageXYScale(idleNow);
            float denom = nowAvg > 1e-6f ? nowAvg : 1f;
            float s = Mathf.LerpUnclamped(s0, nowAvg, k);
            float mul = s / denom;
            playButtonRect.localScale = new Vector3(idleNow.x * mul, idleNow.y * mul, 1f);
            playButtonRect.localRotation = Quaternion.identity;

            if (playButtonGraphic != null)
            {
                float ck = Mathf.Clamp01(k);
                Color endCol = new Color(_graphicBaseColor.r, _graphicBaseColor.g, _graphicBaseColor.b, startCol.a);
                playButtonGraphic.color = Color.LerpUnclamped(startCol, endCol, ck);
            }

            yield return null;
        }

        playButtonRect.localScale = GetIdleScaleAt(Time.time);
        playButtonRect.localRotation = Quaternion.identity;
        ApplyGraphicBaseColor();

        _touchAnimating = false;
        _touchCoroutine = null;
    }

    private static float AverageXYScale(Vector3 localScale)
    {
        return (localScale.x + localScale.y) * 0.5f;
    }

    private Vector3 GetIdleScaleAt(float time)
    {
        float cycle = Q2EntranceAnimationMath.IdleBreathCycleDuration;
        float t = Mathf.Repeat(time, cycle);
        float phase = (2f * Mathf.PI * t) / cycle;
        float wobble = Mathf.Sin(phase);
        float ampX = Q2EntranceAnimationMath.IdleBreathScaleAmplitudeX;
        float ampY = ampX * Q2EntranceAnimationMath.IdleBreathVerticalEmphasis;
        return new Vector3(1f + ampX * wobble, 1f + ampY * wobble, 1f);
    }

    private IEnumerator EntranceRoutine()
    {
        _entranceActive = true;
        float start = Time.time;
        float duration = Q2EntranceAnimationMath.Duration;

        while (Time.time - start < duration)
        {
            float elapsed = Time.time - start;
            ApplyEntranceAt(elapsed);
            yield return null;
        }

        ApplyEntranceAt(duration);
        _codeIdleEnabled = true;
        _entranceActive = false;
        _entranceCoroutine = null;
    }

    private void ApplyEntranceAt(float elapsed)
    {
        Q2EntranceAnimationMath.Evaluate(elapsed, out Vector3 scale, out Quaternion rotation);
        playButtonRect.localScale = scale;
        playButtonRect.localRotation = rotation;
        ApplyGraphicBaseColor();
    }

    private void ApplyIdleBreath()
    {
        float cycle = Q2EntranceAnimationMath.IdleBreathCycleDuration;
        float t = Mathf.Repeat(Time.time, cycle);
        float phase = (2f * Mathf.PI * t) / cycle;
        float wobble = Mathf.Sin(phase);
        float ampX = Q2EntranceAnimationMath.IdleBreathScaleAmplitudeX;
        float ampY = ampX * Q2EntranceAnimationMath.IdleBreathVerticalEmphasis;

        playButtonRect.localScale = new Vector3(1f + ampX * wobble, 1f + ampY * wobble, 1f);
        playButtonRect.localRotation = Quaternion.identity;
        ApplyGraphicBaseColor();
    }

    private void ApplyPressedVisual()
    {
        float m = Q2EntranceAnimationMath.TouchPressedScaleMultiplierXY;
        playButtonRect.localScale = new Vector3(m, m, 1f);
        playButtonRect.localRotation = Quaternion.identity;

        if (playButtonGraphic == null)
            return;

        float cf = Q2EntranceAnimationMath.TouchPressedColorRgbFactor;
        var c = playButtonGraphic.color;
        playButtonGraphic.color = new Color(
            _graphicBaseColor.r * cf,
            _graphicBaseColor.g * cf,
            _graphicBaseColor.b * cf,
            c.a);
    }

    private void ApplyGraphicBaseColor()
    {
        if (playButtonGraphic == null)
            return;

        var c = playButtonGraphic.color;
        playButtonGraphic.color = new Color(_graphicBaseColor.r, _graphicBaseColor.g, _graphicBaseColor.b, c.a);
    }
}
