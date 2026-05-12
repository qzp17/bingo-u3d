using UnityEngine;

/// <summary>
/// 「出场」动效的<strong>时间与数值定义</strong>：供运行时查阅（时长等），并供编辑器烘焙 AnimationClip 时采样。
/// Animation 剪辑由编辑器菜单 Tools/Q2/Bake Entrance Animation Assets（见 <c>Assets/Editor/Q2BakeEntranceAnimationAssets.cs</c>）
/// 根据本类的公式<strong>程序化生成关键帧</strong>，与原协程版保持一致；Idle 剪辑另含独立「呼吸」缩放循环。
/// </summary>
public static class Q2EntranceAnimationMath
{
    /// <summary>整场出场时长（秒），与原版协程动画一致。</summary>
    public const float Duration = 0.72f;

    /// <summary>Ease-out-back 的过冲系数（略小于典型值以偏弱回弹）。</summary>
    public const float BackOvershoot = 1.35f;

    /// <summary>左右摆动基准角速度（弧度/秒）。</summary>
    public const float SwayBaseAngularSpeed = 26f;

    /// <summary>在整场时长内<strong>额外</strong>叠加的完整左右往复次数（相位 +N·2π）。</summary>
    public const float SwayExtraFullCyclesDuringEntrance = 2f;

    /// <summary>摆动最大角度（度），包络后叠加在 Z 欧拉角上。</summary>
    public const float SwayMaxDegrees = 5.5f;

    /// <summary>果冻挤压第一段（上下压扁）— 起始归一化时刻。</summary>
    public const float SquashVertPhaseStart = 0.09f;

    /// <summary>果冻挤压第一段（上下压扁）— 结束归一化时刻。</summary>
    public const float SquashVertPhaseEnd = 0.41f;

    public const float SquashHorizPhaseStart = 0.43f;
    public const float SquashHorizPhaseEnd = 0.77f;

    public const float VertSquashYDepth = 0.145f;
    public const float VertSquashXBulge = 0.108f;
    public const float VertReboundYBump = 0.058f;
    public const float VertReboundXTrim = 0.036f;

    public const float HorizSquashXDepth = 0.134f;
    public const float HorizSquashYBulge = 0.092f;
    public const float HorizReboundXBump = 0.05f;
    public const float HorizReboundYTrim = 0.03f;

    /// <summary>待机「呼吸」一整轮时长（秒）；剪辑循环该段，首尾尺度一致可无缝衔接。</summary>
    public const float IdleBreathCycleDuration = 2.45f;

    /// <summary>呼吸时 X 轴相对 1 的正弦振幅（约 ± 该值）。数值略小，避免 UI 按钮看起来像弹性球。</summary>
    public const float IdleBreathScaleAmplitudeX = 0.014f;

    /// <summary>Y 轴振幅在 X 基础上的倍率，略大于 1 时「长高/压扁」更明显，接近原地胸腔起伏。</summary>
    public const float IdleBreathVerticalEmphasis = 1.22f;

    /// <summary>模拟 Touch 按下时 XY 统一缩放系数（&lt;1 略缩小）。</summary>
    public const float TouchPressedScaleMultiplierXY = 0.89f;

    /// <summary>按下时 Graphic 颜色的 RGB 乘以该系数（相对场景默认纯白 1）；α 保持不变，形成「哑黑」。</summary>
    public const float TouchPressedColorRgbFactor = 0.71f;

    /// <summary>
    /// 在给定已过时间采样：世界/父节点不变前提下，应用到 PlayButton RectTransform 的局部缩放与绕 Z 旋转。
    /// </summary>
    public static void Evaluate(float elapsed, out Vector3 localScale, out Quaternion localRotation)
    {
        float tNorm = Mathf.Clamp01(elapsed / Duration);
        float scaleFactor = EaseOutBack(tNorm, BackOvershoot);
        Vector2 jelly = GetJellySquashMultipliers(tNorm);
        localScale = new Vector3(scaleFactor * jelly.x, scaleFactor * jelly.y, scaleFactor);

        float swayOmega =
            SwayBaseAngularSpeed + (SwayExtraFullCyclesDuringEntrance * 2f * Mathf.PI / Duration);
        float swayEnvelope = (1f - tNorm) * (1f - tNorm);
        float eulerZ = Mathf.Sin(elapsed * swayOmega) * SwayMaxDegrees * swayEnvelope;

        localRotation = Quaternion.Euler(0f, 0f, eulerZ);
    }

    private static float EaseOutBack(float tt, float c1)
    {
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(tt - 1f, 3f) + c1 * Mathf.Pow(tt - 1f, 2f);
    }

    private static Vector2 GetJellySquashMultipliers(float tNorm)
    {
        float mulX = 1f;
        float mulY = 1f;

        float uVert = InverseLerpClamped(SquashVertPhaseStart, SquashVertPhaseEnd, tNorm);
        if (uVert > 0f)
        {
            float bellVert = Mathf.Sin(Mathf.PI * uVert);
            mulY *= 1f - VertSquashYDepth * bellVert;
            mulX *= 1f + VertSquashXBulge * bellVert;

            float vertTailGate = InverseLerpClamped(0.82f, 1f, uVert);
            float vertTailBell = Mathf.Sin(Mathf.PI * vertTailGate);
            mulY *= 1f + VertReboundYBump * vertTailBell;
            mulX *= 1f - VertReboundXTrim * vertTailBell;
        }

        float uHoriz = InverseLerpClamped(SquashHorizPhaseStart, SquashHorizPhaseEnd, tNorm);
        if (uHoriz > 0f)
        {
            float bellHoriz = Mathf.Sin(Mathf.PI * uHoriz);
            mulX *= 1f - HorizSquashXDepth * bellHoriz;
            mulY *= 1f + HorizSquashYBulge * bellHoriz;

            float horizTailGate = InverseLerpClamped(0.82f, 1f, uHoriz);
            float horizTailBell = Mathf.Sin(Mathf.PI * horizTailGate);
            mulX *= 1f + HorizReboundXBump * horizTailBell;
            mulY *= 1f - HorizReboundYTrim * horizTailBell;
        }

        return new Vector2(mulX, mulY);
    }

    private static float InverseLerpClamped(float edgeA, float edgeB, float value)
    {
        float denom = edgeB - edgeA;
        if (Mathf.Abs(denom) < 1e-6f)
            return value >= edgeB ? 1f : 0f;

        return Mathf.Clamp01((value - edgeA) / denom);
    }
}
