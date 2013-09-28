using System;
using System.Globalization;
using System.Text;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.String
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.5.4
    /// </summary>
    public sealed class StringPrototype : StringInstance
    {
        private StringPrototype(Engine engine)
            : base(engine)
        {
        }

        public static StringPrototype CreatePrototypeObject(Engine engine, StringConstructor stringConstructor)
        {
            var obj = new StringPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.PrimitiveValue = "";
            obj.Extensible = true;

            obj.FastAddProperty("constructor", stringConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, string>(Engine, ToStringString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance<object, string>(Engine, ValueOf), true, false, true);
            FastAddProperty("charAt", new ClrFunctionInstance<object, object>(Engine, CharAt, 1), true, false, true);
            FastAddProperty("charCodeAt", new ClrFunctionInstance<object, object>(Engine, CharCodeAt, 1), true, false, true);
            FastAddProperty("concat", new ClrFunctionInstance<object, string>(Engine, Concat, 1), true, false, true);
            FastAddProperty("indexOf", new ClrFunctionInstance<object, double>(Engine, IndexOf, 1), true, false, true);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance<object, double>(Engine, LastIndexOf, 1), true, false, true);
            FastAddProperty("localeCompare", new ClrFunctionInstance<object, double>(Engine, LocaleCompare), true, false, true);
            FastAddProperty("match", new ClrFunctionInstance<object, object>(Engine, Match, 1), true, false, true);
            FastAddProperty("replace", new ClrFunctionInstance<object, object>(Engine, Replace), true, false, true);
            FastAddProperty("search", new ClrFunctionInstance<object, double>(Engine, Search, 1), true, false, true);
            FastAddProperty("slice", new ClrFunctionInstance<object, string>(Engine, Slice, 2), true, false, true);
            FastAddProperty("split", new ClrFunctionInstance<object, ArrayInstance>(Engine, Split, 2), true, false, true);
            FastAddProperty("substring", new ClrFunctionInstance<object, string>(Engine, Substring, 2), true, false, true);
            FastAddProperty("toLowerCase", new ClrFunctionInstance<object, string>(Engine, ToLowerCase), true, false, true);
            FastAddProperty("toLocaleLowerCase", new ClrFunctionInstance<object, string>(Engine, ToLocaleLowerCase), true, false, true);
            FastAddProperty("toUpperCase", new ClrFunctionInstance<object, string>(Engine, ToUpperCase), true, false, true);
            FastAddProperty("toLocaleUpperCase", new ClrFunctionInstance<object, string>(Engine, ToLocaleUpperCase), true, false, true);
            FastAddProperty("trim", new ClrFunctionInstance<object, string>(Engine, Trim), true, false, true);
        }

        private string ToStringString(object thisObj, object[] arguments)
        {
            var s = TypeConverter.ToObject(Engine, thisObj) as StringInstance;
            if (s == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return s.PrimitiveValue;
        }

        private string Trim(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return s.Trim();
        }

        private static string ToLocaleUpperCase(object thisObj, object[] arguments)
        {
            var s = TypeConverter.ToString(thisObj);
            return s.ToUpper();
        }

        private static string ToUpperCase(object thisObj, object[] arguments)
        {
            var s = TypeConverter.ToString(thisObj);
            return s.ToUpperInvariant();
        }

        private static string ToLocaleLowerCase(object thisObj, object[] arguments)
        {
            var s = TypeConverter.ToString(thisObj);
            return s.ToLower();
        }

        private static string ToLowerCase(object thisObj, object[] arguments)
        {
            var s = TypeConverter.ToString(thisObj);
            return s.ToLowerInvariant();
        }

        private string Substring(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var start = TypeConverter.ToNumber(arguments.At(0));
            var end = TypeConverter.ToNumber(arguments.At(1));

            if (double.IsNaN(start) || start < 0)
            {
                start = 0;
            }

            if (double.IsNaN(end) || end < 0)
            {
                end = 0;
            }

            var len = s.Length;
            var intStart = (int) TypeConverter.ToInteger(start);
            var intEnd = arguments.At(1) == Undefined.Instance ? len : (int) TypeConverter.ToInteger(end);
            var finalStart = System.Math.Min(len, System.Math.Max(intStart, 0));
            var finalEnd = System.Math.Min(len, System.Math.Max(intEnd, 0));
            var from = System.Math.Min(finalStart, finalEnd);
            var to = System.Math.Max(finalStart, finalEnd);
            return s.Substring(from, to - from);
        }

        private ArrayInstance Split(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);

            var separator = arguments.At(0);
            var l = arguments.At(1);

            var a = (ArrayInstance) Engine.Array.Construct(Arguments.Empty);
            var lengthA = 0;
            var limit = l == Undefined.Instance ? UInt32.MaxValue : TypeConverter.ToUint32(l);
            var len = s.Length;
            var p = 0;
            
            if (limit == 0)
            {
                return a;
            }

            if (separator == Undefined.Instance)
            {
                return (ArrayInstance) Engine.Array.Construct(Arguments.From(s));
            }

            var rx = TypeConverter.ToObject(Engine, separator) as RegExpInstance;
            if (rx != null)
            {
                var match = rx.Value.Match(s, 0);

                int lastIndex = 0;
                int index = 0;
                while (match.Success && index < limit)
                {
                    if (match.Length == 0 && (match.Index == 0 || match.Index == len || match.Index == lastIndex))
                    {
                        match = match.NextMatch();
                        continue;
                    }

                    // Add the match results to the array.
                    a.DefineOwnProperty(index++.ToString(), new DataDescriptor(s.Substring(lastIndex, match.Index - lastIndex)) {Writable = true, Enumerable = true,Configurable = true}, false);
                    
                    if (index >= limit)
                    {
                        return a;
                    }

                    lastIndex = match.Index + match.Length;
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        var group = match.Groups[i];
                        object item = Undefined.Instance;
                        if (group.Captures.Count > 0)
                        {
                            item = match.Groups[i].Value;
                        }

                        a.DefineOwnProperty(index++.ToString(), new DataDescriptor(item) { Writable = true, Enumerable = true, Configurable = true }, false);

                        if (index >= limit)
                        {
                            return a;
                        }
                    }

                    match = match.NextMatch();
                }

                return a;
            }
            else
            {
                var sep = TypeConverter.ToString(separator);


                var segments = s.Split(new [] { sep }, StringSplitOptions.None);
                for (int i = 0; i < segments.Length && i < limit; i++)
                {
                    a.DefineOwnProperty(i.ToString(), new DataDescriptor(segments[i]) {Writable = true, Enumerable = true, Configurable=true} , false);
                }
            
                return a;
            }
            
        }

        private string Slice(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var start = TypeConverter.ToNumber(arguments.At(0));
            var end = TypeConverter.ToNumber(arguments.At(1));
            var len = s.Length;
            var intStart = (int)TypeConverter.ToInteger(start);
            var intEnd = arguments.At(1) == Undefined.Instance ? len : (int)TypeConverter.ToInteger(end);
            var from = intStart < 0 ? System.Math.Max(len + intStart, 0) : System.Math.Min(intStart, len);
            var to = intEnd < 0 ? System.Math.Max(len + intEnd, 0) : System.Math.Min(intEnd, len);
            var span = System.Math.Max(to - from, 0);

            return s.Substring(from, span);
        }

        private double Search(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);

            var regex = arguments.At(0);
            var rx = TypeConverter.ToObject(Engine, regex) as RegExpInstance ?? (RegExpInstance)Engine.RegExp.Construct(new[] { regex });
            var match = rx.Value.Match(s);
            if (!match.Success)
            {
                return -1;
            }

            return match.Index;
        }

        private object Replace(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
            /*
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var searchValue = arguments.At(0);
            var replaceValue = arguments.At(1);

            var rx = TypeConverter.ToObject(Engine, searchValue) as RegExpInstance;
            if (rx != null)
            {






                // Check if the replacement string contains any patterns.
                bool replaceTextContainsPattern = replaceText.IndexOf('$') >= 0;

                // Replace the input string with replaceText, recording the last match found.
                Match lastMatch = null;
                string result = rx.Value.Replace(s, match =>
                {
                    lastMatch = match;

                    // If there is no pattern, replace the pattern as is.
                    if (replaceTextContainsPattern == false)
                        return replaceText;

                    // Patterns
                    // $$	Inserts a "$".
                    // $&	Inserts the matched substring.
                    // $`	Inserts the portion of the string that precedes the matched substring.
                    // $'	Inserts the portion of the string that follows the matched substring.
                    // $n or $nn	Where n or nn are decimal digits, inserts the nth parenthesized submatch string, provided the first argument was a RegExp object.
                    var replacementBuilder = new System.Text.StringBuilder();
                    for (int i = 0; i < replaceText.Length; i++)
                    {
                        char c = replaceText[i];
                        if (c == '$' && i < replaceText.Length - 1)
                        {
                            c = replaceText[++i];
                            if (c == '$')
                                replacementBuilder.Append('$');
                            else if (c == '&')
                                replacementBuilder.Append(match.Value);
                            else if (c == '`')
                                replacementBuilder.Append(input.Substring(0, match.Index));
                            else if (c == '\'')
                                replacementBuilder.Append(input.Substring(match.Index + match.Length));
                            else if (c >= '0' && c <= '9')
                            {
                                int matchNumber1 = c - '0';

                                // The match number can be one or two digits long.
                                int matchNumber2 = 0;
                                if (i < replaceText.Length - 1 && replaceText[i + 1] >= '0' && replaceText[i + 1] <= '9')
                                    matchNumber2 = matchNumber1*10 + (replaceText[i + 1] - '0');

                                // Try the two digit capture first.
                                if (matchNumber2 > 0 && matchNumber2 < match.Groups.Count)
                                {
                                    // Two digit capture replacement.
                                    replacementBuilder.Append(match.Groups[matchNumber2].Value);
                                    i++;
                                }
                                else if (matchNumber1 > 0 && matchNumber1 < match.Groups.Count)
                                {
                                    // Single digit capture replacement.
                                    replacementBuilder.Append(match.Groups[matchNumber1].Value);
                                }
                                else
                                {
                                    // Capture does not exist.
                                    replacementBuilder.Append('$');
                                    i--;
                                }
                            }
                            else
                            {
                                // Unknown replacement pattern.
                                replacementBuilder.Append('$');
                                replacementBuilder.Append(c);
                            }
                        }
                        else
                            replacementBuilder.Append(c);
                    }

                    return replacementBuilder.ToString();
                }, this.Global == true ? -1 : 1);

                // Set the deprecated RegExp properties if at least one match was found.
                if (lastMatch != null)
                    this.Engine.RegExp.SetDeprecatedProperties(input, lastMatch);

                return result;
            }
            else
            {
                var searchString = TypeConverter.ToString(searchValue);
                var m = 0;

                // Find the first occurrance of substr.
                int start = thisObject.IndexOf(substr, StringComparison.Ordinal);
                if (start == -1)
                    return thisObject;
                int end = start + substr.Length;

                // Replace only the first match.
                var result = new System.Text.StringBuilder(thisObject.Length + (replaceText.Length - substr.Length));
                result.Append(thisObject, 0, start);
                result.Append(replaceText);
                result.Append(thisObject, end, thisObject.Length - end);
                return result.ToString();



            }
             * */
        }

        private object Match(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);

            var regex = arguments.At(0);
            RegExpInstance rx = null;
            if (TypeConverter.GetType(regex) == Types.Object)
            {
                rx = regex as RegExpInstance;
            }

            rx = rx ?? (RegExpInstance) Engine.RegExp.Construct(new[] {regex});

            var global = (bool) rx.Get("global");
            if (!global)
            {
                return Engine.RegExp.PrototypeObject.Exec(rx, Arguments.From(s));
            }
            else
            {
                rx.Put("lastIndex", (double) 0, false);
                var a = Engine.Array.Construct(Arguments.Empty);
                double previousLastIndex = 0;
                var n = 0;
                var lastMatch = true;
                while (lastMatch)
                {
                    var result = Engine.RegExp.PrototypeObject.Exec(rx, Arguments.From(s)) as ObjectInstance;
                    if (result == null)
                    {
                        lastMatch = false;
                    }
                    else
                    {
                        var thisIndex = (double) rx.Get("lastIndex");
                        if (thisIndex == previousLastIndex)
                        {
                            rx.Put("lastIndex", (double) thisIndex + 1, false);
                            previousLastIndex = thisIndex;
                        }

                        var matchStr = result.Get("0");
                        a.DefineOwnProperty(TypeConverter.ToString(n), new DataDescriptor(matchStr) { Writable = true, Enumerable = true, Configurable = true}, false);
                        n++;
                    }
                }
                if (n == 0)
                {
                    return Null.Instance;
                }
                return a;
            }

        }

        private double LocaleCompare(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var that = TypeConverter.ToString(arguments.Length > 0 ? arguments[0] : Undefined.Instance);

            return string.CompareOrdinal(s, that);
        }

        private double LastIndexOf(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var searchStr = TypeConverter.ToString(arguments.At(0));
            double numPos = arguments.At(0) == Undefined.Instance ? double.NaN : TypeConverter.ToNumber(arguments.At(0));
            double pos = double.IsNaN(numPos) ? double.PositiveInfinity : TypeConverter.ToInteger(numPos);
            var len = s.Length;
            var start = System.Math.Min(len, System.Math.Max(pos, 0));
            var searchLen = searchStr.Length;

            return s.LastIndexOf(searchStr, len - (int) start, StringComparison.Ordinal);
        }

        private double IndexOf(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var searchStr = TypeConverter.ToString(arguments.Length > 0 ? arguments[0] : Undefined.Instance);
            double pos = 0;
            if (arguments.Length > 1 && arguments[1] != Undefined.Instance)
            {
                pos = TypeConverter.ToInteger(arguments[1]);
            }

            if (pos >= s.Length)
            {
                return -1;
            }

            if (pos < 0)
            {
                pos = 0;
            }

            return s.IndexOf(searchStr, (int) pos, StringComparison.Ordinal);
        }

        private string Concat(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var sb = new StringBuilder(s);
            for (int i = 0; i < arguments.Length; i++)
            {
                sb.Append(TypeConverter.ToString(arguments[i]));
            }

            return sb.ToString();
        }

        private object CharCodeAt(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            object pos = arguments.Length > 0 ? arguments[0] : 0;
            var s = TypeConverter.ToString(thisObj);
            var position = (int)TypeConverter.ToInteger(pos);
            if (position < 0 || position >= s.Length)
            {
                return double.NaN;
            }
            return (uint)s[position];
        }

        private object CharAt(object thisObj, object[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            var position = TypeConverter.ToInteger(arguments.At(0));
            var size = s.Length;
            if (position >= size || position < 0)
            {
                return "";
            }
            return s[(int) position].ToString();

        }

        private string ValueOf(object thisObj, object[] arguments)
        {
            var s = thisObj as StringInstance;
            if (s == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return s.PrimitiveValue;
        }
    }
}
