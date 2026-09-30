using System.Text;
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Animations;

/// <summary>https://drafts.csswg.org/web-animations-1/#the-keyframeeffect-interface</summary>
internal sealed class JsKeyframeEffect : JsAnimationEffect
{
    private static readonly Dictionary<string, string> _propertyNames = PropertyNames();
    private Element? _target;
    private string? _pseudoElement;
    private List<Keyframe> _frames = [];

    private JsKeyframeEffect(AnimationRealm owner) : base(owner) => _prototype = owner.KeyframeEffectPrototype;

    internal Element? Target
    {
        get => _target;
        set
        {
            _target = value;
            Animation?.EffectChanged();
        }
    }

    internal string Composite { get; set; } = "replace";

    internal string? PseudoElement
    {
        get => _pseudoElement;
        set
        {
            if (value is ":before" or ":after" or ":first-letter" or ":first-line")
            {
                value = ":" + value;
            }

            if (value is not (null or "::before" or "::after" or "::first-letter" or "::first-line"
                or "::marker" or "::backdrop" or "::placeholder" or "::selection" or "::file-selector-button"))
            {
                DomFailures.Refuse(Owner.Dom, "KeyframeEffect.pseudoElement", DomExceptionNames.Syntax, "Invalid or unsupported pseudo-element selector.");
            }

            _pseudoElement = value;
            Animation?.EffectChanged();
        }
    }

    internal IReadOnlyList<Keyframe> Frames => _frames;

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-keyframeeffect-keyframeeffect</summary>
    internal static JsKeyframeEffect Construct(AnimationRealm owner, JsValue[] args)
    {
        if (args.Length == 1 && args[0] is JsKeyframeEffect source)
        {
            var copy = new JsKeyframeEffect(owner)
            {
                _target = source.Target,
                _pseudoElement = source.PseudoElement,
                Composite = source.Composite,
                Timing = source.Timing,
            };
            foreach (var frame in source._frames)
            {
                AnimationValues.Check(owner, copy._frames.Count);
                copy._frames.Add(frame.Copy(owner));
            }

            return copy;
        }

        AnimationValues.Required(owner, args, 2);
        var target = AnimationValues.Target(owner, args[0]);
        RequireKeyframes(owner, args[1]);
        return Create(owner, target, args[1], args.At(2), out _, out _);
    }

    internal static JsKeyframeEffect Create(AnimationRealm owner, Element? target, JsValue keyframes, JsValue options,
        out string id, out JsAnimationTimeline? timeline, bool animate = false)
    {
        var effect = new JsKeyframeEffect(owner) { _target = target };
        id = "";
        timeline = owner.DefaultTimeline;
        if (options is ObjectInstance || options.IsNullOrUndefined())
        {
            var dictionary = AnimationValues.Dictionary(owner, options);
            var timing = effect.Timing.Read(owner, dictionary);
            var composite = dictionary?.Get("composite") ?? JsValue.Undefined;
            effect.Composite = composite.IsUndefined() ? "replace" : AnimationValues.Enum(owner, composite, "replace", "add", "accumulate");
            var pseudo = dictionary?.Get("pseudoElement") ?? JsValue.Undefined;
            var pseudoText = pseudo.IsNullOrUndefined() ? null : TypeConverter.ToString(pseudo);
            if (animate)
            {
                var identifier = dictionary?.Get("id") ?? JsValue.Undefined;
                id = identifier.IsUndefined() ? "" : TypeConverter.ToString(identifier);
                var suppliedTimeline = dictionary?.Get("timeline") ?? JsValue.Undefined;
                if (!suppliedTimeline.IsUndefined())
                {
                    timeline = AnimationRealm.TimelineArgument(owner, suppliedTimeline);
                }
            }

            effect.PseudoElement = pseudoText;
            effect.Timing = timing.Validate(owner);
        }
        else
        {
            var duration = TypeConverter.ToNumber(options);
            if (duration < 0 || double.IsNaN(duration))
            {
                Throw.TypeError(owner.Realm, "Invalid animation duration.");
            }

            effect.Timing = effect.Timing with { Duration = duration };
        }

        effect.SetKeyframes(keyframes);
        return effect;
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#dom-keyframeeffect-getkeyframes</summary>
    internal JsArray GetKeyframes()
    {
        using var scope = new RealmScope(Owner.Engine, Owner.Realm);
        var values = new JsValue[_frames.Count];
        for (var i = 0; i < values.Length; i++)
        {
            AnimationValues.Check(Owner, i);
            var frame = _frames[i];
            var result = new JsObject(Owner.Engine);
            result.CreateDataPropertyOrThrow("offset", AnimationValues.Value(frame.Offset));
            result.CreateDataPropertyOrThrow("computedOffset", frame.ComputedOffset);
            result.CreateDataPropertyOrThrow("easing", frame.Easing);
            result.CreateDataPropertyOrThrow("composite", frame.Composite);
            var propertyIndex = 0;
            foreach (var property in frame.Properties)
            {
                AnimationValues.Check(Owner, propertyIndex++);
                result.CreateDataPropertyOrThrow(property.Key, property.Value);
            }

            values[i] = result;
        }

        return Owner.Realm.Intrinsics.Array.ConstructFast(values);
    }

    /// <summary>
    /// https://drafts.csswg.org/web-animations-1/#process-a-keyframes-argument.
    /// All observable reads and string conversions precede offset/easing validation.
    /// </summary>
    internal void SetKeyframes(JsValue value)
    {
        RequireKeyframes(Owner, value);
        var frames = new List<Keyframe>();
        var unusedEasings = new List<string>();
        var propertyIndex = 0;
        if (value is ObjectInstance input)
        {
            var method = input.GetMethod(GlobalSymbolRegistry.Iterator);
            if (method is not null)
            {
                var iterator = input.GetIterator(Owner.Realm, method: method);
                while (iterator.TryIteratorStepValue(out var next))
                {
                    AnimationValues.Check(Owner, frames.Count);
                    var dictionary = AnimationValues.Dictionary(Owner, next);
                    var frame = new Keyframe();
                    if (dictionary is not null)
                    {
                        var composite = dictionary.Get("composite");
                        frame.Composite = composite.IsUndefined() ? "auto" : CompositeValue(composite);
                        var easing = dictionary.Get("easing");
                        frame.Easing = easing.IsUndefined() ? "linear" : TypeConverter.ToString(easing);
                        frame.Offset = AnimationValues.NullableNumber(Owner, dictionary.Get("offset"));
                        foreach (var name in Names(dictionary))
                        {
                            AnimationValues.Check(Owner, propertyIndex++);
                            frame.Properties.Add(name, TypeConverter.ToString(dictionary.Get(name)));
                        }
                    }

                    frames.Add(frame);
                }
            }
            else
            {
                var composites = Sequence(input.Get("composite"), CompositeValue);
                var easings = Sequence(input.Get("easing"), TypeConverter.ToString);
                var offsets = Sequence(input.Get("offset"), v => AnimationValues.NullableNumber(Owner, v));
                var byOffset = new SortedDictionary<double, Keyframe>();
                foreach (var name in Names(input))
                {
                    AnimationValues.Check(Owner, propertyIndex++);
                    var items = Sequence(input.Get(name), TypeConverter.ToString, undefinedIsEmpty: false);
                    for (var i = 0; i < items.Count; i++)
                    {
                        AnimationValues.Check(Owner, i);
                        var offset = items.Count == 1 ? 1 : (double) i / (items.Count - 1);
                        if (!byOffset.TryGetValue(offset, out var frame))
                        {
                            frame = new Keyframe();
                            byOffset.Add(offset, frame);
                        }

                        frame.Properties.Add(name, items[i]);
                    }
                }

                frames.AddRange(byOffset.Values);
                for (var i = 0; i < frames.Count; i++)
                {
                    AnimationValues.Check(Owner, i);
                    var frame = frames[i];
                    frame.Offset = i < offsets.Count ? offsets[i] : null;
                    if (easings.Count > 0)
                    {
                        frame.Easing = easings[i % easings.Count];
                    }

                    if (composites.Count > 0)
                    {
                        frame.Composite = composites[i % composites.Count];
                    }
                }

                if (easings.Count > frames.Count)
                {
                    unusedEasings.AddRange(easings.GetRange(frames.Count, easings.Count - frames.Count));
                }
            }
        }

        var previous = 0d;
        for (var i = 0; i < frames.Count; i++)
        {
            AnimationValues.Check(Owner, i);
            if (frames[i].Offset is { } offset)
            {
                if (offset < previous || offset > 1)
                {
                    Throw.TypeError(Owner.Realm, "Keyframe offsets must be non-decreasing numbers in [0, 1].");
                }

                previous = offset;
            }
        }

        foreach (var frame in frames)
        {
            frame.Easing = AnimationValues.Easing(Owner, frame.Easing).Serialization;
        }

        foreach (var easing in unusedEasings)
        {
            AnimationValues.Easing(Owner, easing);
        }

        Space(frames);
        _frames = frames;
        Animation?.EffectChanged();
    }

    private string CompositeValue(JsValue value) => AnimationValues.Enum(Owner, value, "auto", "replace", "add", "accumulate");

    private List<T> Sequence<T>(JsValue value, Func<JsValue, T> convert, bool undefinedIsEmpty = true)
    {
        var result = new List<T>();
        if (undefinedIsEmpty && value.IsUndefined())
        {
            return result;
        }

        if (value is ObjectInstance instance && instance.GetMethod(GlobalSymbolRegistry.Iterator) is { } method)
        {
            var iterator = instance.GetIterator(Owner.Realm, method: method);
            while (iterator.TryIteratorStepValue(out var item))
            {
                AnimationValues.Check(Owner, result.Count);
                try
                {
                    result.Add(convert(item));
                }
                catch (JavaScriptException)
                {
                    iterator.Close(CompletionType.Throw);
                    throw;
                }
            }
        }
        else
        {
            result.Add(convert(value));
        }

        return result;
    }

    private List<string> Names(ObjectInstance input)
    {
        var names = new List<string>();
        foreach (var key in input.GetOwnPropertyKeys(Types.String))
        {
            AnimationValues.Check(Owner, names.Count);
            var name = TypeConverter.ToString(key);
            if (input.GetOwnProperty(key).Enumerable && (_propertyNames.ContainsKey(name) || name.StartsWith("--", StringComparison.Ordinal) && name.Length > 2))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static void RequireKeyframes(AnimationRealm owner, JsValue value)
    {
        if (!value.IsNullOrUndefined() && value is not ObjectInstance)
        {
            Throw.TypeError(owner.Realm, "Keyframes must be an object or null.");
        }
    }

    /// <summary>https://drafts.csswg.org/web-animations-1/#compute-missing-keyframe-offsets</summary>
    private void Space(List<Keyframe> frames)
    {
        if (frames.Count == 0)
        {
            return;
        }

        frames[^1].ComputedOffset = frames[^1].Offset ?? 1;
        if (frames.Count == 1)
        {
            return;
        }

        frames[0].ComputedOffset = frames[0].Offset ?? 0;
        var previous = 0;
        for (var i = 1; i < frames.Count; i++)
        {
            AnimationValues.Check(Owner, i);
            if (frames[i].Offset is null && i != frames.Count - 1)
            {
                continue;
            }

            var end = frames[i].ComputedOffset = frames[i].Offset ?? 1;
            var start = frames[previous].ComputedOffset;
            for (var j = previous + 1; j < i; j++)
            {
                AnimationValues.Check(Owner, j);
                frames[j].ComputedOffset = start + (end - start) * (j - previous) / (i - previous);
            }

            previous = i;
        }
    }

    /// <summary>
    /// https://drafts.csswg.org/web-animations-1/#commit-an-animation.
    /// With no interpolated style engine, commit only the last specified value at or before progress.
    /// </summary>
    internal void CommitStyles()
    {
        if (Target is not { } target || PseudoElement is not null || target.NamespaceUri is not (Namespaces.Html or Namespaces.Svg))
        {
            DomFailures.Refuse(Owner.Dom, "Animation.commitStyles", DomExceptionNames.NoModificationAllowed,
                "The effect target has no writable style attribute.");
            return;
        }

        if (!AnimationValues.IsConnected(target))
        {
            DomFailures.Refuse(Owner.Dom, "Animation.commitStyles", DomExceptionNames.InvalidState, "The effect target is not connected.");
        }

        if (Sample.Progress is not { } progress)
        {
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var propertyIndex = 0;
        for (var i = 0; i < _frames.Count; i++)
        {
            AnimationValues.Check(Owner, i);
            var frame = _frames[i];
            if (frame.ComputedOffset > progress)
            {
                break;
            }

            foreach (var property in frame.Properties)
            {
                AnimationValues.Check(Owner, propertyIndex++);
                values[property.Key] = property.Value;
            }
        }

        DomFailures.PrepareMutation(Owner.Dom, target);
        using var mutation = Owner.Dom.MutateLayout();
        var style = NativeCssDeclarations.Of(Owner.Dom, target);
        foreach (var property in values)
        {
            AnimationValues.Check(Owner, propertyIndex++);
            style.SetProperty(CssName(property.Key), property.Value);
        }

        DomFailures.CompleteMutation(Owner.Dom, target);
    }

    internal static string CssName(string idl) => _propertyNames.GetValueOrDefault(idl, idl);

    /// <summary>https://drafts.csswg.org/web-animations-1/#animation-property-name-to-idl-attribute-name</summary>
    private static Dictionary<string, string> PropertyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in CssPropertyCatalog.Names)
        {
            if (name.StartsWith("animation", StringComparison.Ordinal) || name.StartsWith("transition", StringComparison.Ordinal))
            {
                continue;
            }

            var idl = new StringBuilder();
            var upper = false;
            foreach (var c in name)
            {
                if (c == '-')
                {
                    upper = true;
                }
                else
                {
                    idl.Append(upper ? char.ToUpperInvariant(c) : c);
                    upper = false;
                }
            }

            names[name == "float" ? "cssFloat" : name == "offset" ? "cssOffset" : idl.ToString()] = name;
        }

        names["cssOffset"] = "offset";
        return names;
    }

    internal sealed class Keyframe
    {
        internal double? Offset { get; set; }
        internal double ComputedOffset { get; set; }
        internal string Easing { get; set; } = "linear";
        internal string Composite { get; set; } = "auto";
        internal Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);

        internal Keyframe Copy(AnimationRealm owner)
        {
            var result = new Keyframe { Offset = Offset, ComputedOffset = ComputedOffset, Easing = Easing, Composite = Composite };
            foreach (var property in Properties)
            {
                AnimationValues.Check(owner, result.Properties.Count);
                result.Properties.Add(property.Key, property.Value);
            }

            return result;
        }
    }
}
