using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp
{
    public sealed class RegExpPrototype : Prototype
    {
        private static readonly JsString PropertyExec = new("exec");
        private static readonly JsString PropertyIndex = new("index");
        private static readonly JsString PropertyInput = new("input");
        private static readonly JsString PropertySticky = new("sticky");
        private static readonly JsString PropertyGlobal = new("global");
        internal static readonly JsString PropertySource = new("source");
        private static readonly JsString DefaultSource = new("(?:)");
        internal static readonly JsString PropertyFlags = new("flags");
        private static readonly JsString PropertyGroups = new("groups");

        private readonly RegExpConstructor _constructor;
        private readonly Func<JsValue, JsValue[], JsValue> _defaultExec;

        internal RegExpPrototype(
            Engine engine,
            Realm realm,
            RegExpConstructor constructor,
            ObjectPrototype objectPrototype) : base(engine, realm)
        {
            _defaultExec = Exec;
            _constructor = constructor;
            _prototype = objectPrototype;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;

            GetSetPropertyDescriptor CreateGetAccessorDescriptor(string name, Func<RegExpInstance, JsValue> valueExtractor, JsValue? protoValue = null)
            {
                return new GetSetPropertyDescriptor(
                    get: new ClrFunctionInstance(Engine, name, (thisObj, arguments) =>
                    {
                        if (ReferenceEquals(thisObj, this))
                        {
                            return protoValue ?? Undefined;
                        }

                        var r = thisObj as RegExpInstance;
                        if (r is null)
                        {
                            ExceptionHelper.ThrowTypeError(_realm);
                        }

                        return valueExtractor(r);
                    }, 0, lengthFlags),
                    set: Undefined,
                    flags: PropertyFlag.Configurable);
            }

            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(14, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, propertyFlags),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToRegExpString, 0, lengthFlags), propertyFlags),
                ["exec"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "exec", _defaultExec, 1, lengthFlags), propertyFlags),
                ["test"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "test", Test, 1, lengthFlags), propertyFlags),
                ["dotAll"] = CreateGetAccessorDescriptor("get dotAll", static r => r.DotAll),
                ["flags"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get flags", Flags, 0, lengthFlags), set: Undefined, flags: PropertyFlag.Configurable),
                ["global"] = CreateGetAccessorDescriptor("get global", static r => r.Global),
                ["hasIndices"] = CreateGetAccessorDescriptor("get hasIndices", static r => r.Indices),
                ["ignoreCase"] = CreateGetAccessorDescriptor("get ignoreCase", static r => r.IgnoreCase),
                ["multiline"] = CreateGetAccessorDescriptor("get multiline", static r => r.Multiline),
                ["source"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get source", Source, 0, lengthFlags), set: Undefined, flags: PropertyFlag.Configurable),
                ["sticky"] = CreateGetAccessorDescriptor("get sticky", static r => r.Sticky),
                ["unicode"] = CreateGetAccessorDescriptor("get unicode", static r => r.FullUnicode),
                ["unicodeSets"] = CreateGetAccessorDescriptor("get unicodeSets", static r => r.UnicodeSets)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(5)
            {
                [GlobalSymbolRegistry.Match] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.match]", Match, 1, lengthFlags), propertyFlags),
                [GlobalSymbolRegistry.MatchAll] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.matchAll]", MatchAll, 1, lengthFlags), propertyFlags),
                [GlobalSymbolRegistry.Replace] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.replace]", Replace, 2, lengthFlags), propertyFlags),
                [GlobalSymbolRegistry.Search] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.search]", Search, 1, lengthFlags), propertyFlags),
                [GlobalSymbolRegistry.Split] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.split]", Split, 2, lengthFlags), propertyFlags)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-regexp.prototype.source
        /// </summary>
        private JsValue Source(JsValue thisObj, JsValue[] arguments)
        {
            if (ReferenceEquals(thisObj, this))
            {
                return DefaultSource;
            }

            var r = thisObj as RegExpInstance;
            if (r is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            if (r.Source is null)
            {
                return JsString.Empty;
            }

            return r.Source.Replace("/", "\\/").Replace("\n", "\\n");
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp.prototype-@@replace
        /// </summary>
        private JsValue Replace(JsValue thisObj, JsValue[] arguments)
        {
            var rx = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.replace");
            var s = TypeConverter.ToString(arguments.At(0));
            var lengthS = s.Length;
            var replaceValue = arguments.At(1);
            var functionalReplace = replaceValue is ICallable;

            // we need heavier logic if we have named captures
            var mayHaveNamedCaptures = false;
            if (!functionalReplace)
            {
                var value = TypeConverter.ToString(replaceValue);
                replaceValue = value;
                mayHaveNamedCaptures = value.IndexOf('$') != -1;
            }

            var flags = TypeConverter.ToString(rx.Get(PropertyFlags));
            var global = flags.IndexOf('g') != -1;

            var fullUnicode = false;

            if (global)
            {
                fullUnicode = flags.IndexOf('u') != -1;
                rx.Set(RegExpInstance.PropertyLastIndex, 0, true);
            }

            // check if we can access fast path
            if (!fullUnicode
                && !mayHaveNamedCaptures
                && !TypeConverter.ToBoolean(rx.Get(PropertySticky))
                && rx is RegExpInstance rei && rei.TryGetDefaultRegExpExec(out _))
            {
                var count = global ? int.MaxValue : 1;

                string result;
                if (functionalReplace)
                {
                    string Evaluator(Match match)
                    {
                        var replacerArgs = new List<JsValue>(match.Groups.Count + 2);
                        replacerArgs.Add(match.Value);

                        ObjectInstance? groups = null;
                        for (var i = 1; i < match.Groups.Count; i++)
                        {
                            var capture = match.Groups[i];
                            replacerArgs.Add(capture.Success ? capture.Value : Undefined);

                            var groupName = GetRegexGroupName(rei.Value, i);
                            if (!string.IsNullOrWhiteSpace(groupName))
                            {
                                groups ??= OrdinaryObjectCreate(_engine, null);
                                groups.CreateDataPropertyOrThrow(groupName, capture.Success ? capture.Value : Undefined);
                            }
                        }

                        replacerArgs.Add(match.Index);
                        replacerArgs.Add(s);
                        if (groups is not null)
                        {
                            replacerArgs.Add(groups);
                        }

                        return CallFunctionalReplace(replaceValue, replacerArgs);
                    }

                    result = rei.Value.Replace(s, Evaluator, count);
                }
                else
                {
                    result = rei.Value.Replace(s, TypeConverter.ToString(replaceValue), count);
                }

                rx.Set(RegExpInstance.PropertyLastIndex, JsNumber.PositiveZero);
                return result;
            }

            var results = new List<ObjectInstance>();

            while (true)
            {
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
                    var thisIndex = TypeConverter.ToLength(rx.Get(RegExpInstance.PropertyLastIndex));
                    var nextIndex = AdvanceStringIndex(s, thisIndex, fullUnicode);
                    rx.Set(RegExpInstance.PropertyLastIndex, nextIndex);
                }
            }

            var accumulatedResult = "";
            var nextSourcePosition = 0;

            var captures = new List<string>();
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var nCaptures = (int) result.Length;
                nCaptures = System.Math.Max(nCaptures - 1, 0);
                var matched = TypeConverter.ToString(result.Get(0));
                var matchLength = matched.Length;
                var position = (int) TypeConverter.ToInteger(result.Get(PropertyIndex));
                position = System.Math.Max(System.Math.Min(position, lengthS), 0);
                uint n = 1;

                captures.Clear();
                while (n <= nCaptures)
                {
                    var capN = result.Get(n);
                    var value = !capN.IsUndefined() ? TypeConverter.ToString(capN) : "";
                    captures.Add(value);
                    n++;
                }

                var namedCaptures = result.Get(PropertyGroups);
                string replacement;
                if (functionalReplace)
                {
                    var replacerArgs = new List<JsValue>();
                    replacerArgs.Add(matched);
                    foreach (var capture in captures)
                    {
                        replacerArgs.Add(capture);
                    }

                    replacerArgs.Add(position);
                    replacerArgs.Add(s);
                    if (!namedCaptures.IsUndefined())
                    {
                        replacerArgs.Add(namedCaptures);
                    }

                    replacement = CallFunctionalReplace(replaceValue, replacerArgs);
                }
                else
                {
                    if (!namedCaptures.IsUndefined())
                    {
                        namedCaptures = TypeConverter.ToObject(_realm, namedCaptures);
                    }

                    replacement = GetSubstitution(matched, s, position, captures.ToArray(), namedCaptures, TypeConverter.ToString(replaceValue));
                }

                if (position >= nextSourcePosition)
                {
                    accumulatedResult = accumulatedResult +
                                        s.Substring(nextSourcePosition, position - nextSourcePosition) +
                                        replacement;

                    nextSourcePosition = position + matchLength;
                }
            }

            if (nextSourcePosition >= lengthS)
            {
                return accumulatedResult;
            }

            return accumulatedResult + s.Substring(nextSourcePosition);
        }

        private static string CallFunctionalReplace(JsValue replacer, List<JsValue> replacerArgs)
        {
            var result = ((ICallable) replacer).Call(Undefined, replacerArgs.ToArray());
            return TypeConverter.ToString(result);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getsubstitution
        /// </summary>
        internal static string GetSubstitution(
            string matched,
            string str,
            int position,
            string[] captures,
            JsValue namedCaptures,
            string replacement)
        {
            // If there is no pattern, replace the pattern as is.
            if (replacement.IndexOf('$') < 0)
            {
                return replacement;
            }

            // Patterns
            // $$	Inserts a "$".
            // $&	Inserts the matched substring.
            // $`	Inserts the portion of the string that precedes the matched substring.
            // $'	Inserts the portion of the string that follows the matched substring.
            // $n or $nn	Where n or nn are decimal digits, inserts the nth parenthesized submatch string, provided the first argument was a RegExp object.
            using var replacementBuilder = StringBuilderPool.Rent();
            var sb = replacementBuilder.Builder;
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
                            sb.Append(matched);
                            break;
                        case '`':
                            sb.Append(str.Substring(0, position));
                            break;
                        case '\'':
                            sb.Append(str.Substring(position + matched.Length));
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
                                    sb.Append(TypeConverter.ToString(capture));
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
                                        sb.Append(TypeConverter.ToString(captures[matchNumber2 - 1]));
                                        i++;
                                    }
                                    else if (matchNumber1 > 0 && matchNumber1 <= captures.Length)
                                    {
                                        // Single digit capture replacement.
                                        sb.Append(TypeConverter.ToString(captures[matchNumber1 - 1]));
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

            return replacementBuilder.ToString();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp.prototype-@@split
        /// </summary>
        private JsValue Split(JsValue thisObj, JsValue[] arguments)
        {
            var rx = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.split");
            var s = TypeConverter.ToString(arguments.At(0));
            var limit = arguments.At(1);
            var c = SpeciesConstructor(rx, _realm.Intrinsics.RegExp);
            var flags = TypeConverter.ToJsString(rx.Get(PropertyFlags));
            var unicodeMatching = flags.IndexOf('u') > -1;
            var newFlags = flags.IndexOf('y') > -1 ? flags : new JsString(flags.ToString() + 'y');
            var splitter = Construct(c, new JsValue[]
            {
                rx,
                newFlags
            });
            uint lengthA = 0;
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

            if (!unicodeMatching && rx is RegExpInstance R && R.TryGetDefaultRegExpExec(out _))
            {
                // we can take faster path

                if (R.Source == RegExpInstance.regExpForMatchingAllCharacters)
                {
                    // if empty string, just a string split
                    return StringPrototype.SplitWithStringSeparator(_realm, "", s, (uint) s.Length);
                }

                var a = (ArrayInstance) _realm.Intrinsics.Array.Construct(Arguments.Empty);
                var match = R.Value.Match(s, 0);

                if (!match.Success) // No match at all return the string in an array
                {
                    a.SetIndexValue(0, s, updateLength: true);
                    return a;
                }

                int lastIndex = 0;
                uint index = 0;
                while (match.Success && index < lim)
                {
                    if (match.Length == 0 && (match.Index == 0 || match.Index == s.Length || match.Index == lastIndex))
                    {
                        match = match.NextMatch();
                        continue;
                    }

                    // Add the match results to the array.
                    a.SetIndexValue(index++, s.Substring(lastIndex, match.Index - lastIndex), updateLength: true);

                    if (index >= lim)
                    {
                        return a;
                    }

                    lastIndex = match.Index + match.Length;
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        var group = match.Groups[i];
                        var item = Undefined;
                        if (group.Captures.Count > 0)
                        {
                            item = match.Groups[i].Value;
                        }

                        a.SetIndexValue(index++, item, updateLength: true);

                        if (index >= lim)
                        {
                            return a;
                        }
                    }

                    match = match.NextMatch();
                    if (!match.Success) // Add the last part of the split
                    {
                        a.SetIndexValue(index++, s.Substring(lastIndex), updateLength: true);
                    }
                }

                return a;
            }

            return SplitSlow(s, splitter, unicodeMatching, lengthA, lim);
        }

        private JsValue SplitSlow(string s, ObjectInstance splitter, bool unicodeMatching, uint lengthA, long lim)
        {
            var a = _realm.Intrinsics.Array.ArrayCreate(0);
            ulong previousStringIndex = 0;
            ulong currentIndex = 0;
            while (currentIndex < (ulong) s.Length)
            {
                splitter.Set(RegExpInstance.PropertyLastIndex, currentIndex, true);
                var z = RegExpExec(splitter, s);
                if (z.IsNull())
                {
                    currentIndex = AdvanceStringIndex(s, currentIndex, unicodeMatching);
                    continue;
                }

                var endIndex = TypeConverter.ToLength(splitter.Get(RegExpInstance.PropertyLastIndex));
                endIndex = System.Math.Min(endIndex, (ulong) s.Length);
                if (endIndex == previousStringIndex)
                {
                    currentIndex = AdvanceStringIndex(s, currentIndex, unicodeMatching);
                    continue;
                }

                var t = s.Substring((int) previousStringIndex, (int) (currentIndex - previousStringIndex));
                a.SetIndexValue(lengthA, t, updateLength: true);
                lengthA++;
                if (lengthA == lim)
                {
                    return a;
                }

                previousStringIndex = endIndex;
                var numberOfCaptures = (int) TypeConverter.ToLength(z.Get(CommonProperties.Length));
                numberOfCaptures = System.Math.Max(numberOfCaptures - 1, 0);
                var i = 1;
                while (i <= numberOfCaptures)
                {
                    var nextCapture = z.Get(i);
                    a.SetIndexValue(lengthA, nextCapture, updateLength: true);
                    i++;
                    lengthA++;
                    if (lengthA == lim)
                    {
                        return a;
                    }
                }

                currentIndex = previousStringIndex;
            }

            a.SetIndexValue(lengthA, s.Substring((int) previousStringIndex, s.Length - (int) previousStringIndex), updateLength: true);
            return a;
        }

        private JsValue Flags(JsValue thisObj, JsValue[] arguments)
        {
            var r = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.flags");

            static string AddFlagIfPresent(JsValue o, JsValue p, char flag, string s)
            {
                return TypeConverter.ToBoolean(o.Get(p)) ? s + flag : s;
            }

            var result = AddFlagIfPresent(r, "hasIndices", 'd', "");
            result = AddFlagIfPresent(r, PropertyGlobal, 'g', result);
            result = AddFlagIfPresent(r, "ignoreCase", 'i', result);
            result = AddFlagIfPresent(r, "multiline", 'm', result);
            result = AddFlagIfPresent(r, "dotAll", 's', result);
            result = AddFlagIfPresent(r, "unicode", 'u', result);
            result = AddFlagIfPresent(r, "unicodeSets", 'v', result);
            result = AddFlagIfPresent(r, PropertySticky, 'y', result);

            return result;
        }

        private JsValue ToRegExpString(JsValue thisObj, JsValue[] arguments)
        {
            var r = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.toString");

            var pattern = TypeConverter.ToString(r.Get(PropertySource));
            var flags = TypeConverter.ToString(r.Get(PropertyFlags));

            return "/" + pattern + "/" + flags;
        }

        private JsValue Test(JsValue thisObj, JsValue[] arguments)
        {
            var r = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.test");
            var s = TypeConverter.ToString(arguments.At(0));

            // check couple fast paths
            if (r is RegExpInstance R && !R.FullUnicode)
            {
                if (!R.Sticky && !R.Global)
                {
                    R.Set(RegExpInstance.PropertyLastIndex, 0, throwOnError: true);
                    return R.Value.IsMatch(s);
                }

                var lastIndex = (int) TypeConverter.ToLength(R.Get(RegExpInstance.PropertyLastIndex));
                if (lastIndex >= s.Length && s.Length > 0)
                {
                    return JsBoolean.False;
                }

                var m = R.Value.Match(s, lastIndex);
                if (!m.Success || (R.Sticky && m.Index != lastIndex))
                {
                    R.Set(RegExpInstance.PropertyLastIndex, 0, throwOnError: true);
                    return JsBoolean.False;
                }
                R.Set(RegExpInstance.PropertyLastIndex, m.Index + m.Length, throwOnError: true);
                return JsBoolean.True;
            }

            var match = RegExpExec(r, s);
            return !match.IsNull();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp.prototype-@@search
        /// </summary>
        private JsValue Search(JsValue thisObj, JsValue[] arguments)
        {
            var rx = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.search");

            var s = TypeConverter.ToString(arguments.At(0));
            var previousLastIndex = rx.Get(RegExpInstance.PropertyLastIndex);
            if (!SameValue(previousLastIndex, 0))
            {
                rx.Set(RegExpInstance.PropertyLastIndex, 0, true);
            }

            var result = RegExpExec(rx, s);
            var currentLastIndex = rx.Get(RegExpInstance.PropertyLastIndex);
            if (!SameValue(currentLastIndex, previousLastIndex))
            {
                rx.Set(RegExpInstance.PropertyLastIndex, previousLastIndex, true);
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
        private JsValue Match(JsValue thisObj, JsValue[] arguments)
        {
            var rx = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.match");

            var s = TypeConverter.ToString(arguments.At(0));
            var flags = TypeConverter.ToString(rx.Get(PropertyFlags));
            var global = flags.IndexOf('g') != -1;
            if (!global)
            {
                return RegExpExec(rx, s);
            }

            var fullUnicode = flags.IndexOf('u') != -1;
            rx.Set(RegExpInstance.PropertyLastIndex, JsNumber.PositiveZero, true);

            if (!fullUnicode
                && rx is RegExpInstance rei
                && rei.TryGetDefaultRegExpExec(out _))
            {
                // fast path
                var a = _realm.Intrinsics.Array.ArrayCreate(0);

                if (rei.Sticky)
                {
                    var match = rei.Value.Match(s);
                    if (!match.Success || match.Index != 0)
                    {
                        return Null;
                    }

                    a.SetIndexValue(0, match.Value, updateLength: false);
                    uint li = 0;
                    while (true)
                    {
                        match = match.NextMatch();
                        if (!match.Success || match.Index != ++li)
                            break;
                        a.SetIndexValue(li,  match.Value, updateLength: false);
                    }
                    a.SetLength(li);
                    return a;
                }
                else
                {
                    var matches = rei.Value.Matches(s);
                    if (matches.Count == 0)
                    {
                        return Null;
                    }

                    a.EnsureCapacity((uint) matches.Count);
                    a.SetLength((uint) matches.Count);
                    for (var i = 0; i < matches.Count; i++)
                    {
                        a.SetIndexValue((uint) i, matches[i].Value, updateLength: false);
                    }
                    return a;
                }
            }

            return MatchSlow(rx, s, fullUnicode);
        }

        private JsValue MatchSlow(ObjectInstance rx, string s, bool fullUnicode)
        {
            var a = _realm.Intrinsics.Array.ArrayCreate(0);
            uint n = 0;
            while (true)
            {
                var result = RegExpExec(rx, s);
                if (result.IsNull())
                {
                    a.SetLength(n);
                    return n == 0 ? Null : a;
                }

                var matchStr = TypeConverter.ToString(result.Get(JsString.NumberZeroString));
                a.SetIndexValue(n, matchStr, updateLength: false);
                if (matchStr == "")
                {
                    var thisIndex = TypeConverter.ToLength(rx.Get(RegExpInstance.PropertyLastIndex));
                    var nextIndex = AdvanceStringIndex(s, thisIndex, fullUnicode);
                    rx.Set(RegExpInstance.PropertyLastIndex, nextIndex, true);
                }

                n++;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp-prototype-matchall
        /// </summary>
        private JsValue MatchAll(JsValue thisObj, JsValue[] arguments)
        {
            var r = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.matchAll");

            var s = TypeConverter.ToString(arguments.At(0));
            var c = SpeciesConstructor(r, _realm.Intrinsics.RegExp);

            var flags = TypeConverter.ToJsString(r.Get(PropertyFlags));
            var matcher = Construct(c, new JsValue[]
            {
                r,
                flags
            });

            var lastIndex = TypeConverter.ToLength(r.Get(RegExpInstance.PropertyLastIndex));
            matcher.Set(RegExpInstance.PropertyLastIndex, lastIndex, true);

            var global = flags.IndexOf('g') != -1;
            var fullUnicode = flags.IndexOf('u') != -1;

            return _realm.Intrinsics.RegExpStringIteratorPrototype.Construct(matcher, s, global, fullUnicode);
        }

        private static ulong AdvanceStringIndex(string s, ulong index, bool unicode)
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
            var exec = r.Get(PropertyExec);
            if (exec is ICallable callable)
            {
                var result = callable.Call(r, new JsValue[]  { s });
                if (!result.IsNull() && !result.IsObject())
                {
                    ExceptionHelper.ThrowTypeError(r.Engine.Realm);
                }

                return result;
            }

            var ri = r as RegExpInstance;
            if (ri is null)
            {
                ExceptionHelper.ThrowTypeError(r.Engine.Realm);
            }

            return RegExpBuiltinExec(ri, s);
        }

        internal bool TryGetDefaultExec(ObjectInstance o, [NotNullWhen((true))] out Func<JsValue, JsValue[], JsValue>? exec)
        {
            if (o.Get(PropertyExec) is ClrFunctionInstance functionInstance && functionInstance._func == _defaultExec)
            {
                exec = _defaultExec;
                return true;
            }

            exec = default;
            return false;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexpbuiltinexec
        /// </summary>
        private static JsValue RegExpBuiltinExec(RegExpInstance R, string s)
        {
            var length = (ulong) s.Length;
            var lastIndex = TypeConverter.ToLength(R.Get(RegExpInstance.PropertyLastIndex));

            var global = R.Global;
            var sticky = R.Sticky;
            if (!global && !sticky)
            {
                lastIndex = 0;
            }

            if (R.Source == RegExpInstance.regExpForMatchingAllCharacters)  // Reg Exp is really ""
            {
                if (lastIndex > (ulong) s.Length)
                {
                    return Null;
                }

                // "aaa".match() => [ '', index: 0, input: 'aaa' ]
                var array = R.Engine.Realm.Intrinsics.Array.ArrayCreate(1);
                array.FastAddProperty(PropertyIndex, lastIndex, true, true, true);
                array.FastAddProperty(PropertyInput, s, true, true, true);
                array.SetIndexValue(0, JsString.Empty, updateLength: false);
                return array;
            }

            var matcher = R.Value;
            var fullUnicode = R.FullUnicode;
            var hasIndices = R.Indices;

            if (!global & !sticky && !fullUnicode && !hasIndices)
            {
                // we can the non-stateful fast path which is the common case
                var m = matcher.Match(s, (int) lastIndex);
                if (!m.Success)
                {
                    return Null;
                }

                return CreateReturnValueArray(R.Engine, matcher, m, s, fullUnicode: false, hasIndices: false);
            }

            // the stateful version
            Match match;
            while (true)
            {
                if (lastIndex > length)
                {
                    R.Set(RegExpInstance.PropertyLastIndex, JsNumber.PositiveZero, true);
                    return Null;
                }

                match = R.Value.Match(s, (int) lastIndex);
                var success = match.Success && (!sticky || match.Index == (int) lastIndex);
                if (!success)
                {
                    R.Set(RegExpInstance.PropertyLastIndex, JsNumber.PositiveZero, true);
                    return Null;
                }

                break;
            }

            var e = match.Index + match.Length;
            if (fullUnicode)
            {
                e = GetStringIndex(s, e);
            }

            if (global || sticky)
            {
                R.Set(RegExpInstance.PropertyLastIndex, e, true);
            }

            return CreateReturnValueArray(R.Engine, matcher, match, s, fullUnicode, hasIndices);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getstringindex
        /// </summary>
        private static int GetStringIndex(string s, int codePointIndex)
        {
            if (s.Length == 0)
            {
                return 0;
            }

            var len = s.Length;
            var codeUnitCount = 0;
            var codePointCount = 0;

            while (codeUnitCount < len)
            {
                if (codePointCount == codePointIndex)
                {
                    return codeUnitCount;
                }

                var isSurrogatePair = char.IsSurrogatePair(s, codeUnitCount);
                codeUnitCount += isSurrogatePair ? 2 : 1;
                codePointCount += 1;
            }

            return len;
        }

        private static ArrayInstance CreateReturnValueArray(
            Engine engine,
            Regex regex,
            Match match,
            string s,
            bool fullUnicode,
            bool hasIndices)
        {
            var array = engine.Realm.Intrinsics.Array.ArrayCreate((ulong) match.Groups.Count);
            array.CreateDataProperty(PropertyIndex, match.Index);
            array.CreateDataProperty(PropertyInput, s);

            ObjectInstance? groups = null;
            List<string>? groupNames = null;
            var indices = hasIndices ? new List<JsNumber[]?>(match.Groups.Count) : null;
            for (uint i = 0; i < match.Groups.Count; i++)
            {
                var capture = match.Groups[(int) i];
                var capturedValue = Undefined;
                if (capture?.Success == true)
                {
                    capturedValue = capture.Value;
                }

                if (hasIndices)
                {
                    if (capture?.Success == true)
                    {
                        indices!.Add(new[] { JsNumber.Create(capture.Index), JsNumber.Create(capture.Index + capture.Length) });
                    }
                    else
                    {
                        indices!.Add(null);
                    }
                }

                var groupName = GetRegexGroupName(regex, (int) i);
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    groups ??= OrdinaryObjectCreate(engine, null);
                    groups.CreateDataPropertyOrThrow(groupName, capturedValue);
                    groupNames ??= new List<string>();
                    groupNames.Add(groupName!);
                }

                array.SetIndexValue(i, capturedValue, updateLength: false);
            }

            array.CreateDataProperty(PropertyGroups, groups ?? Undefined);

            if (hasIndices)
            {
                var indicesArray = MakeMatchIndicesIndexPairArray(engine, s, indices!, groupNames, groupNames?.Count > 0);
                array.CreateDataPropertyOrThrow("indices", indicesArray);
            }

            return array;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-makematchindicesindexpairarray
        /// </summary>
        private static ArrayInstance MakeMatchIndicesIndexPairArray(
            Engine engine,
            string s,
            List<JsNumber[]?> indices,
            List<string>? groupNames,
            bool hasGroups)
        {
            var n = indices.Count;
            var a = engine.Realm.Intrinsics.Array.Construct((uint) n);
            ObjectInstance? groups = null;
            if (hasGroups)
            {
                groups = OrdinaryObjectCreate(engine, null);
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
                    groups!.CreateDataPropertyOrThrow(groupNames![i - 1], matchIndexPair);
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

        private static string? GetRegexGroupName(Regex regex, int index)
        {
            if (index == 0)
            {
                return null;
            }
            var groupNameFromNumber = regex.GroupNameFromNumber(index);
            if (groupNameFromNumber.Length == 1 && groupNameFromNumber[0] == 48 + index)
            {
                // regex defaults to index as group name when it's not a named group
                return null;

            }
            return groupNameFromNumber;
        }

        private JsValue Exec(JsValue thisObj, JsValue[] arguments)
        {
            var r = thisObj as RegExpInstance;
            if (r is null)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            var s = TypeConverter.ToString(arguments.At(0));
            return RegExpBuiltinExec(r, s);
        }
    }
}
