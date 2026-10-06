using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Animations;

/// <summary>WebIDL conversions shared by the Web Animations interfaces.</summary>
internal static class AnimationValues
{
    internal static ObjectInstance? Dictionary(AnimationRealm owner, JsValue value)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is not ObjectInstance result)
        {
            Throw.TypeError(owner.Realm, "An animation dictionary must be an object.");
            return null;
        }

        return result;
    }

    internal static double Number(AnimationRealm owner, JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            Throw.TypeError(owner.Realm, "The animation time or rate must be finite.");
        }

        return number;
    }

    internal static double? NullableNumber(AnimationRealm owner, JsValue value)
        => value.IsNullOrUndefined() ? null : Number(owner, value);

    internal static JsValue Value(double? value) => value is { } number ? JsNumber.Create(number) : JsValue.Null;

    internal static string Enum(AnimationRealm owner, JsValue value, params ReadOnlySpan<string> allowed)
    {
        var text = TypeConverter.ToString(value);
        foreach (var candidate in allowed)
        {
            if (text == candidate)
            {
                return text;
            }
        }

        Throw.TypeError(owner.Realm, "Invalid animation enumeration value: " + text);
        return null!;
    }

    internal static CssEasing Easing(AnimationRealm owner, string text)
    {
        if (!CssEasingValues.TryParse(text, new CssValueWork(owner.Dom.CancellationToken, owner.Engine.Constraints.Check),
                out _, out var easing))
        {
            Throw.TypeError(owner.Realm, "Invalid easing function: " + text);
        }

        return easing;
    }

    internal static Element? Target(AnimationRealm owner, JsValue value)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is IDomWrapper { DomTarget: Element element })
        {
            return element;
        }

        Throw.TypeError(owner.Realm, "The animation target must be an Element or null.");
        return null;
    }

    internal static void Required(AnimationRealm owner, JsValue[] args, int count)
    {
        if (args.Length < count)
        {
            Throw.TypeError(owner.Realm, count + " arguments required.");
        }
    }

    internal static void Check(AnimationRealm owner, int index)
    {
        if (index % Engine.ConstraintCheckInterval == 0)
        {
            owner.Engine.Constraints.Check();
        }
    }

    internal static bool IsConnected(Node node)
    {
        var root = DomNodeMembers.Root(node);
        while (root is ShadowRoot shadow)
        {
            root = DomNodeMembers.Root(shadow.Host);
        }

        return root is Document;
    }
}

internal enum EffectPhase : byte
{
    Idle,
    Before,
    Active,
    After,
}

internal readonly record struct EffectSample(EffectPhase Phase, double? Progress, double? CurrentIteration);

/// <summary>https://drafts.csswg.org/web-animations-1/#the-effecttiming-dictionaries</summary>
internal sealed record EffectTiming
{
    internal double Delay { get; init; }
    internal double EndDelay { get; init; }
    internal string Fill { get; init; } = "auto";
    internal double IterationStart { get; init; }
    internal double Iterations { get; init; } = 1;
    internal double? Duration { get; init; }
    internal string Direction { get; init; } = "normal";
    internal CssEasing Easing { get; init; } = CssEasing.Linear;
    private string EasingText { get; init; } = "linear";
    private string? DurationText { get; init; }

    internal double ActiveDuration => Duration is null or 0 || Iterations == 0 ? 0 : Duration.Value * Iterations;
    internal double EndTime => Math.Max(Delay + ActiveDuration + EndDelay, 0);

    /// <summary>
    /// https://drafts.csswg.org/web-animations-1/#updating-animationeffect-timing.
    /// Convert the entire dictionary before validation, and publish only a successfully validated copy.
    /// </summary>
    internal EffectTiming Update(AnimationRealm owner, ObjectInstance? input) => Read(owner, input).Validate(owner);

    internal EffectTiming Read(AnimationRealm owner, ObjectInstance? input)
    {
        if (input is null)
        {
            return this;
        }

        var delay = input.Get("delay");
        var newDelay = delay.IsUndefined() ? Delay : AnimationValues.Number(owner, delay);
        var direction = input.Get("direction");
        var newDirection = direction.IsUndefined() ? Direction
            : AnimationValues.Enum(owner, direction, "normal", "reverse", "alternate", "alternate-reverse");
        var duration = input.Get("duration");
        var newDuration = Duration;
        string? durationString = null;
        if (!duration.IsUndefined())
        {
            if (duration.IsNumber())
            {
                newDuration = TypeConverter.ToNumber(duration);
            }
            else
            {
                durationString = TypeConverter.ToString(duration);
                newDuration = null;
            }
        }

        var easing = input.Get("easing");
        var easingText = easing.IsUndefined() ? Easing.Serialization : TypeConverter.ToString(easing);
        var endDelay = input.Get("endDelay");
        var newEndDelay = endDelay.IsUndefined() ? EndDelay : AnimationValues.Number(owner, endDelay);
        var fill = input.Get("fill");
        var newFill = fill.IsUndefined() ? Fill : AnimationValues.Enum(owner, fill, "none", "forwards", "backwards", "both", "auto");
        var iterationStart = input.Get("iterationStart");
        var newIterationStart = iterationStart.IsUndefined() ? IterationStart : AnimationValues.Number(owner, iterationStart);
        var iterations = input.Get("iterations");
        var newIterations = iterations.IsUndefined() ? Iterations : TypeConverter.ToNumber(iterations);

        return this with
        {
            Delay = newDelay,
            Direction = newDirection,
            Duration = newDuration,
            EasingText = easingText,
            DurationText = durationString,
            EndDelay = newEndDelay,
            Fill = newFill,
            IterationStart = newIterationStart,
            Iterations = newIterations,
        };
    }

    internal EffectTiming Validate(AnimationRealm owner)
    {
        if (IterationStart < 0 || Iterations < 0 || double.IsNaN(Iterations)
            || Duration < 0 || Duration is { } d && double.IsNaN(d)
            || DurationText is not (null or "auto"))
        {
            Throw.TypeError(owner.Realm, "Invalid effect timing.");
        }

        return this with { Easing = AnimationValues.Easing(owner, EasingText) };
    }

    /// <summary>
    /// https://drafts.csswg.org/web-animations-1/#animation-effect-phases-and-states and
    /// https://drafts.csswg.org/web-animations-1/#core-animation-effect-calculations.
    /// </summary>
    internal EffectSample Sample(double? localTime, double playbackRate)
    {
        if (localTime is not { } time)
        {
            return new(EffectPhase.Idle, null, null);
        }

        var beforeBoundary = Math.Max(Math.Min(Delay, EndTime), 0);
        var afterBoundary = Math.Max(Math.Min(Delay + ActiveDuration, EndTime), 0);
        var phase = time < beforeBoundary || playbackRate < 0 && time == beforeBoundary ? EffectPhase.Before
            : time > afterBoundary || playbackRate >= 0 && time == afterBoundary ? EffectPhase.After : EffectPhase.Active;
        double active;
        switch (phase)
        {
            case EffectPhase.Before when Fill is "backwards" or "both":
                active = Math.Max(time - Delay, 0);
                break;
            case EffectPhase.After when Fill is "forwards" or "both":
                active = Math.Max(Math.Min(time - Delay, ActiveDuration), 0);
                break;
            case EffectPhase.Active:
                active = time - Delay;
                break;
            default:
                return new(phase, null, null);
        }

        var overall = (Duration is null or 0 ? phase == EffectPhase.Before ? 0 : Iterations : active / Duration.Value) + IterationStart;
        var simple = double.IsPositiveInfinity(overall) ? IterationStart % 1 : overall % 1;
        if (simple == 0 && phase is EffectPhase.Active or EffectPhase.After && active == ActiveDuration && Iterations != 0)
        {
            simple = 1;
        }

        var iteration = phase == EffectPhase.After && double.IsPositiveInfinity(Iterations)
            ? double.PositiveInfinity : Math.Floor(overall) - (simple == 1 ? 1 : 0);
        var reverse = Direction == "reverse"
            || Direction is "alternate" or "alternate-reverse" && !double.IsInfinity(iteration)
            && (iteration + (Direction == "alternate-reverse" ? 1 : 0)) % 2 != 0;
        var directed = reverse ? 1 - simple : simple;
        var before = phase == EffectPhase.Before && !reverse || phase == EffectPhase.After && reverse;
        return new(phase, Easing.Evaluate(directed, before), iteration);
    }
}

/// <summary>https://drafts.csswg.org/web-animations-1/#the-animationeffect-interface</summary>
internal abstract class JsAnimationEffect : ObjectInstance
{
    protected JsAnimationEffect(AnimationRealm owner) : base(owner.Engine) => Owner = owner;

    internal AnimationRealm Owner { get; }
    internal EffectTiming Timing { get; set; } = new();
    internal JsAnimation? Animation { get; set; }

    internal EffectSample Sample => Timing.Sample(Animation?.CurrentTime, Animation?.PlaybackRate ?? 1);

    internal bool Relevant
    {
        get
        {
            var sample = Sample;
            return sample.Progress is not null || Animation is { } animation
                && (sample.Phase == EffectPhase.Active && animation.PlayState != "finished"
                    || sample.Phase == EffectPhase.Before && animation.PlaybackRate > 0
                    || sample.Phase == EffectPhase.After && animation.PlaybackRate < 0);
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-animationeffect-updatetiming</summary>
    internal void UpdateTiming(JsValue value)
    {
        Timing = Timing.Update(Owner, AnimationValues.Dictionary(Owner, value));
        Animation?.EffectChanged();
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#the-computedeffecttiming-dictionary</summary>
    internal JsObject GetTiming(bool computed)
    {
        using var scope = new RealmScope(Owner.Engine, Owner.Realm);
        var result = new JsObject(Owner.Engine);
        result.CreateDataPropertyOrThrow("delay", Timing.Delay);
        result.CreateDataPropertyOrThrow("direction", Timing.Direction);
        result.CreateDataPropertyOrThrow("duration", Timing.Duration is { } duration ? JsNumber.Create(duration)
            : computed ? JsNumber.PositiveZero : JsString.Create("auto"));
        result.CreateDataPropertyOrThrow("easing", Timing.Easing.Serialization);
        result.CreateDataPropertyOrThrow("endDelay", Timing.EndDelay);
        result.CreateDataPropertyOrThrow("fill", computed && Timing.Fill == "auto" ? "none" : Timing.Fill);
        result.CreateDataPropertyOrThrow("iterationStart", Timing.IterationStart);
        result.CreateDataPropertyOrThrow("iterations", Timing.Iterations);
        if (computed)
        {
            var time = Animation?.CurrentTime;
            var sample = Timing.Sample(time, Animation?.PlaybackRate ?? 1);
            result.CreateDataPropertyOrThrow("endTime", Timing.EndTime);
            result.CreateDataPropertyOrThrow("activeDuration", Timing.ActiveDuration);
            result.CreateDataPropertyOrThrow("localTime", AnimationValues.Value(time));
            result.CreateDataPropertyOrThrow("progress", AnimationValues.Value(sample.Progress));
            result.CreateDataPropertyOrThrow("currentIteration", AnimationValues.Value(sample.CurrentIteration));
        }

        return result;
    }
}
