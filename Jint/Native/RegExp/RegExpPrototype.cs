#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.RegExp;
using Jint.Runtime.RegExp.GregsEx;

namespace Jint.Native.RegExp;

[JsObject(UseShape = true)]
internal sealed partial class RegExpPrototype : Prototype
{
    private const int ConstraintCheckInterval = Engine.ConstraintCheckInterval;

    private static readonly JsString PropertyExec = new("exec");
    private static readonly JsString PropertyIndex = new("index");
    private static readonly JsString PropertyInput = new("input");
    private static readonly JsString PropertySticky = new("sticky");
    private static readonly JsString PropertyGlobal = new("global");
    internal static readonly JsString PropertySource = new("source");
    private static readonly JsString DefaultSource = new("(?:)");
    internal static readonly JsString PropertyFlags = new("flags");
    private static readonly JsString PropertyGroups = new("groups");
    private static readonly JsString PropertyIgnoreCase = new("ignoreCase");
    private static readonly JsString PropertyMultiline = new("multiline");
    private static readonly JsString PropertyDotAll = new("dotAll");
    private static readonly JsString PropertyUnicode = new("unicode");
    private static readonly JsString PropertyUnicodeSets = new("unicodeSets");

    [JsProperty(Name = "constructor", Flags = PropertyFlag.Configurable | PropertyFlag.Writable)]
    private readonly RegExpConstructor _constructor;

    // exec is a per-realm instance slot (not a [JsFunction]): HasDefaultExec compares the current exec
    // value's underlying delegate against _defaultExec to detect user replacement, so it must be a
    // ClrFunction wrapping _defaultExec with a stable per-realm identity — which the shape's instance slot
    // provides (a generated dispatcher would be a different Function subtype and break the check).
    [JsProperty(Name = "exec", Flags = PropertyFlag.Configurable | PropertyFlag.Writable)]
    private readonly ClrFunction _execFunction;

    private readonly JsCallDelegate _defaultExec;

    internal RegExpPrototype(
        Engine engine,
        Realm realm,
        RegExpConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _defaultExec = Exec;
        _execFunction = new ClrFunction(engine, "exec", _defaultExec, 1, PropertyFlag.Configurable);
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
        _builtInPropertiesVersion = _propertiesVersion;
    }

    // The own-property version this prototype had once its built-in properties were installed. Every
    // redefinition of an own property — an accessor's getter replaced, an accessor turned into a data
    // property, a property deleted — goes through SetOwnProperty or RemoveOwnProperty and moves the
    // version, so while the two still match, every flag accessor is the built-in one.
    private uint _builtInPropertiesVersion;

    /// <summary>
    /// Whether <c>flags</c> and the eight flag accessors it reads (hasIndices, global, ignoreCase,
    /// multiline, dotAll, unicode, unicodeSets, sticky) are still this prototype's built-in accessors,
    /// so reading <c>flags</c> off an instance with no own properties is unobservable.
    /// </summary>
    internal bool HasDefaultFlagAccessors => _propertiesVersion == _builtInPropertiesVersion;

    /// <summary>
    /// <c>ToString(Get(rx, "flags"))</c>, without running the accessors when that is unobservable: an
    /// instance with no own properties whose prototype still carries the built-in accessors would answer
    /// with its own flags in canonical order, which is exactly <see cref="JsRegExp.CanonicalFlags"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetFlagsString(ObjectInstance rx)
    {
        if (rx is JsRegExp { Properties: null } r && r.Prototype is RegExpPrototype { HasDefaultFlagAccessors: true })
        {
            return r.CanonicalFlags;
        }

        return TypeConverter.ToString(rx.Get(PropertyFlags));
    }

    // Spec: each prototype getter, when called on RegExp.prototype itself (not a JsRegExp instance),
    // returns undefined (or a default for source). Cannot use cast typing on thisObject — that would
    // raise TypeError before the proto-check runs.

    [JsAccessor("dotAll")]
    private JsValue DotAllGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.dotAll getter called on non-RegExp object");
        return r.DotAll;
    }

    [JsAccessor("global")]
    private JsValue GlobalGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.global getter called on non-RegExp object");
        return r.Global;
    }

    [JsAccessor("hasIndices")]
    private JsValue HasIndicesGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.hasIndices getter called on non-RegExp object");
        return r.Indices;
    }

    [JsAccessor("ignoreCase")]
    private JsValue IgnoreCaseGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.ignoreCase getter called on non-RegExp object");
        return r.IgnoreCase;
    }

    [JsAccessor("multiline")]
    private JsValue MultilineGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.multiline getter called on non-RegExp object");
        return r.Multiline;
    }

    [JsAccessor("sticky")]
    private JsValue StickyGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.sticky getter called on non-RegExp object");
        return r.Sticky;
    }

    [JsAccessor("unicode")]
    private JsValue UnicodeGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.unicode getter called on non-RegExp object");
        return r.Unicode;
    }

    [JsAccessor("unicodeSets")]
    private JsValue UnicodeSetsGet(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this)) return Undefined;
        var r = thisObject as JsRegExp;
        if (r is null) Throw.TypeError(_realm, "RegExp.prototype.unicodeSets getter called on non-RegExp object");
        return r.UnicodeSets;
    }

    [JsAccessor("flags")]
    private JsValue FlagsGet(JsValue thisObject) => Flags(thisObject);

    [JsAccessor("source")]
    private JsValue SourceGet(JsValue thisObject) => Source(thisObject);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-regexp.prototype.source
    /// </summary>
    private JsValue Source(JsValue thisObject)
    {
        if (ReferenceEquals(thisObject, this))
        {
            return DefaultSource;
        }

        var r = thisObject as JsRegExp;
        if (r is null)
        {
            Throw.TypeError(_realm, "RegExp.prototype.source getter called on non-RegExp object");
        }

        if (string.IsNullOrEmpty(r.Source))
        {
            return JsRegExp.regExpForMatchingAllCharacters;
        }

        return r.Source;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.prototype-@@replace
    /// </summary>
    [JsSymbolFunction("Replace", Length = 2, Flags = global::Jint.Runtime.Descriptors.PropertyFlag.Configurable | global::Jint.Runtime.Descriptors.PropertyFlag.Writable)]
    private JsValue Replace(JsValue thisObject, JsValue stringArg, JsValue replaceValue)
    {
        var rx = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.replace");
        var s = TypeConverter.ToString(stringArg);
        var lengthS = s.Length;
        var functionalReplace = replaceValue is ICallable;

        // we need heavier logic if we have named captures
        var mayHaveNamedCaptures = false;
        if (!functionalReplace)
        {
            var value = TypeConverter.ToString(replaceValue);
            replaceValue = value;
            mayHaveNamedCaptures = value.Contains('$');
        }

        var flags = GetFlagsString(rx);
        var global = flags.Contains('g');

        var fullUnicode = false;

        if (global)
        {
            fullUnicode = flags.Contains('u') || flags.Contains('v');
            rx.Set(JsRegExp.PropertyLastIndex, 0, true);
        }

        // Step 9 above is the only lastIndex write this algorithm performs of its own accord; everything
        // else is left to RegExpBuiltinExec. In particular there is no write *after* the matching is
        // done: for a global regexp the exec that ends the loop has already reset lastIndex to +0, and a
        // non-global one is not supposed to have its lastIndex touched at all (so a -0 stays -0, and a
        // replacer function that assigns lastIndex keeps what it assigned).

        // The two fused fast paths below collapse the whole RegExpExec loop (steps 11-12) into a single
        // engine-level scan, so they may only run where that substitution is unobservable. Two things
        // make it observable, both of them inside RegExpBuiltinExec:
        //   - step 2 reads and coerces lastIndex *before* step 3 reads the flags and step 8 reads the
        //     matcher, so a lastIndex holding an object runs user code from inside the match, and that
        //     code may replace the very pattern being matched (RegExp.prototype.compile);
        //   - a sticky regexp starts matching at lastIndex (step 12.b) and writes the result back.
        // Neither is reproducible in a fused scan, so both send the call down the generic path below.
        // Reading lastIndex here is free of side effects in turn: it is a non-configurable data property
        // of every RegExp instance, so it can never have become an accessor.
        // https://tc39.es/ecma262/#sec-regexpbuiltinexec
        var canFuseExecLoop = rx is JsRegExp fusable
                              && !fusable.Sticky
                              && (global || fusable.Get(JsRegExp.PropertyLastIndex) is JsNumber);

        // Custom engine fast path for simple string replacement (no $-substitutions, no function)
        if (canFuseExecLoop
            && !functionalReplace
            && !mayHaveNamedCaptures
            && rx is JsRegExp { HasDefaultRegExpExec: true, UsesDotNetEngine: false } customRei)
        {
            var customEngine = customRei.Matcher!;
            var replStr = TypeConverter.ToString(replaceValue);
            var sb = new ValueStringBuilder(stackalloc char[256]);

            int lastPos = 0;
            int searchStart = 0;
            int maxCount = global ? int.MaxValue : 1;
            int count = 0;
            using var batch = JintRegExpEngine.BeginBatch();
            var loopTimeout = GetCustomEngineTimeout(customRei);

            while (count < maxCount && searchStart <= s.Length)
            {
                if (count > 0 && count % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                if (!TryMatchRangeWithTimeout(customRei, customEngine, s, searchStart, out var matchIndex, out var matchLength, loopTimeout))
                {
                    break;
                }

                sb.Append(s.AsSpan(lastPos, matchIndex - lastPos));
                sb.Append(replStr);

                lastPos = matchIndex + matchLength;
                searchStart = matchLength == 0
                    ? (int) AdvanceStringIndex(s, (ulong) matchIndex, fullUnicode)
                    : lastPos;
                count++;
            }

            if (count == 0)
            {
                // Nothing matched: the result is the subject, which needs no copy.
                sb.Dispose();
                return s;
            }

            sb.Append(s.AsSpan(lastPos));
            return sb.ToString();
        }

        // Fused lane for a replacer function or a $-template on the ECMAScript matcher. It keeps the
        // specification's order - every match is found (steps 11-12) before any replacement is computed
        // (step 14), so a replacer observes the same RegExp and legacy statics it would on the generic path -
        // but reads capture spans straight off the matcher instead of building and re-reading a result
        // array per match, and accumulates the output in one builder rather than one concatenation per match.
        if (canFuseExecLoop
            && rx is JsRegExp { HasDefaultRegExpExec: true, UsesDotNetEngine: false } fusedRei)
        {
            return FusedReplace(fusedRei, s, global, fullUnicode, functionalReplace, replaceValue);
        }

        // check if we can access fast path (only for .NET Regex engine)
        if (canFuseExecLoop
            && !fullUnicode
            && !mayHaveNamedCaptures
            && rx is JsRegExp { HasDefaultRegExpExec: true, UsesDotNetEngine: true } rei
)
        {
            var count = global ? int.MaxValue : 1;

            string result;
            if (functionalReplace)
            {
                string Evaluator(Match match)
                {
                    var actualGroupCount = GetActualRegexGroupCount(rei, match);

                    ObjectInstance? groups = null;
                    // Pre-initialize groups with unique names in order
                    for (var i = 1; i < actualGroupCount; i++)
                    {
                        var groupName = GetRegexGroupName(rei, i);
                        if (!string.IsNullOrWhiteSpace(groupName))
                        {
                            groups ??= OrdinaryObjectCreate(_engine, null);
                            if (!groups.HasOwnProperty(groupName))
                            {
                                groups.CreateDataPropertyOrThrow(groupName, Undefined);
                            }
                        }
                    }

                    // matched + captures + position + string + optional groups object; the array is freshly
                    // allocated and exactly JsValue[], so writes can skip the covariant store type check.
                    var replacerArgs = new JsValue[actualGroupCount + 2 + (groups is not null ? 1 : 0)];
                    var argIndex = 0;
                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, match.Value);

                    for (var i = 1; i < actualGroupCount; i++)
                    {
                        var capture = match.Groups[i];
                        Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, capture.Success ? capture.Value : Undefined);

                        var groupName = GetRegexGroupName(rei, i);
                        if (!string.IsNullOrWhiteSpace(groupName) && capture.Success)
                        {
                            groups!.CreateDataPropertyOrThrow(groupName, (JsString) capture.Value);
                        }
                    }

                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, match.Index);
                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, s);
                    if (groups is not null)
                    {
                        Arguments.WriteNoTypeCheck(replacerArgs, argIndex, groups);
                    }

                    return CallFunctionalReplace(replaceValue, replacerArgs);
                }

                result = rei.HostRegex!.Replace(s, Evaluator, count);
            }
            else
            {
                result = rei.HostRegex!.Replace(s, TypeConverter.ToString(replaceValue), count);
            }

            return result;
        }

        var results = new List<ObjectInstance>();

        var matchCount = 0;
        while (true)
        {
            if (++matchCount % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var result = RegExpExec(rx, s);
            if (result.IsNull())
            {
                break;
            }

            results.Add((ObjectInstance) result);
            if (!global)
            {
                break;
            }

            var matchStr = TypeConverter.ToString(result.Get(0));
            if (matchStr == "")
            {
                var thisIndex = TypeConverter.ToLength(rx.Get(JsRegExp.PropertyLastIndex));
                var nextIndex = AdvanceStringIndex(s, thisIndex, fullUnicode);
                rx.Set(JsRegExp.PropertyLastIndex, nextIndex);
            }
        }

        var accumulatedResult = "";
        var nextSourcePosition = 0;

        // Step 14.k: a capture keeps undefined rather than becoming "", which a replacer function observes;
        // GetSubstitution reads an undefined capture as the empty string itself.
        var captures = new List<JsValue>();
        for (var i = 0; i < results.Count; i++)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var result = results[i];
            var nCaptures = (int) result.GetLength();
            nCaptures = System.Math.Max(nCaptures - 1, 0);
            var matched = TypeConverter.ToString(result.Get(0));
            var matchLength = matched.Length;
            // Step 14.e: clamp the reported index between 0 and lengthS. The clamp is what establishes
            // GetSubstitution's "position <= stringLength" assertion, so it has to happen while the value
            // is still a double: narrowing first loses the magnitude, and an out-of-range double-to-int
            // conversion saturates on .NET but is unspecified on .NET Framework, where it yields
            // int.MinValue and would turn a far-right index into 0.
            var reportedIndex = TypeConverter.ToInteger(result.Get(PropertyIndex));
            var position = (int) System.Math.Max(System.Math.Min(reportedIndex, lengthS), 0);
            uint n = 1;

            captures.Clear();
            while (n <= nCaptures)
            {
                var capN = result.Get(n);
                captures.Add(!capN.IsUndefined() ? TypeConverter.ToString(capN) : Undefined);
                n++;
            }

            var namedCaptures = result.Get(PropertyGroups);
            string replacement;
            if (functionalReplace)
            {
                // matched + captures + position + string + optional named captures; the array is freshly
                // allocated and exactly JsValue[], so writes can skip the covariant store type check.
                var hasNamedCaptures = !namedCaptures.IsUndefined();
                var replacerArgs = new JsValue[captures.Count + 3 + (hasNamedCaptures ? 1 : 0)];
                var argIndex = 0;
                Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, matched);
                foreach (var capture in captures)
                {
                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, capture);
                }

                Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, position);
                Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, s);
                if (hasNamedCaptures)
                {
                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex, namedCaptures);
                }

                replacement = CallFunctionalReplace(replaceValue, replacerArgs);
            }
            else
            {
                if (!namedCaptures.IsUndefined())
                {
                    namedCaptures = TypeConverter.ToObject(_realm, namedCaptures);
                }

                var captureStrings = new string[captures.Count];
                for (var c = 0; c < captureStrings.Length; c++)
                {
                    captureStrings[c] = captures[c].IsUndefined() ? "" : ((JsString) captures[c]).ToString();
                }

                replacement = GetSubstitution(_realm, matched, s, position, captureStrings, namedCaptures, TypeConverter.ToString(replaceValue));
            }

            if (position >= nextSourcePosition)
            {
                // Checked before the concatenation, so an over-long result is refused rather than
                // built: every match appends both the preserved slice and its replacement, and a
                // replacement pattern can be arbitrarily larger than the match it stands in for.
                JsString.ThrowIfLengthExceeded(
                    _realm,
                    (long) accumulatedResult.Length + (position - nextSourcePosition) + replacement.Length);

#pragma warning disable CA1845
                accumulatedResult = accumulatedResult +
                                    s.Substring(nextSourcePosition, position - nextSourcePosition) +
                                    replacement;
#pragma warning restore CA1845

                nextSourcePosition = position + matchLength;
            }
        }

        if (nextSourcePosition >= lengthS)
        {
            return accumulatedResult;
        }

        JsString.ThrowIfLengthExceeded(_realm, (long) accumulatedResult.Length + (lengthS - nextSourcePosition));

#pragma warning disable CA1845
        return accumulatedResult + s.Substring(nextSourcePosition);
#pragma warning restore CA1845
    }

    private string FusedReplace(JsRegExp rei, string s, bool global, bool fullUnicode, bool functionalReplace, JsValue replaceValue)
    {
        var matcher = rei.Matcher!;
        var groupCount = matcher.CaptureCount;
        var groups = new RegexCapture[groupCount];

        // Steps 11-12: collect every match as flat (index, length) pairs per group; -1 marks a group that did
        // not participate.
        var spans = new List<int>();
        var searchStart = 0;
        var matchCount = 0;
        // No script runs while matches are collected (the replacer is called afterwards), so the searches may share
        // clock readings; the scope ends before the first replacer runs.
        using (JintRegExpEngine.BeginBatch())
        {
            var loopTimeout = GetCustomEngineTimeout(rei);
            while (searchStart <= s.Length)
            {
                if (++matchCount % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                if (!TryMatchWithTimeout(rei, matcher, s, searchStart, groups, loopTimeout))
                {
                    break;
                }

                foreach (var group in groups)
                {
                    spans.Add(group.Participated ? group.Index : -1);
                    spans.Add(group.Length);
                }

                if (!global)
                {
                    break;
                }

                searchStart = groups[0].Length == 0
                    ? (int) AdvanceStringIndex(s, (ulong) groups[0].Index, fullUnicode)
                    : groups[0].Index + groups[0].Length;
            }
        }

        var stride = 2 * groupCount;
        var matches = spans.Count / stride;
        if (matches == 0)
        {
            return s;
        }

        UpdateLegacyStaticProperties(_engine, matcher, s, spans, (matches - 1) * stride);

        var template = functionalReplace ? null : TypeConverter.ToString(replaceValue);
        var sb = new ValueStringBuilder(stackalloc char[256]);
        var nextSourcePosition = 0;
        var captureValues = new JsValue[groupCount - 1];
        var captureStrings = functionalReplace ? null : new string[groupCount - 1];
        for (var m = 0; m < matches; m++)
        {
            if (m > 0 && m % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var offset = m * stride;
            var position = spans[offset];
            var matchLength = spans[offset + 1];
            var matched = s.Substring(position, matchLength);
            for (var g = 1; g < groupCount; g++)
            {
                var index = spans[offset + 2 * g];
                var value = index < 0 ? null : s.Substring(index, spans[offset + 2 * g + 1]);
                captureValues[g - 1] = value is null ? Undefined : value;
                if (captureStrings is not null)
                {
                    captureStrings[g - 1] = value ?? "";
                }
            }

            var namedCaptures = matcher.HasGroupNames ? CreateGroupsObject(_engine, matcher, captureValues) : null;

            string replacement;
            if (functionalReplace)
            {
                var replacerArgs = new JsValue[groupCount + 2 + (namedCaptures is not null ? 1 : 0)];
                var argIndex = 0;
                Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, matched);
                foreach (var capture in captureValues)
                {
                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, capture);
                }

                Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, position);
                Arguments.WriteNoTypeCheck(replacerArgs, argIndex++, s);
                if (namedCaptures is not null)
                {
                    Arguments.WriteNoTypeCheck(replacerArgs, argIndex, namedCaptures);
                }

                replacement = CallFunctionalReplace(replaceValue, replacerArgs);
            }
            else
            {
                replacement = GetSubstitution(_realm, matched, s, position, captureStrings!, (JsValue?) namedCaptures ?? Undefined, template!);
            }

            // Checked before appending, as the generic path does, so an over-long result is refused rather than built.
            JsString.ThrowIfLengthExceeded(_realm, (long) sb.Length + (position - nextSourcePosition) + replacement.Length);
            sb.Append(s.AsSpan(nextSourcePosition, position - nextSourcePosition));
            sb.Append(replacement);
            nextSourcePosition = position + matchLength;
        }

        JsString.ThrowIfLengthExceeded(_realm, (long) sb.Length + (s.Length - nextSourcePosition));
        sb.Append(s.AsSpan(nextSourcePosition));
        return sb.ToString();
    }

    /// <summary>
    /// The <c>groups</c> object of one match: every name once, in source order, holding the value of whichever
    /// group of that name participated (duplicate names are disjoint, so at most one did), or undefined.
    /// https://tc39.es/ecma262/#sec-regexpbuiltinexec steps 33-34
    /// </summary>
    private static ObjectInstance CreateGroupsObject(Engine engine, JintRegExpEngine matcher, JsValue[] captureValues)
    {
        var groups = OrdinaryObjectCreate(engine, null);
        for (var g = 1; g <= captureValues.Length; g++)
        {
            var name = matcher.GetGroupName(g);
            if (name is null)
            {
                continue;
            }

            var value = captureValues[g - 1];
            if (!groups.HasOwnProperty(name))
            {
                groups.CreateDataPropertyOrThrow(name, value);
            }
            else if (!value.IsUndefined())
            {
                groups.CreateDataPropertyOrThrow(name, value);
            }
        }

        return groups;
    }

    private static void UpdateLegacyStaticProperties(Engine engine, JintRegExpEngine matcher, string s, List<int> spans, int offset)
    {
        var constructor = engine.Realm.Intrinsics.RegExp;
        constructor._legacyInput = s;
        constructor.SetLegacyContext(s, spans[offset], spans[offset + 1]);

        var lastParen = "";
        for (var i = 0; i < 9; i++)
        {
            var group = i + 1;
            if (group < matcher.CaptureCount && spans[offset + 2 * group] >= 0)
            {
                var value = s.Substring(spans[offset + 2 * group], spans[offset + 2 * group + 1]);
                constructor._legacyParens[i] = value;
                lastParen = value;
            }
            else
            {
                constructor._legacyParens[i] = "";
            }
        }

        constructor._legacyLastParen = lastParen;
    }

    private static string CallFunctionalReplace(JsValue replacer, JsCallArguments replacerArgs)
    {
        var result = ((ICallable) replacer).Call(Undefined, replacerArgs);
        return TypeConverter.ToString(result);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getsubstitution
    /// </summary>
    /// <remarks>
    /// A single replacement pattern can expand without bound — <c>"$&amp;$&amp;$&amp;…"</c> repeats the
    /// match once per token — so the result is bounded by <see cref="JsString.MaxLength"/> twice
    /// over: once up front from <see cref="MinimumSubstitutionLength"/>, so a pathological expansion
    /// is refused before a character is copied, and then before every append that can contribute more
    /// than one character, which is the exact enforcement. All three callers are instance methods on
    /// a prototype, which is why the realm is a parameter rather than something this method has to
    /// reach for.
    /// </remarks>
    internal static string GetSubstitution(
        Realm realm,
        string matched,
        string str,
        int position,
        string[] captures,
        JsValue namedCaptures,
        string replacement)
    {
        // If there is no pattern, replace the pattern as is.
        if (!replacement.Contains('$'))
        {
            return replacement;
        }

        JsString.ThrowIfLengthExceeded(realm, MinimumSubstitutionLength(matched, str, position, captures, namedCaptures, replacement));

        // Patterns
        // $$	Inserts a "$".
        // $&	Inserts the matched substring.
        // $`	Inserts the portion of the string that precedes the matched substring.
        // $'	Inserts the portion of the string that follows the matched substring.
        // $n or $nn	Where n or nn are decimal digits, inserts the nth parenthesized submatch string, provided the first argument was a RegExp object.
        using var sb = new ValueStringBuilder(stackalloc char[128]);

        // Only the appends below that can contribute more than one character are checked. Every other
        // branch emits at most as many characters as it consumes from `replacement`, whose own length
        // is already bounded by JsString.MaxLength, so the per-character path stays guard-free.
        for (var i = 0; i < replacement.Length; i++)
        {
            char c = replacement[i];
            if (c == '$' && i < replacement.Length - 1)
            {
                c = replacement[++i];
                switch (c)
                {
                    case '$':
                        sb.Append('$');
                        break;
                    case '&':
                        JsString.ThrowIfLengthExceeded(realm, (long) sb.Length + matched.Length);
                        sb.Append(matched);
                        break;
                    case '`':
                        JsString.ThrowIfLengthExceeded(realm, (long) sb.Length + position);
                        sb.Append(str.AsSpan(0, position));
                        break;
                    case '\'':
                        // Step 5.e: tailPos is position + the length of matched, and the replacement is
                        // the substring of str from min(tailPos, stringLength) -- so a match running to
                        // or past the end contributes nothing. tailPos can only exceed stringLength when
                        // @@replace ran against an object whose "exec" is not the intrinsic one and which
                        // reported a matched string longer than what is left of str; the sum is taken as
                        // a long so a pair of near-int.MaxValue lengths cannot wrap into a negative index.
                        var tailPos = (int) System.Math.Min((long) position + matched.Length, str.Length);
                        JsString.ThrowIfLengthExceeded(realm, (long) sb.Length + (str.Length - tailPos));
                        sb.Append(str.AsSpan(tailPos));
                        break;
                    case '<':
                        var gtPos = replacement.IndexOf('>', i + 1);
                        if (gtPos == -1 || namedCaptures.IsUndefined())
                        {
                            sb.Append('$');
                            sb.Append(c);
                        }
                        else
                        {
                            var startIndex = i + 1;
                            var groupName = replacement.Substring(startIndex, gtPos - startIndex);
                            var capture = namedCaptures.Get(groupName);
                            if (!capture.IsUndefined())
                            {
                                var captureText = TypeConverter.ToString(capture);
                                JsString.ThrowIfLengthExceeded(realm, (long) sb.Length + captureText.Length);
                                sb.Append(captureText);
                            }

                            i = gtPos;
                        }
                        break;
                    default:
                        {
                            if (char.IsDigit(c))
                            {
                                int matchNumber1 = c - '0';

                                // The match number can be one or two digits long.
                                int matchNumber2 = 0;
                                if (i < replacement.Length - 1 && char.IsDigit(replacement[i + 1]))
                                {
                                    matchNumber2 = matchNumber1 * 10 + (replacement[i + 1] - '0');
                                }

                                // Try the two digit capture first.
                                if (matchNumber2 > 0 && matchNumber2 <= captures.Length)
                                {
                                    // Two digit capture replacement.
                                    var capture = TypeConverter.ToString(captures[matchNumber2 - 1]);
                                    JsString.ThrowIfLengthExceeded(realm, (long) sb.Length + capture.Length);
                                    sb.Append(capture);
                                    i++;
                                }
                                else if (matchNumber1 > 0 && matchNumber1 <= captures.Length)
                                {
                                    // Single digit capture replacement.
                                    var capture = TypeConverter.ToString(captures[matchNumber1 - 1]);
                                    JsString.ThrowIfLengthExceeded(realm, (long) sb.Length + capture.Length);
                                    sb.Append(capture);
                                }
                                else
                                {
                                    // Capture does not exist.
                                    sb.Append('$');
                                    i--;
                                }
                            }
                            else
                            {
                                // Unknown replacement pattern.
                                sb.Append('$');
                                sb.Append(c);
                            }

                            break;
                        }
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// An exact lower bound on what <see cref="GetSubstitution"/> is about to produce, computed
    /// without copying a character.
    /// </summary>
    /// <remarks>
    /// Only the tokens whose contribution is knowable without running user code are counted; every
    /// other character counts as zero. The result therefore can never exceed the real one and can
    /// never produce a spurious <c>RangeError</c> — it exists so a pathological expansion
    /// (<c>"$&amp;$&amp;$&amp;…"</c> against a large match, the shape issue #3011 reported) is refused
    /// while the builder is still empty, instead of after it has grown to the gigabyte it is going to
    /// discard. The one token deliberately skipped is <c>$&lt;name&gt;</c>, whose capture is resolved
    /// through a <c>Get</c> that can run user code and must not run twice; the per-append checks in
    /// <see cref="GetSubstitution"/> cover it. The loop mirrors that method's parse step for step,
    /// including how far each branch advances, because miscounting <em>upwards</em> is the only way
    /// this can be wrong.
    /// </remarks>
    private static long MinimumSubstitutionLength(
        string matched,
        string str,
        int position,
        string[] captures,
        JsValue namedCaptures,
        string replacement)
    {
        long length = 0;
        for (var i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$' || i >= replacement.Length - 1)
            {
                continue;
            }

            var c = replacement[++i];
            switch (c)
            {
                case '&':
                    length += matched.Length;
                    break;
                case '`':
                    length += position;
                    break;
                case '\'':
                    length += str.Length - (int) System.Math.Min((long) position + matched.Length, str.Length);
                    break;
                case '<':
                    var gtPos = replacement.IndexOf('>', i + 1);
                    if (gtPos != -1 && !namedCaptures.IsUndefined())
                    {
                        // The group name is consumed, exactly as above, so a "$&" spelled inside it
                        // is not counted as a token it never was.
                        i = gtPos;
                    }
                    break;
                default:
                    if (char.IsDigit(c))
                    {
                        var matchNumber1 = c - '0';

                        var matchNumber2 = 0;
                        if (i < replacement.Length - 1 && char.IsDigit(replacement[i + 1]))
                        {
                            matchNumber2 = matchNumber1 * 10 + (replacement[i + 1] - '0');
                        }

                        if (matchNumber2 > 0 && matchNumber2 <= captures.Length)
                        {
                            length += captures[matchNumber2 - 1].Length;
                            i++;
                        }
                        else if (matchNumber1 > 0 && matchNumber1 <= captures.Length)
                        {
                            length += captures[matchNumber1 - 1].Length;
                        }
                        else
                        {
                            // Capture does not exist: the digit is re-read as a literal, as above.
                            i--;
                        }
                    }
                    break;
            }
        }

        return length;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.prototype-@@split
    /// </summary>
    [JsSymbolFunction("Split", Length = 2, Flags = global::Jint.Runtime.Descriptors.PropertyFlag.Configurable | global::Jint.Runtime.Descriptors.PropertyFlag.Writable)]
    private JsValue Split(JsValue thisObject, JsValue stringArg, JsValue limit)
    {
        var rx = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.split");
        var s = TypeConverter.ToString(stringArg);
        var c = SpeciesConstructor(rx, _realm.Intrinsics.RegExp);
        var flags = TypeConverter.ToJsString(rx.Get(PropertyFlags));
        var unicodeMatching = flags.Contains('u') || flags.Contains('v');
        var newFlags = flags.Contains('y') ? flags : new JsString(flags.ToString() + 'y');
        var splitter = Construct(c, [
            rx,
            newFlags
        ]);
        var lim = limit.IsUndefined() ? NumberConstructor.MaxSafeInteger : TypeConverter.ToUint32(limit);

        if (lim == 0)
        {
            return _realm.Intrinsics.Array.ArrayCreate(0);
        }

        if (s.Length == 0)
        {
            var a = _realm.Intrinsics.Array.ArrayCreate(0);
            var z = RegExpExec(splitter, s);
            if (!z.IsNull())
            {
                return a;
            }

            a.SetIndexValue(0, s, updateLength: true);
            return a;
        }

        if (splitter is JsRegExp { HasDefaultRegExpExec: true, UsesDotNetEngine: false } fused)
        {
            if (string.Equals(fused.Source, JsRegExp.regExpForMatchingAllCharacters, StringComparison.Ordinal))
            {
                // The empty pattern matches between every pair of code units (or code points under u/v), which is
                // a split into single characters; there is nothing for a matcher to search for.
                return unicodeMatching
                    ? SplitFused(fused, s, unicodeMatching, lim)
                    : StringPrototype.SplitWithStringSeparator(_engine, _realm, "", s, (uint) System.Math.Min(s.Length, lim));
            }

            return SplitFused(fused, s, unicodeMatching, lim);
        }

        if (!unicodeMatching && splitter is JsRegExp R && R.HasDefaultRegExpExec && R.UsesDotNetEngine)
        {
            // we can take faster path

            if (string.Equals(R.Source, JsRegExp.regExpForMatchingAllCharacters, StringComparison.Ordinal))
            {
                // if empty string, just a string split
                return StringPrototype.SplitWithStringSeparator(_engine, _realm, "", s, (uint) System.Math.Min(s.Length, lim));
            }

            // the result is a plain array per spec (no species for the result), so segments
            // accumulate in a pooled buffer and materialize at exact size
            var builder = new JsValueListBuilder(16);
            try
            {
                int lastIndex = 0;
                var matchCount = 0;

#if NET8_0_OR_GREATER
                // Kept in its own method: inlining it here grew RegExp.prototype.split enough to cost
                // the capture-carrying path ~5% (measured), even though that path never enters it.
                if (GetRegexGroupCount(R) == 1)
                {
                    return SplitWithoutCaptures(R, s, lim, ref builder);
                }
#endif

                for (var match = R.HostRegex!.Match(s, 0); match.Success; match = match.NextMatch())
                {
                    if (++matchCount % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    if (match.Length == 0 && (match.Index == 0 || match.Index == s.Length || match.Index == lastIndex))
                    {
                        continue;
                    }

                    builder.Add(s.Substring(lastIndex, match.Index - lastIndex));

                    if (builder.Length >= lim)
                    {
                        return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                    }

                    lastIndex = match.Index + match.Length;
                    var actualGroupCount = GetActualRegexGroupCount(R, match);
                    for (int i = 1; i < actualGroupCount; i++)
                    {
                        var group = match.Groups[i];
                        var item = Undefined;
                        if (group.Captures.Count > 0)
                        {
                            item = match.Groups[i].Value;
                        }

                        builder.Add(item);

                        if (builder.Length >= lim)
                        {
                            return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                        }
                    }
                }

                // Add the last part of the split
                builder.Add(s.Substring(lastIndex));

                return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
            }
            finally
            {
                builder.Dispose();
            }
        }

        return SplitSlow(s, splitter, unicodeMatching, lim);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Splits on a pattern that has no capture groups, which makes the caller's group loop dead code
    /// and leaves nothing in the scan needing a <see cref="Match"/> — only each match's index and
    /// length. <see cref="Regex.EnumerateMatches(ReadOnlySpan{char})"/> hands those back as a struct,
    /// so a split matching n times no longer allocates n (Match + Int32[] + Int32[][]) triples: .NET
    /// reuses its cached runmatch only when a scan FAILS, so on the Match/NextMatch path every
    /// successful match allocates a fresh one.
    /// </summary>
    private JsValue SplitWithoutCaptures(JsRegExp r, string s, long lim, ref JsValueListBuilder builder)
    {
        var lastIndex = 0;
        var matchCount = 0;

        foreach (var match in r.HostRegex!.EnumerateMatches(s))
        {
            if (++matchCount % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            if (match.Length == 0 && (match.Index == 0 || match.Index == s.Length || match.Index == lastIndex))
            {
                continue;
            }

            builder.Add(s.Substring(lastIndex, match.Index - lastIndex));

            if (builder.Length >= lim)
            {
                return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
            }

            lastIndex = match.Index + match.Length;
        }

        // Add the last part of the split
        builder.Add(s.Substring(lastIndex));
        return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
    }
#endif

    /// <summary>
    /// Steps 19-25 of https://tc39.es/ecma262/#sec-regexp.prototype-%symbol.split% on the ECMAScript matcher. The
    /// specification retries a sticky match at every position q; a forward search from q finds the same leftmost
    /// match without the per-position calls, and the splitter is a fresh internal instance whose lastIndex nothing
    /// else can observe, so leaving it untouched is unobservable.
    /// </summary>
    private JsArray SplitFused(JsRegExp splitter, string s, bool unicodeMatching, long lim)
    {
        var matcher = splitter.Matcher!;
        if (matcher.CaptureCount == 1)
        {
            return SplitFusedWithoutCaptures(splitter, matcher, s, unicodeMatching, lim);
        }

        var groups = new RegexCapture[matcher.CaptureCount];
        var builder = new JsValueListBuilder(16);
        try
        {
            var size = s.Length;
            var p = 0;
            var q = 0;
            var matchCount = 0;
            using var batch = JintRegExpEngine.BeginBatch();
            var loopTimeout = GetCustomEngineTimeout(splitter);
            while (q < size)
            {
                if (++matchCount % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                if (!TryFindWithTimeout(splitter, matcher, s, q, groups, loopTimeout))
                {
                    break;
                }

                var matchStart = groups[0].Index;
                if (matchStart >= size)
                {
                    break;
                }

                var e = System.Math.Min(matchStart + groups[0].Length, size);
                if (e == p)
                {
                    q = (int) AdvanceStringIndex(s, (ulong) matchStart, unicodeMatching);
                    continue;
                }

                builder.Add(s.Substring(p, matchStart - p));
                if (builder.Length >= lim)
                {
                    return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                }

                p = e;
                for (var i = 1; i < groups.Length; i++)
                {
                    var group = groups[i];
                    builder.Add(group.Participated ? s.Substring(group.Index, group.Length) : Undefined);
                    if (builder.Length >= lim)
                    {
                        return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                    }
                }

                q = p;
            }

            builder.Add(s.Substring(p));
            return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// <see cref="SplitFused"/> for a pattern without capture groups, which reads only match ranges and so takes them
    /// from the matcher a batch at a time. A batch's searches each start where the previous match ended, which is
    /// where this loop would search next after a non-empty match; a batch ends after an empty match, whose resume
    /// point is the loop's own rule.
    /// </summary>
    private JsArray SplitFusedWithoutCaptures(JsRegExp splitter, JintRegExpEngine matcher, string s, bool unicodeMatching, long lim)
    {
        Span<RegexMatchRange> ranges = stackalloc RegexMatchRange[32];
        var builder = new JsValueListBuilder(16);
        try
        {
            var size = s.Length;
            var p = 0;
            var q = 0;
            var matchCount = 0;
            using var batch = JintRegExpEngine.BeginBatch();
            var loopTimeout = GetCustomEngineTimeout(splitter);
            while (q < size)
            {
                var found = FindRangesWithTimeout(splitter, matcher, s, q, ranges, loopTimeout);
                if (found == 0)
                {
                    break;
                }

                for (var i = 0; i < found; i++)
                {
                    if (++matchCount % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    var matchStart = ranges[i].Index;
                    if (matchStart >= size)
                    {
                        q = size;
                        break;
                    }

                    var e = System.Math.Min(matchStart + ranges[i].Length, size);
                    if (e == p)
                    {
                        // Only an empty match at p, which is the batch's last.
                        q = (int) AdvanceStringIndex(s, (ulong) matchStart, unicodeMatching);
                        break;
                    }

                    builder.Add(s.Substring(p, matchStart - p));
                    if (builder.Length >= lim)
                    {
                        return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                    }

                    p = e;
                    q = p;
                }
            }

            builder.Add(s.Substring(p));
            return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static int FindRangesWithTimeout(JsRegExp R, JintRegExpEngine engine, string s, int startIndex, Span<RegexMatchRange> ranges, TimeSpan resolvedTimeout)
    {
        if (resolvedTimeout.TotalMilliseconds <= 0 || resolvedTimeout == Timeout.InfiniteTimeSpan)
        {
            return engine.FindRanges(s, startIndex, JintRegExpEngine.NoDeadline, ranges);
        }

        try
        {
            return engine.FindRanges(s, startIndex, ComputeRegexDeadline(resolvedTimeout), ranges);
        }
        catch (OperationCanceledException)
        {
            throw new RegexMatchTimeoutException(s, R.Source ?? "", resolvedTimeout);
        }
    }

    private JsArray SplitSlow(string s, ObjectInstance splitter, bool unicodeMatching, long lim)
    {
        var builder = new JsValueListBuilder(16);
        try
        {
            ulong previousStringIndex = 0;
            ulong currentIndex = 0;
            var iterations = 0;
            while (currentIndex < (ulong) s.Length)
            {
                if (++iterations % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                splitter.Set(JsRegExp.PropertyLastIndex, currentIndex, true);
                var z = RegExpExec(splitter, s);
                if (z.IsNull())
                {
                    currentIndex = AdvanceStringIndex(s, currentIndex, unicodeMatching);
                    continue;
                }

                var endIndex = TypeConverter.ToLength(splitter.Get(JsRegExp.PropertyLastIndex));
                endIndex = System.Math.Min(endIndex, (ulong) s.Length);
                if (endIndex == previousStringIndex)
                {
                    currentIndex = AdvanceStringIndex(s, currentIndex, unicodeMatching);
                    continue;
                }

                var t = s.Substring((int) previousStringIndex, (int) (currentIndex - previousStringIndex));
                builder.Add(t);
                if (builder.Length == lim)
                {
                    return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                }

                previousStringIndex = endIndex;
                var numberOfCaptures = (int) TypeConverter.ToLength(z.Get(CommonProperties.Length));
                numberOfCaptures = System.Math.Max(numberOfCaptures - 1, 0);
                var i = 1;
                while (i <= numberOfCaptures)
                {
                    var nextCapture = z.Get(i);
                    builder.Add(nextCapture);
                    i++;
                    if (builder.Length == lim)
                    {
                        return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                    }
                }

                currentIndex = previousStringIndex;
            }

            builder.Add(s.Substring((int) previousStringIndex, s.Length - (int) previousStringIndex));
            return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
        }
    }

    private JsValue Flags(JsValue thisObject)
    {
        var r = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.flags");

        static string AddFlagIfPresent(JsValue o, JsValue p, char flag, string s)
        {
            return TypeConverter.ToBoolean(o.Get(p)) ? s + flag : s;
        }

        var result = AddFlagIfPresent(r, "hasIndices", 'd', "");
        result = AddFlagIfPresent(r, PropertyGlobal, 'g', result);
        result = AddFlagIfPresent(r, PropertyIgnoreCase, 'i', result);
        result = AddFlagIfPresent(r, PropertyMultiline, 'm', result);
        result = AddFlagIfPresent(r, PropertyDotAll, 's', result);
        result = AddFlagIfPresent(r, PropertyUnicode, 'u', result);
        result = AddFlagIfPresent(r, PropertyUnicodeSets, 'v', result);
        result = AddFlagIfPresent(r, PropertySticky, 'y', result);

        return result;
    }

    [JsFunction(Name = "toString")]
    private JsValue ToRegExpString(JsValue thisObject)
    {
        var r = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.toString");

        var pattern = TypeConverter.ToString(r.Get(PropertySource));
        var flags = TypeConverter.ToString(r.Get(PropertyFlags));

        return "/" + pattern + "/" + flags;
    }

    [JsFunction(FastCall = true)]
    private JsValue Test(JsValue thisObject, JsValue stringArg)
    {
        var r = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.test");
        var s = TypeConverter.ToString(stringArg);

        // ToLength(lastIndex) can invoke user code that recompiles the regexp. Only
        // numeric values let this fast path read the matcher before coercion.
        if (r is JsRegExp R && R.HasDefaultRegExpExec && R.Get(JsRegExp.PropertyLastIndex) is JsNumber numericLastIndex)
        {
            // Fast path for custom engine (allocation-free IsMatch)
            if (!R.UsesDotNetEngine)
            {
                var customEngine = R.Matcher!;
                if (!R.Sticky && !R.Global)
                {
                    return IsMatchWithTimeout(R, customEngine, s, 0);
                }

                if (!TryGetSearchStart(R, s, numericLastIndex, out var lastIndex))
                {
                    return JsBoolean.False;
                }

                // For global/sticky, we need the match position to update lastIndex
                if (!TryMatchRangeWithTimeout(R, customEngine, s, lastIndex, out var matchIndex, out var matchLength)
                    || (R.Sticky && matchIndex != lastIndex))
                {
                    R.Set(JsRegExp.PropertyLastIndex, 0, throwOnError: true);
                    return JsBoolean.False;
                }
                R.Set(JsRegExp.PropertyLastIndex, matchIndex + matchLength, throwOnError: true);
                return JsBoolean.True;
            }

            // Fast path for .NET Regex engine
            if (!R.FullUnicode)
            {
                if (!R.Sticky && !R.Global)
                {
                    // RegExpBuiltinExec step 10 zeroes the *local* lastIndex for a non-global,
                    // non-sticky regexp; the property itself is never written (which would raise a
                    // TypeError on a non-writable lastIndex, as exec does not).
                    return R.HostRegex!.IsMatch(s);
                }

                if (!TryGetSearchStart(R, s, numericLastIndex, out var lastIndex))
                {
                    return JsBoolean.False;
                }

                var m = R.HostRegex!.Match(s, lastIndex);
                if (!m.Success || (R.Sticky && m.Index != lastIndex))
                {
                    R.Set(JsRegExp.PropertyLastIndex, 0, throwOnError: true);
                    return JsBoolean.False;
                }
                R.Set(JsRegExp.PropertyLastIndex, m.Index + m.Length, throwOnError: true);
                return JsBoolean.True;
            }
        }

        var match = RegExpExec(r, s);
        return !match.IsNull();
    }

    /// <summary>
    /// Reads the search start for a global or sticky regexp from its <c>lastIndex</c>, applying
    /// RegExpBuiltinExec step 15.a: a <c>lastIndex</c> strictly past the end of the subject resets the
    /// property and fails the match. <c>lastIndex == length</c> is a legal start (a zero-length match can
    /// still succeed there), and the comparison happens before the narrowing cast so a value above
    /// <see cref="int.MaxValue"/> cannot wrap into a valid position.
    /// https://tc39.es/ecma262/#sec-regexpbuiltinexec
    /// </summary>
    private static bool TryGetSearchStart(JsRegExp R, string s, JsNumber numericLastIndex, out int start)
    {
        var lastIndex = TypeConverter.ToLength(numericLastIndex);
        if (lastIndex > (ulong) s.Length)
        {
            R.Set(JsRegExp.PropertyLastIndex, 0, throwOnError: true);
            start = 0;
            return false;
        }

        start = (int) lastIndex;
        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.prototype-@@search
    /// </summary>
    [JsSymbolFunction("Search", Length = 1, Flags = global::Jint.Runtime.Descriptors.PropertyFlag.Configurable | global::Jint.Runtime.Descriptors.PropertyFlag.Writable)]
    private JsValue Search(JsValue thisObject, JsValue stringArg)
    {
        var rx = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.search");

        var s = TypeConverter.ToString(stringArg);
        var previousLastIndex = rx.Get(JsRegExp.PropertyLastIndex);
        if (!SameValue(previousLastIndex, 0))
        {
            rx.Set(JsRegExp.PropertyLastIndex, 0, true);
        }

        // Global/sticky exec must perform its lastIndex writes, including throwing
        // when the property is non-writable.
        if (rx is JsRegExp { HasDefaultRegExpExec: true, UsesDotNetEngine: false, Global: false, Sticky: false } customR)
        {
            var found = TryMatchRangeWithTimeout(customR, customR.Matcher!, s, 0, out var searchIndex, out _);
            var currentLastIndex2 = rx.Get(JsRegExp.PropertyLastIndex);
            if (!SameValue(currentLastIndex2, previousLastIndex))
            {
                rx.Set(JsRegExp.PropertyLastIndex, previousLastIndex, true);
            }

            return found ? searchIndex : -1;
        }

        var result = RegExpExec(rx, s);
        var currentLastIndex = rx.Get(JsRegExp.PropertyLastIndex);
        if (!SameValue(currentLastIndex, previousLastIndex))
        {
            rx.Set(JsRegExp.PropertyLastIndex, previousLastIndex, true);
        }

        if (result.IsNull())
        {
            return -1;
        }

        return result.Get(PropertyIndex);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.prototype-@@match
    /// </summary>
    [JsSymbolFunction("Match", Length = 1, Flags = global::Jint.Runtime.Descriptors.PropertyFlag.Configurable | global::Jint.Runtime.Descriptors.PropertyFlag.Writable)]
    private JsValue Match(JsValue thisObject, JsValue stringArg)
    {
        var rx = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.match");

        var s = TypeConverter.ToString(stringArg);
        var flags = GetFlagsString(rx);
        var global = flags.Contains('g');
        if (!global)
        {
            return RegExpExec(rx, s);
        }

        var fullUnicode = flags.Contains('u') || flags.Contains('v');
        rx.Set(JsRegExp.PropertyLastIndex, JsNumber.PositiveZero, true);

        if (rx is JsRegExp rei && rei.HasDefaultRegExpExec && !rei.UsesDotNetEngine)
        {
            // fast path for custom engine: call Execute directly, skip building
            // full JS result arrays per match (saves 15-20 allocations per match)
            var customEngine = rei.Matcher!;
            var builder = new JsValueListBuilder(16);
            try
            {
                int lastIndex = 0;
                using var batch = JintRegExpEngine.BeginBatch();
                var loopTimeout = GetCustomEngineTimeout(rei);
                while (lastIndex <= s.Length)
                {
                    if (builder.Length > 0 && builder.Length % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    if (!TryMatchRangeWithTimeout(rei, customEngine, s, lastIndex, out var matchIndex, out var matchLength, loopTimeout))
                    {
                        break;
                    }

                    builder.Add(s.Substring(matchIndex, matchLength));

                    if (matchLength == 0)
                    {
                        lastIndex = (int) AdvanceStringIndex(s, (ulong) matchIndex, fullUnicode);
                    }
                    else
                    {
                        lastIndex = matchIndex + matchLength;
                    }
                }

                return builder.Length == 0 ? Null : _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
            }
            finally
            {
                builder.Dispose();
            }
        }

        if (!fullUnicode
            && rx is JsRegExp { HasDefaultRegExpExec: true, UsesDotNetEngine: true } dotnetRei
)
        {
            // fast path (only for .NET Regex engine)
            var a = _realm.Intrinsics.Array.ArrayCreate(0);

            if (dotnetRei.Sticky)
            {
                var match = dotnetRei.HostRegex!.Match(s);
                if (!match.Success || match.Index != 0)
                {
                    return Null;
                }

                uint matchCount = 0;
                while (true)
                {
                    a.SetIndexValue(matchCount, match.Value, updateLength: false);
                    matchCount++;

                    if (matchCount % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    // sticky continuation: the next match must start exactly where the previous one
                    // ended; an empty match advances by one (AdvanceStringIndex, never unicode here)
                    var expectedIndex = match.Length == 0 ? match.Index + 1 : match.Index + match.Length;
                    match = match.NextMatch();
                    if (!match.Success || match.Index != expectedIndex)
                    {
                        break;
                    }
                }

                a.SetLength(matchCount);
                return a;
            }
            else
            {
                var matches = dotnetRei.HostRegex!.Matches(s);
                if (matches.Count == 0)
                {
                    return Null;
                }

                a.EnsureCapacity((uint) matches.Count);
                a.SetLength((uint) matches.Count);
                for (var i = 0; i < matches.Count; i++)
                {
                    if (i > 0 && i % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    a.SetIndexValue((uint) i, matches[i].Value, updateLength: false);
                }
                return a;
            }
        }

        return MatchSlow(rx, s, fullUnicode);
    }

    private JsValue MatchSlow(ObjectInstance rx, string s, bool fullUnicode)
    {
        var builder = new JsValueListBuilder(16);
        try
        {
            while (true)
            {
                if (builder.Length > 0 && builder.Length % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                var result = RegExpExec(rx, s);
                if (result.IsNull())
                {
                    return builder.Length == 0 ? Null : _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
                }

                var matchStr = TypeConverter.ToString(result.Get(JsString.NumberZeroString));
                builder.Add(matchStr);
                if (matchStr == "")
                {
                    var thisIndex = TypeConverter.ToLength(rx.Get(JsRegExp.PropertyLastIndex));
                    var nextIndex = AdvanceStringIndex(s, thisIndex, fullUnicode);
                    rx.Set(JsRegExp.PropertyLastIndex, nextIndex, true);
                }
            }
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp-prototype-matchall
    /// </summary>
    [JsSymbolFunction("MatchAll", Length = 1, Flags = global::Jint.Runtime.Descriptors.PropertyFlag.Configurable | global::Jint.Runtime.Descriptors.PropertyFlag.Writable)]
    private JsValue MatchAll(JsValue thisObject, JsValue stringArg)
    {
        var r = AssertThisIsObjectInstance(thisObject, "RegExp.prototype.matchAll");

        var s = TypeConverter.ToString(stringArg);
        var c = SpeciesConstructor(r, _realm.Intrinsics.RegExp);

        var flags = TypeConverter.ToJsString(r.Get(PropertyFlags));
        var matcher = Construct(c, [
            r,
            flags
        ]);

        var lastIndex = TypeConverter.ToLength(r.Get(JsRegExp.PropertyLastIndex));
        matcher.Set(JsRegExp.PropertyLastIndex, lastIndex, true);

        var global = flags.Contains('g');
        var fullUnicode = flags.Contains('u') || flags.Contains('v');

        return _realm.Intrinsics.RegExpStringIteratorPrototype.Construct(matcher, s, global, fullUnicode);
    }

    internal static ulong AdvanceStringIndex(string s, ulong index, bool unicode)
    {
        if (!unicode || index + 1 >= (ulong) s.Length)
        {
            return index + 1;
        }

        var first = s[(int) index];
        if (first < 0xD800 || first > 0xDBFF)
        {
            return index + 1;
        }

        var second = s[(int) (index + 1)];
        if (second < 0xDC00 || second > 0xDFFF)
        {
            return index + 1;
        }

        return index + 2;
    }

    internal static JsValue RegExpExec(ObjectInstance r, string s)
    {
        var ri = r as JsRegExp;

        if ((ri is null || !ri.HasDefaultRegExpExec) && r.Get(PropertyExec) is ICallable callable)
        {
            var result = callable.Call(r, s);
            if (!result.IsNull() && !result.IsObject())
            {
                Throw.TypeError(r.Engine.Realm, "Method RegExp.prototype.exec called on incompatible receiver");
            }

            return result;
        }

        if (ri is null)
        {
            Throw.TypeError(r.Engine.Realm, "Method RegExp.prototype.exec called on incompatible receiver");
        }

        return RegExpBuiltinExec(ri, s);
    }

    /// <summary>
    /// Whether <c>RegExp.prototype.exec</c> is still this realm's built-in implementation, which is what
    /// lets <see cref="RegExpExec"/> and the fast paths skip the <c>Get(R, "exec")</c> of
    /// <see href="https://tc39.es/ecma262/#sec-regexpexec">RegExpExec</see> step 1.
    /// <para>
    /// The check reads the own descriptor rather than performing an ordinary <c>[[Get]]</c>: a script may
    /// have replaced <c>exec</c> with an accessor, and a <c>[[Get]]</c> here would call that accessor an
    /// extra time — with <c>RegExp.prototype</c> itself as the receiver rather than the regexp being
    /// matched, which is observable and simply wrong.
    /// </para>
    /// </summary>
    internal bool HasDefaultExec
    {
        get
        {
            var descriptor = GetOwnProperty(PropertyExec);
            return !descriptor.IsAccessorDescriptor()
                   && UnwrapJsValue(descriptor) is ClrFunction functionInstance
                   && functionInstance._func == _defaultExec;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexpbuiltinexec
    /// </summary>
    /// <remarks>
    /// Internal rather than private because the specification of a host API can name it directly:
    /// <c>URLPattern</c>'s match algorithm runs <c>RegExpBuiltinExec</c> on each component's regular expression,
    /// deliberately not <c>RegExpExec</c>, so a script that replaced <c>RegExp.prototype.exec</c> cannot observe
    /// or alter a <c>URLPattern</c> match.
    /// </remarks>
    internal static JsValue RegExpBuiltinExec(JsRegExp R, string s)
    {
        var length = (ulong) s.Length;
        var lastIndex = TypeConverter.ToLength(R.Get(JsRegExp.PropertyLastIndex));

        var global = R.Global;
        var sticky = R.Sticky;
        if (!global && !sticky)
        {
            lastIndex = 0;
        }

        // Stateful expressions and match indices need the full result handling below.
        if (!global && !sticky && !R.Indices
            && string.Equals(R.Source, JsRegExp.regExpForMatchingAllCharacters, StringComparison.Ordinal))
        {
            // "aaa".match() => [ '', index: 0, input: 'aaa' ]
            var array = R.Engine.Realm.Intrinsics.Array.ArrayCreate(1);
            array.DefineOwnDataPropertyUnchecked(PropertyIndex._value, lastIndex);
            array.DefineOwnDataPropertyUnchecked(PropertyInput._value, s);
            array.SetIndexValue(0, JsString.Empty, updateLength: false);
            return array;
        }

        // Every pattern written in script runs on the ECMAScript matcher; only a host-supplied Regex reaches .NET.
        if (!R.UsesDotNetEngine)
        {
            return CustomEngineBuiltinExec(R, R.Matcher!, s, lastIndex, global, sticky);
        }

        var matcher = R.HostRegex!;
        var fullUnicode = R.FullUnicode;
        var hasIndices = R.Indices;

        if (!global && !sticky && !fullUnicode && !hasIndices)
        {
            // we can the non-stateful fast path which is the common case
            var m = matcher.Match(s, (int) lastIndex);
            if (!m.Success)
            {
                return Null;
            }

            return CreateReturnValueArray(R, m, s, fullUnicode: false, hasIndices: false);
        }

        // the stateful version
        Match match;

        if (lastIndex > length)
        {
            R.Set(JsRegExp.PropertyLastIndex, JsNumber.PositiveZero, true);
            return Null;
        }

        var startAt = (int) lastIndex;
        var iterations = 0;
        while (true)
        {
            if (++iterations % ConstraintCheckInterval == 0)
            {
                R.Engine.Constraints.Check();
            }

            match = R.HostRegex!.Match(s, startAt);

            // The conversion of Unicode regex patterns to .NET Regex has some flaws:
            // when the pattern may match empty strings, the adapted Regex will return empty string matches
            // in the middle of surrogate pairs. As a best effort solution, we remove these fake positive matches.
            // (See also: https://github.com/sebastienros/esprima-dotnet/pull/364#issuecomment-1606045259)

            if (match.Success
                && fullUnicode
                && match.Length == 0
                && 0 < match.Index && match.Index < s.Length
                && char.IsHighSurrogate(s[match.Index - 1]) && char.IsLowSurrogate(s[match.Index]))
            {
                startAt++;
                continue;
            }

            break;
        }

        var success = match.Success && (!sticky || match.Index == (int) lastIndex);
        if (!success)
        {
            R.Set(JsRegExp.PropertyLastIndex, JsNumber.PositiveZero, true);
            return Null;
        }

        var e = match.Index + match.Length;

        // NOTE: Even in Unicode mode, we don't need to translate indices as .NET regexes always return code unit indices.

        if (global || sticky)
        {
            R.Set(JsRegExp.PropertyLastIndex, e, true);
        }

        return CreateReturnValueArray(R, match, s, fullUnicode, hasIndices);
    }

    /// <summary>
    /// RegExpBuiltinExec implementation for the custom regex engine.
    /// </summary>
    private static JsValue CustomEngineBuiltinExec(JsRegExp R, JintRegExpEngine customEngine, string s, ulong lastIndex, bool global, bool sticky)
    {
        var hasIndices = R.Indices;
        var length = (ulong) s.Length;

        if (lastIndex > length)
        {
            if (global || sticky)
            {
                R.Set(JsRegExp.PropertyLastIndex, JsNumber.PositiveZero, true);
            }

            return Null;
        }

        var groups = new RegexCapture[customEngine.CaptureCount];
        var success = TryMatchWithTimeout(R, customEngine, s, (int) lastIndex, groups)
                      && (!sticky || groups[0].Index == (int) lastIndex);

        if (!success)
        {
            if (global || sticky)
            {
                R.Set(JsRegExp.PropertyLastIndex, JsNumber.PositiveZero, true);
            }

            return Null;
        }

        var e = groups[0].Index + groups[0].Length;
        if (global || sticky)
        {
            R.Set(JsRegExp.PropertyLastIndex, e, true);
        }

        return CreateReturnValueArrayFromCustom(R, customEngine, groups, s, hasIndices);
    }

    /// <summary>
    /// Effective per-match timeout for a script regex: the value the pattern was compiled under,
    /// carried via <see cref="JsRegExp.ConversionOptions"/>, falling back to the engine's configured
    /// constraint where no conversion options were in force or where they chose no timeout — which is what
    /// a prepared script or module carries, having had no engine to resolve one against.
    /// </summary>
    private static TimeSpan GetCustomEngineTimeout(JsRegExp R) =>
        Options.ConstraintOptions.NormalizeRegexTimeout(
            R.ConversionOptions?.Timeout
            ?? R.Engine.Options.Constraints.RegexTimeout);

    /// <summary>
    /// Runs the matcher under the configured timeout, reporting only the match range, for the lanes that read the
    /// match position and nothing else: the matcher does no capture work and nothing is materialized. The deadline
    /// is enforced by elapsed-time checks inside the matcher rather than a thread-pool
    /// <see cref="CancellationTokenSource"/> timer, so the abort fires promptly even when the pool is saturated —
    /// mirroring how <see cref="Regex.Match(string,int)"/> enforces <see cref="Regex.MatchTimeout"/>. Throws
    /// <see cref="RegexMatchTimeoutException"/> when the timeout elapses.
    /// </summary>
    private static bool TryMatchRangeWithTimeout(JsRegExp R, JintRegExpEngine engine, string s, int startIndex, out int index, out int length, TimeSpan? resolvedTimeout = null)
    {
        // A loop resolves the timeout once and passes it; Options are frozen, so it cannot change between searches.
        var timeout = resolvedTimeout ?? GetCustomEngineTimeout(R);
        if (timeout.TotalMilliseconds <= 0 || timeout == Timeout.InfiniteTimeSpan)
        {
            return engine.TryMatchRange(s, startIndex, JintRegExpEngine.NoDeadline, out index, out length);
        }

        try
        {
            return engine.TryMatchRange(s, startIndex, ComputeRegexDeadline(timeout), out index, out length);
        }
        catch (OperationCanceledException)
        {
            throw new RegexMatchTimeoutException(s, R.Source ?? "", timeout);
        }
    }

    /// <summary>
    /// Capture-writing counterpart of <see cref="TryMatchRangeWithTimeout"/>: group spans, no strings.
    /// </summary>
    private static bool TryMatchWithTimeout(JsRegExp R, JintRegExpEngine engine, string s, int startIndex, RegexCapture[] groups, TimeSpan? resolvedTimeout = null)
    {
        // A loop resolves the timeout once and passes it; Options are frozen, so it cannot change between searches.
        var timeout = resolvedTimeout ?? GetCustomEngineTimeout(R);
        if (timeout.TotalMilliseconds <= 0 || timeout == Timeout.InfiniteTimeSpan)
        {
            return engine.TryMatch(s, startIndex, JintRegExpEngine.NoDeadline, groups);
        }

        try
        {
            return engine.TryMatch(s, startIndex, ComputeRegexDeadline(timeout), groups);
        }
        catch (OperationCanceledException)
        {
            throw new RegexMatchTimeoutException(s, R.Source ?? "", timeout);
        }
    }

    /// <summary>
    /// Non-sticky search counterpart of <see cref="TryMatchWithTimeout"/>.
    /// </summary>
    private static bool TryFindWithTimeout(JsRegExp R, JintRegExpEngine engine, string s, int startIndex, RegexCapture[] groups, TimeSpan? resolvedTimeout = null)
    {
        // A loop resolves the timeout once and passes it; Options are frozen, so it cannot change between searches.
        var timeout = resolvedTimeout ?? GetCustomEngineTimeout(R);
        if (timeout.TotalMilliseconds <= 0 || timeout == Timeout.InfiniteTimeSpan)
        {
            return engine.TryFind(s, startIndex, JintRegExpEngine.NoDeadline, groups);
        }

        try
        {
            return engine.TryFind(s, startIndex, ComputeRegexDeadline(timeout), groups);
        }
        catch (OperationCanceledException)
        {
            throw new RegexMatchTimeoutException(s, R.Source ?? "", timeout);
        }
    }

    /// <summary>
    /// IsMatch counterpart of <see cref="TryMatchRangeWithTimeout"/>.
    /// </summary>
    private static bool IsMatchWithTimeout(JsRegExp R, JintRegExpEngine engine, string s, int startIndex)
    {
        var timeout = GetCustomEngineTimeout(R);
        if (timeout.TotalMilliseconds <= 0 || timeout == Timeout.InfiniteTimeSpan)
        {
            return engine.IsMatch(s, startIndex);
        }

        try
        {
            return engine.IsMatch(s, startIndex, ComputeRegexDeadline(timeout));
        }
        catch (OperationCanceledException)
        {
            throw new RegexMatchTimeoutException(s, R.Source ?? "", timeout);
        }
    }

    /// <summary>
    /// Convert a positive, finite regex timeout into an absolute <see cref="Stopwatch.GetTimestamp"/>
    /// deadline for the custom engine's inline interrupt checks. Saturates to <see cref="JintRegExpEngine.NoDeadline"/>
    /// (never fires) instead of overflowing for extreme timeout values.
    /// </summary>
    private static long ComputeRegexDeadline(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            // Untimed, which is what Regex.InfiniteMatchTimeout means and what a normalized timeout uses
            // to say it. Without this the negative interval would put the deadline in the past and every
            // match would report a timeout immediately.
            return JintRegExpEngine.NoDeadline;
        }

        var now = JintRegExpEngine.RecentTimestamp();
        var ticks = timeout.TotalSeconds * Stopwatch.Frequency;
        return ticks >= long.MaxValue - now ? JintRegExpEngine.NoDeadline : now + (long) ticks;
    }

    /// <summary>
    /// Build the JS result array from a custom engine match result.
    /// </summary>
    /// <summary>
    /// Steps 20-36 of https://tc39.es/ecma262/#sec-regexpbuiltinexec over the matcher's capture spans.
    /// </summary>
    private static JsArray CreateReturnValueArrayFromCustom(
        JsRegExp rei,
        JintRegExpEngine matcher,
        RegexCapture[] groups,
        string s,
        bool hasIndices)
    {
        var engine = rei.Engine;
        var actualGroupCount = groups.Length;
        var array = engine.Realm.Intrinsics.Array.ArrayCreate((ulong) actualGroupCount);
        array.CreateDataProperty(PropertyIndex, groups[0].Index);
        array.CreateDataProperty(PropertyInput, s);

        ObjectInstance? jsGroups = null;
        List<string?>? groupNames = null;
        var indices = hasIndices ? new List<JsNumber[]?>(actualGroupCount) : null;

        var hasAnyGroupName = matcher.HasGroupNames;
        if (hasAnyGroupName || hasIndices)
        {
            // Pre-initialize the groups object, so every name is present (undefined when its group did not
            // participate) in the order the names first appear.
            for (var i = 1; i < actualGroupCount; i++)
            {
                var groupName = matcher.GetGroupName(i);
                if (groupName is not null)
                {
                    jsGroups ??= OrdinaryObjectCreate(engine, null);
                    if (!jsGroups.HasOwnProperty(groupName))
                    {
                        jsGroups.CreateDataPropertyOrThrow(groupName, Undefined);
                    }
                }

                if (hasIndices)
                {
                    groupNames ??= [];
                    groupNames.Add(groupName);
                }
            }
        }

        for (var i = 0; i < actualGroupCount; i++)
        {
            var capture = groups[i];
            // A slice of the subject rather than a copy, as the .NET lane does: a match spanning most of a large
            // subject would otherwise copy the subject on every exec.
            JsValue capturedValue = capture.Participated ? JsString.CreateSliced(s, capture.Index, capture.Length) : Undefined;

            if (hasIndices)
            {
                indices!.Add(capture.Participated
                    ? [JsNumber.Create(capture.Index), JsNumber.Create(capture.Index + capture.Length)]
                    : null);
            }

            if (i > 0 && capture.Participated && matcher.GetGroupName(i) is { } name)
            {
                jsGroups!.CreateDataPropertyOrThrow(name, capturedValue);
            }

            array.SetIndexValue((uint) i, capturedValue, updateLength: false);
        }

        array.CreateDataProperty(PropertyGroups, jsGroups ?? Undefined);

        if (hasIndices)
        {
            var indicesArray = MakeMatchIndicesIndexPairArray(engine, s, indices!, groupNames, hasAnyGroupName);
            array.CreateDataPropertyOrThrow("indices", indicesArray);
        }

        // Update the legacy RegExp static properties (RegExp Legacy Features proposal, not Annex B)
        var constructor = engine.Realm.Intrinsics.RegExp;
        constructor._legacyInput = s;
        // lastMatch/leftContext/rightContext are all derived from this position on read.
        constructor.SetLegacyContext(s, groups[0].Index, groups[0].Length);
        var lastParen = "";
        for (var i = 0; i < 9; i++)
        {
            var group = i + 1;
            if (group < actualGroupCount && groups[group].Participated)
            {
                var groupValue = s.Substring(groups[group].Index, groups[group].Length);
                constructor._legacyParens[i] = groupValue;
                lastParen = groupValue;
            }
            else
            {
                constructor._legacyParens[i] = "";
            }
        }

        constructor._legacyLastParen = lastParen;
        return array;
    }

    private static JsArray CreateReturnValueArray(
        JsRegExp rei,
        Match match,
        string s,
        bool fullUnicode,
        bool hasIndices)
    {
        var engine = rei.Engine;
        var actualGroupCount = GetActualRegexGroupCount(rei, match);
        var array = engine.Realm.Intrinsics.Array.ArrayCreate((ulong) actualGroupCount);
        array.CreateDataProperty(PropertyIndex, match.Index);
        array.CreateDataProperty(PropertyInput, s);

        ObjectInstance? groups = null;
        List<string?>? groupNames = null;
        var indices = hasIndices ? new List<JsNumber[]?>(actualGroupCount) : null;

        // Pre-initialize groups object with all unique names in source order.
        // This ensures correct property ordering and that duplicate names don't
        // overwrite a successful capture with undefined from a non-participating group.
        var hasAnyGroupName = false;
        for (uint i = 1; i < actualGroupCount; i++)
        {
            var groupName = GetRegexGroupName(rei, (int) i);
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                hasAnyGroupName = true;
                groups ??= OrdinaryObjectCreate(engine, null);
                if (!groups.HasOwnProperty(groupName))
                {
                    groups.CreateDataPropertyOrThrow(groupName, Undefined);
                }
            }

            if (hasIndices)
            {
                groupNames ??= [];
                groupNames.Add(groupName);
            }
        }

        for (uint i = 0; i < actualGroupCount; i++)
        {
            var capture = match.Groups[(int) i];
            var capturedValue = Undefined;
            if (capture?.Success == true)
            {
                // Capture.Value would copy the captured span out of the subject. CreateSliced lets
                // its retention policy decide: a capture covering most of a large subject becomes a
                // zero-copy view, while short captures still copy so they cannot pin the subject.
                capturedValue = JsString.CreateSliced(s, capture.Index, capture.Length);
            }

            if (hasIndices)
            {
                if (capture?.Success == true)
                {
                    indices!.Add([JsNumber.Create(capture.Index), JsNumber.Create(capture.Index + capture.Length)]);
                }
                else
                {
                    indices!.Add(null);
                }
            }

            if (i > 0)
            {
                var groupName = GetRegexGroupName(rei, (int) i);
                if (!string.IsNullOrWhiteSpace(groupName) && capture?.Success == true)
                {
                    groups!.CreateDataPropertyOrThrow(groupName, capturedValue);
                }
            }

            array.SetIndexValue(i, capturedValue, updateLength: false);
        }

        array.CreateDataProperty(PropertyGroups, groups ?? Undefined);

        if (hasIndices)
        {
            var indicesArray = MakeMatchIndicesIndexPairArray(engine, s, indices!, groupNames, hasAnyGroupName);
            array.CreateDataPropertyOrThrow("indices", indicesArray);
        }

        // Update the legacy RegExp static properties (RegExp Legacy Features proposal, not Annex B)
        UpdateLegacyStaticProperties(engine, match, s, actualGroupCount);

        return array;
    }

    /// <summary>
    /// Updates the RegExp legacy static properties after a successful match. These come from the TC39
    /// "RegExp Legacy Features" proposal (https://github.com/tc39/proposal-regexp-legacy-features),
    /// not from ECMA-262 Annex B.
    /// </summary>
    private static void UpdateLegacyStaticProperties(Engine engine, Match match, string s, int actualGroupCount)
    {
        var constructor = engine.Realm.Intrinsics.RegExp;
        constructor._legacyInput = s;
        // lastMatch/leftContext/rightContext are all derived from this position on read.
        constructor.SetLegacyContext(s, match.Index, match.Length);

        // Update $1-$9
        var lastParen = "";
        for (var i = 0; i < 9; i++)
        {
            var groupIndex = i + 1;
            if (groupIndex < actualGroupCount && match.Groups[groupIndex].Success)
            {
                // Capture.Value builds a fresh string on every read, so take it once.
                var groupValue = match.Groups[groupIndex].Value;
                constructor._legacyParens[i] = groupValue;
                lastParen = groupValue;
            }
            else
            {
                constructor._legacyParens[i] = "";
            }
        }
        constructor._legacyLastParen = lastParen;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-makematchindicesindexpairarray
    /// </summary>
    private static JsArray MakeMatchIndicesIndexPairArray(
        Engine engine,
        string s,
        List<JsNumber[]?> indices,
        List<string?>? groupNames,
        bool hasGroups)
    {
        var n = indices.Count;
        var a = engine.Realm.Intrinsics.Array.Construct((uint) n);
        ObjectInstance? groups = null;
        if (hasGroups)
        {
            groups = OrdinaryObjectCreate(engine, null);

            // Pre-initialize all unique group names with undefined for correct property ordering
            foreach (var name in groupNames!)
            {
                if (!string.IsNullOrWhiteSpace(name) && !groups.HasOwnProperty(name))
                {
                    groups.CreateDataPropertyOrThrow(name, Undefined);
                }
            }
        }

        a.CreateDataPropertyOrThrow("groups", groups ?? Undefined);
        for (var i = 0; i < n; ++i)
        {
            var matchIndices = indices[i];

            var matchIndexPair = matchIndices is not null
                ? GetMatchIndexPair(engine, s, matchIndices)
                : Undefined;

            a.Push(matchIndexPair);
            if (i > 0 && !string.IsNullOrWhiteSpace(groupNames?[i - 1]))
            {
                // For duplicate group names, only update if this group actually matched
                if (matchIndices is not null)
                {
                    groups!.CreateDataPropertyOrThrow(groupNames![i - 1], matchIndexPair);
                }
            }
        }
        return a;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmatchindexpair
    /// </summary>
    private static JsValue GetMatchIndexPair(Engine engine, string s, JsNumber[] match)
    {
        return engine.Realm.Intrinsics.Array.CreateArrayFromList(match);
    }

    // The helpers below describe a host-supplied Regex (the only kind that reaches the .NET lanes), whose groups
    // are described by the Regex itself.
    private static int GetActualRegexGroupCount(JsRegExp rei, Match match) => match.Groups.Count;

    /// <summary>
    /// The group count of a .NET-engine regex, without needing a <see cref="Match"/> to ask. A result
    /// of 1 means group 0 (the whole match) only, i.e. the pattern has no capture groups.
    /// </summary>
    private static int GetRegexGroupCount(JsRegExp rei) => rei.HostRegex!.GetGroupNumbers().Length;

    private static string? GetRegexGroupName(JsRegExp rei, int index)
    {
        if (index == 0)
        {
            return null;
        }

        var regex = rei.HostRegex!;
        var groupNameFromNumber = regex.GroupNameFromNumber(index);
        if (string.Equals(groupNameFromNumber, index.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            // regex defaults to index as group name when it's not a named group
            return null;
        }

        return groupNameFromNumber;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.prototype.compile
    /// B.2.5.1
    /// </summary>
    [JsFunction]
    private JsValue Compile(JsValue thisObject, JsValue pattern, JsValue flags)
    {
        // 1. Let O be the this value.
        // 2. Perform ? RequireInternalSlot(O, [[RegExpMatcher]]).
        var r = thisObject as JsRegExp;
        if (r is null)
        {
            Throw.TypeError(_realm, "Method RegExp.prototype.compile called on incompatible receiver");
            return default!;
        }

        // 3. If SameValue(O.[[Prototype]], %RegExp%.prototype) is false, throw a TypeError.
        // This rejects subclass instances and cross-realm instances.
        if (!ReferenceEquals(r.Prototype, _realm.Intrinsics.RegExp.PrototypeObject))
        {
            Throw.TypeError(_realm, "RegExp.prototype.compile cannot be used on RegExp subclass or cross-realm instances");
            return default!;
        }

        JsValue p;
        JsValue f;
        if (pattern is JsRegExp regExpPattern)
        {
            if (!flags.IsUndefined())
            {
                Throw.TypeError(_realm, "Cannot supply flags when constructing one RegExp from another");
            }
            p = regExpPattern.Source;
            f = regExpPattern.Flags;
        }
        else
        {
            p = pattern;
            f = flags;
        }

        // RegExpInitialize updates source/flags first, then sets lastIndex = 0.
        // For compile, we use Set(O, "lastIndex", +0𝔽, true) which throws TypeError
        // if lastIndex is non-writable (source/flags are already updated at that point).
        return _constructor.RegExpInitialize(r, p, f, throwOnLastIndex: true);
    }

    // Not [JsFunction] — registered manually in AddRegExpAccessors so HasDefaultExec's
    // ClrFunction-identity check (RegExpPrototype.cs HasDefaultExec) keeps working.
    private JsValue Exec(JsValue thisObject, JsCallArguments arguments)
    {
        var r = thisObject as JsRegExp;
        if (r is null)
        {
            Throw.TypeError(_engine.Realm, "Method RegExp.prototype.exec called on incompatible receiver");
        }

        var s = TypeConverter.ToString(arguments.At(0));
        return RegExpBuiltinExec(r, s);
    }
}
