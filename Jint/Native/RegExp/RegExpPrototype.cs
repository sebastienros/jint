using System;
using System.Collections.Generic;
using System.Globalization;
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
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp
{
    public sealed class RegExpPrototype : ObjectInstance
    {
        private static readonly JsString PropertyExec = new JsString("exec");
        private static readonly JsString PropertyIndex = new JsString("index");
        private static readonly JsString PropertyInput = new JsString("input");
        private static readonly JsString PropertySticky = new JsString("sticky");
        private static readonly JsString PropertyGlobal = new JsString("global");
        internal static readonly JsString PropertySource = new JsString("source");
        private static readonly JsValue DefaultSource = new JsString("(?:)");
        internal static readonly JsString PropertyFlags = new JsString("flags");

        private RegExpConstructor _regExpConstructor;
        private readonly Func<JsValue, JsValue[], JsValue> _defaultExec;

        private RegExpPrototype(Engine engine) : base(engine)
        {
            _defaultExec = Exec;
        }

        public static RegExpPrototype CreatePrototypeObject(Engine engine, RegExpConstructor regExpConstructor)
        {
            var obj = new RegExpPrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject, _regExpConstructor = regExpConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;

            GetSetPropertyDescriptor CreateGetAccessorDescriptor(string name, Func<RegExpInstance, JsValue> valueExtractor, JsValue protoValue = null)
            {
                return new GetSetPropertyDescriptor(
                    get: new ClrFunctionInstance(Engine, name, (thisObj, arguments) =>
                    {
                        if (ReferenceEquals(thisObj, this))
                        {
                            return protoValue ?? Undefined;
                        }

                        if (!(thisObj is RegExpInstance r))
                        {
                            return ExceptionHelper.ThrowTypeError<JsValue>(_engine);
                        }

                        return valueExtractor(r);
                    }, 0, lengthFlags),
                    set: Undefined,
                    flags: PropertyFlag.Configurable);
            }

            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(12, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_regExpConstructor, propertyFlags),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToRegExpString, 0, lengthFlags), propertyFlags),
                ["exec"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "exec", _defaultExec, 1, lengthFlags), propertyFlags),
                ["test"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "test", Test, 1, lengthFlags), propertyFlags),
                ["dotAll"] = CreateGetAccessorDescriptor("get dotAll", r => r.DotAll),
                ["flags"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get flags", Flags, 0, lengthFlags), set: Undefined, flags: PropertyFlag.Configurable),
                ["global"] = CreateGetAccessorDescriptor("get global", r => r.Global),
                ["ignoreCase"] = CreateGetAccessorDescriptor("get ignoreCase", r => r.IgnoreCase),
                ["multiline"] = CreateGetAccessorDescriptor("get multiline", r => r.Multiline),
                ["source"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get source", Source, 0, lengthFlags), set: Undefined, flags: PropertyFlag.Configurable),
                ["sticky"] = CreateGetAccessorDescriptor("get sticky", r => r.Sticky),
                ["unicode"] = CreateGetAccessorDescriptor("get unicode", r => r.FullUnicode)
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

        private JsValue Source(JsValue thisObj, JsValue[] arguments)
        {
            if (ReferenceEquals(thisObj, this))
            {
                return DefaultSource;
            }

            if (!(thisObj is RegExpInstance r))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine);
            }

            return r.Source.Replace("/", "\\/");
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
            bool mayHaveNamedCaptures = false;
            if (!functionalReplace)
            {
                var value = TypeConverter.ToString(replaceValue);
                replaceValue = value;
                mayHaveNamedCaptures = value.IndexOf('$') != -1;
            }

            var fullUnicode = false;
            var global = TypeConverter.ToBoolean(rx.Get(PropertyGlobal));

            if (global)
            {
                fullUnicode = TypeConverter.ToBoolean(rx.Get("unicode"));
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

                        for (var i = 1; i < match.Groups.Count; i++)
                        {
                            var capture = match.Groups[i];
                            replacerArgs.Add(capture.Value);
                        }

                        replacerArgs.Add(match.Index);
                        replacerArgs.Add(s);

                        // no named captures
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
                    var thisIndex = (int) TypeConverter.ToLength(rx.Get(RegExpInstance.PropertyLastIndex));
                    var nextIndex = AdvanceStringIndex(s, thisIndex, fullUnicode);
                    rx.Set(RegExpInstance.PropertyLastIndex, nextIndex);
                }
            }

            var accumulatedResult = "";
            var nextSourcePosition = 0;

            var captures = new List<string>();
            foreach (var result in results)
            {
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

                var namedCaptures = result.Get("groups");
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
                        namedCaptures = TypeConverter.ToObject(_engine, namedCaptures);
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
            using (var replacementBuilder = StringBuilderPool.Rent())
            {
                for (int i = 0; i < replacement.Length; i++)
                {
                    char c = replacement[i];
                    if (c == '$' && i < replacement.Length - 1)
                    {
                        c = replacement[++i];
                        switch (c)
                        {
                            case '$':
                                replacementBuilder.Builder.Append('$');
                                break;
                            case '&':
                                replacementBuilder.Builder.Append(matched);
                                break;
                            case '`':
                                replacementBuilder.Builder.Append(str.Substring(0, position));
                                break;
                            case '\'':
                                replacementBuilder.Builder.Append(str.Substring(position + matched.Length));
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
                                        replacementBuilder.Builder.Append(TypeConverter.ToString(captures[matchNumber2 - 1]));
                                        i++;
                                    }
                                    else if (matchNumber1 > 0 && matchNumber1 <= captures.Length)
                                    {
                                        // Single digit capture replacement.
                                        replacementBuilder.Builder.Append(TypeConverter.ToString(captures[matchNumber1 - 1]));
                                    }
                                    else
                                    {
                                        // Capture does not exist.
                                        replacementBuilder.Builder.Append('$');
                                        i--;
                                    }
                                }
                                else
                                {
                                    // Unknown replacement pattern.
                                    replacementBuilder.Builder.Append('$');
                                    replacementBuilder.Builder.Append(c);
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        replacementBuilder.Builder.Append(c);
                    }
                }

                return replacementBuilder.ToString();
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-regexp.prototype-@@split
        /// </summary>
        private JsValue Split(JsValue thisObj, JsValue[] arguments)
        {
            var rx = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.split");
            var s = TypeConverter.ToString(arguments.At(0));
            var limit = arguments.At(1);
            var c = SpeciesConstructor(rx, _engine.RegExp);
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
                return _engine.Array.ConstructFast(0);
            }

            if (s.Length == 0)
            {
                var a = _engine.Array.ConstructFast(0);
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
                    return StringPrototype.SplitWithStringSeparator(_engine, "", s, (uint) s.Length);                    
                }
                
                var a = (ArrayInstance) Engine.Array.Construct(Arguments.Empty);
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
            var a = _engine.Array.ConstructFast(0);
            var previousStringIndex = 0;
            var currentIndex = 0;
            while (currentIndex < s.Length)
            {
                splitter.Set(RegExpInstance.PropertyLastIndex, currentIndex, true);
                var z = RegExpExec(splitter, s);
                if (z.IsNull())
                {
                    currentIndex = AdvanceStringIndex(s, currentIndex, unicodeMatching);
                    continue;
                }

                var endIndex = (int) TypeConverter.ToLength(splitter.Get(RegExpInstance.PropertyLastIndex));
                endIndex = System.Math.Min(endIndex, s.Length);
                if (endIndex == previousStringIndex)
                {
                    currentIndex = AdvanceStringIndex(s, currentIndex, unicodeMatching);
                    continue;
                }

                var t = s.Substring(previousStringIndex, currentIndex - previousStringIndex);
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

            a.SetIndexValue(lengthA, s.Substring(previousStringIndex, s.Length - previousStringIndex), updateLength: true);
            return a;
        }

        private JsValue Flags(JsValue thisObj, JsValue[] arguments)
        {
            var r = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.flags");

            static string AddFlagIfPresent(JsValue o, JsValue p, char flag, string s)
            {
                return TypeConverter.ToBoolean(o.Get(p)) ? s + flag : s;
            }

            var result = AddFlagIfPresent(r, PropertyGlobal, 'g', "");
            result = AddFlagIfPresent(r, "ignoreCase", 'i', result);
            result = AddFlagIfPresent(r, "multiline", 'm', result);
            result = AddFlagIfPresent(r, "dotAll", 's', result);
            result = AddFlagIfPresent(r, "unicode", 'u', result);
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

        private JsValue Match(JsValue thisObj, JsValue[] arguments)
        {
            var rx = AssertThisIsObjectInstance(thisObj, "RegExp.prototype.match");

            var s = TypeConverter.ToString(arguments.At(0));
            var global = TypeConverter.ToBoolean(rx.Get(PropertyGlobal));
            if (!global)
            {
                return RegExpExec(rx, s);
            }

            var fullUnicode = TypeConverter.ToBoolean(rx.Get("unicode"));
            rx.Set(RegExpInstance.PropertyLastIndex, JsNumber.PositiveZero, true);

            if (!fullUnicode
                && rx is RegExpInstance rei
                && rei.TryGetDefaultRegExpExec(out _))
            {
                // fast path
                var a = Engine.Array.ConstructFast(0);

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
            var a = Engine.Array.ConstructFast(0);
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
                    var thisIndex = (int) TypeConverter.ToLength(rx.Get(RegExpInstance.PropertyLastIndex));
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
            var c = SpeciesConstructor(r, _engine.RegExp);

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

            return _engine.Iterator.CreateRegExpStringIterator(matcher, s, global, fullUnicode);
        }

        private static int AdvanceStringIndex(string s, int index, bool unicode)
        {
            if (!unicode || index + 1 >= s.Length)
            {
                return index + 1;
            }

            var first = s[index];
            if (first < 0xD800 || first > 0xDBFF)
            {
                return index + 1;
            }

            var second = s[index + 1];
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
                    return ExceptionHelper.ThrowTypeError<ObjectInstance>(r.Engine);
                }

                return result;
            }

            if (!(r is RegExpInstance ri))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(r.Engine);
            }

            return RegExpBuiltinExec(ri, s);
        }

        internal bool TryGetDefaultExec(ObjectInstance o, out Func<JsValue, JsValue[], JsValue> exec)
        {
            if (o.Get(PropertyExec) is ClrFunctionInstance functionInstance && functionInstance._func == _defaultExec)
            {
                exec = _defaultExec;
                return true;
            }

            exec = default;
            return false;
        }

        private static JsValue RegExpBuiltinExec(RegExpInstance R, string s)
        {
            var length = s.Length;
            var lastIndex = (int) TypeConverter.ToLength(R.Get(RegExpInstance.PropertyLastIndex));

            var global = R.Global;
            var sticky = R.Sticky;
            if (!global && !sticky)
            {
                lastIndex = 0;
            }

            if (R.Source == RegExpInstance.regExpForMatchingAllCharacters)  // Reg Exp is really ""
            {
                if (lastIndex > s.Length)
                {
                    return Null;
                }

                // "aaa".match() => [ '', index: 0, input: 'aaa' ]
                var array = R.Engine.Array.ConstructFast(1);
                array.FastAddProperty(PropertyIndex, lastIndex, true, true, true);
                array.FastAddProperty(PropertyInput, s, true, true, true);
                array.SetIndexValue(0, JsString.Empty, updateLength: false);
                return array;
            }

            var matcher = R.Value;
            var fullUnicode = R.FullUnicode;

            if (!global & !sticky && !fullUnicode)
            {
                // we can the non-stateful fast path which is the common case
                var m = matcher.Match(s, lastIndex);
                if (!m.Success)
                {
                    return Null;
                }

                return CreateReturnValueArray(R.Engine, m, s, fullUnicode: false);
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

                match = R.Value.Match(s, lastIndex);
                var success = match.Success && (!sticky || match.Index == lastIndex);
                if (!success)
                {
                    if (sticky)
                    {
                        R.Set(RegExpInstance.PropertyLastIndex, JsNumber.PositiveZero, true);
                        return Null;
                    }

                    lastIndex = AdvanceStringIndex(s, lastIndex, fullUnicode);
                }
                else
                {
                    break;
                }
            }

            var e = match.Index + match.Length;
            if (fullUnicode)
            {
                // e is an index into the Input character list, derived from S, matched by matcher.
                // Let eUTF be the smallest index into S that corresponds to the character at element e of Input.
                // If e is greater than or equal to the number of elements in Input, then eUTF is the number of code units in S.
                // Set e to eUTF.
                var indexes = StringInfo.ParseCombiningCharacters(s);
                if (match.Index < indexes.Length)
                {
                    var sub = StringInfo.GetNextTextElement(s, match.Index);
                    e += sub.Length - 1;
                }
            }

            R.Set(RegExpInstance.PropertyLastIndex, e, true);

            return CreateReturnValueArray(R.Engine, match, s, fullUnicode);
        }

        private static ArrayInstance CreateReturnValueArray(Engine engine, Match match, string inputValue, bool fullUnicode)
        {
            var array = engine.Array.ConstructFast((ulong) match.Groups.Count);
            array.CreateDataProperty(PropertyIndex, match.Index);
            array.CreateDataProperty(PropertyInput, inputValue);

            ObjectInstance groups = null;
            for (uint i = 0; i < match.Groups.Count; i++)
            {
                var capture = i < match.Groups.Count ? match.Groups[(int) i] : null;
                var capturedValue = Undefined;
                if (capture?.Success == true)
                {
                    capturedValue = fullUnicode
                        ? StringInfo.GetNextTextElement(inputValue, capture.Index)
                        : capture.Value;


                    // todo detect captured name
                }

                array.SetIndexValue(i, capturedValue, updateLength: false);
            }

            array.CreateDataProperty("groups", groups ?? Undefined);

            return array;
        }

        private JsValue Exec(JsValue thisObj, JsValue[] arguments)
        {
            if (!(thisObj is RegExpInstance r))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine);
            }

            var s = TypeConverter.ToString(arguments.At(0));
            return RegExpBuiltinExec(r, s);
        }
    }
}