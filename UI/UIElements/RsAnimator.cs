namespace AbsoluteRP.RsUI;

/// Time-based float easing. Every animatable property (opacity, slide-in offset, scale) is one of these. The manager ticks each window's animators once per frame with the frame's delta seconds, and the window reads Current when it draws. This isn't a full tween library - it's intentionally the simplest thing that produces buttery motion: a target value, a duration, and a curve chosen from EaseCurve (defaults to symmetric smoothstep). For chrome that needs to feel snappy - sliding panels, dropdown reveals - set Curve to EaseOutExpo or EaseOutQuart so motion starts fast and decelerates into place.
public sealed class RsAnimator
{
    private float _startValue;
    private float _targetValue;
    private float _elapsed;
    private float _duration;

    public RsAnimator(float initial)
    {
        _startValue = initial;
        _targetValue = initial;
        Current = initial;
    }

    /// Easing shape applied when interpolating Current.
    public EaseCurve Curve { get; set; } = EaseCurve.SmoothStep;

    /// Live interpolated value. Read once per frame at draw time.
    public float Current { get; private set; }

    /// Where Current is heading. Set to change the target.
    public float Target => _targetValue;

    /// True while the value hasn't reached its target yet.
    public bool IsAnimating => _elapsed < _duration;

    /// Aim the value at a new target with the given duration in seconds. If the target equals what we're already aiming at, this is a no-op - otherwise we would restart the animation and stutter.
    public void AnimateTo(float target, float durationSeconds)
    {
        if (Math.Abs(target - _targetValue) < 0.0001f && _elapsed >= _duration) return;
        _startValue = Current;
        _targetValue = target;
        _duration = Math.Max(0.0001f, durationSeconds);
        _elapsed = 0f;
    }

    /// Jump straight to a value with no easing.
    public void SetImmediate(float value)
    {
        _startValue = value;
        _targetValue = value;
        Current = value;
        _duration = 0f;
        _elapsed = 0f;
    }

    /// Advance the animation. Called once per frame by the window system. Applies the curve selected by Curve.
    public void Tick(float deltaSeconds)
    {
        if (_elapsed >= _duration) { Current = _targetValue; return; }
        _elapsed += deltaSeconds;
        if (_elapsed >= _duration)
        {
            Current = _targetValue;
            return;
        }
        var t = _elapsed / _duration;
        var eased = Ease(t, Curve);
        Current = _startValue + (_targetValue - _startValue) * eased;
    }

    private static float Ease(float t, EaseCurve curve) => curve switch
    {
        // Symmetric, gentle both ends.
        EaseCurve.SmoothStep    => t * t * (3f - 2f * t),
        // Snappy start, decelerating tail. Reads as "responsive" for UI chrome that reacts to a click.
        EaseCurve.EaseOutCubic  => 1f - Pow(1f - t, 3),
        // Steeper deceleration than cubic - more emphatic settle.
        EaseCurve.EaseOutQuart  => 1f - Pow(1f - t, 4),
        // The most visibly-easing option: a long, slow tail. Motion takes off fast and coasts noticeably into its resting place.
        EaseCurve.EaseOutQuint  => 1f - Pow(1f - t, 5),
        // Fastest apparent motion; almost snaps then eases the last few pixels. Great for reveals that should feel instant.
        EaseCurve.EaseOutExpo   => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t),
        _                       => t,
    };

    private static float Pow(float x, int n)
    {
        var r = 1f;
        for (var i = 0; i < n; i++) r *= x;
        return r;
    }
}

/// Selectable easing shapes for RsAnimator.
public enum EaseCurve
{
    /// 3t - 2t. Symmetric ease-in/out. Default.
    SmoothStep,
    /// 1 - (1-t). Fast start, soft settle.
    EaseOutCubic,
    /// 1 - (1-t). Faster start, quicker settle than cubic.
    EaseOutQuart,
    /// 1 - (1-t). Long, visible coast into the final position.
    EaseOutQuint,
    /// 1 - 2. Very snappy - almost instantaneous start with a smooth tail.
    EaseOutExpo,
}
