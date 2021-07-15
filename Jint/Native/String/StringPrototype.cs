using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Pooling;
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
        private readonly Realm _realm;
        private readonly StringConstructor _constructor;

        internal StringPrototype(
            Engine engine,
            Realm realm,
            StringConstructor constructor,
            ObjectPrototype objectPrototype)
            : base(engine)
        {
            _prototype = objectPrototype;
            PrimitiveValue = JsString.Empty;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
            _realm = realm;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = lengthFlags | PropertyFlag.Writable;

            var trimStart = new PropertyDescriptor(new ClrFunctionInstance(Engine, "trimStart", TrimStart, 0, lengthFlags), propertyFlags);
            var trimEnd = new PropertyDescriptor(new ClrFunctionInstance(Engine, "trimEnd", TrimEnd, 0, lengthFlags), propertyFlags);
            var properties = new PropertyDictionary(35, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToStringString, 0, lengthFlags), propertyFlags),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, lengthFlags), propertyFlags),
                ["charAt"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "charAt", CharAt, 1, lengthFlags), propertyFlags),
                ["charCodeAt"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "charCodeAt", CharCodeAt, 1, lengthFlags), propertyFlags),
                ["codePointAt"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "codePointAt", CodePointAt, 1, lengthFlags), propertyFlags),
                ["concat"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "concat", Concat, 1, lengthFlags), propertyFlags),
                ["indexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1, lengthFlags), propertyFlags),
                ["endsWith"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "endsWith", EndsWith, 1, lengthFlags), propertyFlags),
                ["startsWith"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "startsWith", StartsWith, 1, lengthFlags), propertyFlags),
                ["lastIndexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1, lengthFlags), propertyFlags),
                ["localeCompare"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "localeCompare", LocaleCompare, 1, lengthFlags), propertyFlags),
                ["match"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "match", Match, 1, lengthFlags), propertyFlags),
                ["matchAll"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "matchAll", MatchAll, 1, lengthFlags), propertyFlags),
                ["replace"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "replace", Replace, 2, lengthFlags), propertyFlags),
                ["search"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "search", Search, 1, lengthFlags), propertyFlags),
                ["slice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "slice", Slice, 2, lengthFlags), propertyFlags),
                ["split"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "split", Split, 2, lengthFlags), propertyFlags),
                ["substr"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "substr", Substr, 2), propertyFlags),
                ["substring"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "substring", Substring, 2, lengthFlags), propertyFlags),
                ["toLowerCase"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLowerCase", ToLowerCase, 0, lengthFlags), propertyFlags),
                ["toLocaleLowerCase"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleLowerCase", ToLocaleLowerCase, 0, lengthFlags), propertyFlags),
                ["toUpperCase"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toUpperCase", ToUpperCase, 0, lengthFlags), propertyFlags),
                ["toLocaleUpperCase"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleUpperCase", ToLocaleUpperCase, 0, lengthFlags), propertyFlags),
                ["trim"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "trim", Trim, 0, lengthFlags), propertyFlags),
                ["trimStart"] = trimStart,
                ["trimEnd"] = trimEnd,
                ["trimLeft"] = trimStart,
                ["trimRight"] = trimEnd,
                ["padStart"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "padStart", PadStart, 1, lengthFlags), propertyFlags),
                ["padEnd"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "padEnd", PadEnd, 1, lengthFlags), propertyFlags),
                ["includes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "includes", Includes, 1, lengthFlags), propertyFlags),
                ["normalize"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "normalize", Normalize, 0, lengthFlags), propertyFlags),
                ["repeat"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "repeat", Repeat, 1, lengthFlags), propertyFlags)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.iterator]", Iterator, 0, lengthFlags), propertyFlags)
            };
            SetSymbols(symbols);
        }

        private ObjectInstance Iterator(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(_engine, thisObj);
            var str = TypeConverter.ToString(thisObj);
            return _realm.Intrinsics.Iterator.Construct(str);
        }

        private JsValue ToStringString(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj.IsString())
            {
                return thisObj;
            }

            var s = TypeConverter.ToObject(_realm, thisObj) as StringInstance;
            if (ReferenceEquals(s, null))
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return s.PrimitiveValue;
        }

        // http://msdn.microsoft.com/en-us/library/system.char.iswhitespace(v=vs.110).aspx
        // http://en.wikipedia.org/wiki/Byte_order_mark
        const char BOM_CHAR = '\uFEFF';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsWhiteSpaceEx(char c)
        {
            return char.IsWhiteSpace(c) || c == BOM_CHAR;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string TrimEndEx(string s)
        {
            if (s.Length == 0)
                return string.Empty;

            if (!IsWhiteSpaceEx(s[s.Length - 1]))
                return s;

            return TrimEnd(s);
        }

        private static string TrimEnd(string s)
        {
            var i = s.Length - 1;
            while (i >= 0)
            {
                if (IsWhiteSpaceEx(s[i]))
                    i--;
                else
                    break;
            }

            return i >= 0 ? s.Substring(0, i + 1) : string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string TrimStartEx(string s)
        {
            if (s.Length == 0)
                return string.Empty;

            if (!IsWhiteSpaceEx(s[0]))
                return s;

            return TrimStart(s);
        }

        private static string TrimStart(string s)
        {
            var i = 0;
            while (i < s.Length)
            {
                if (IsWhiteSpaceEx(s[i]))
                    i++;
                else
                    break;
            }

            return i >= s.Length ? string.Empty : s.Substring(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string TrimEx(string s)
        {
            return TrimEndEx(TrimStartEx(s));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue Trim(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return TrimEx(s);
        }

        private JsValue TrimStart(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return TrimStartEx(s);
        }

        private JsValue TrimEnd(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return TrimEndEx(s);
        }

        private JsValue ToLocaleUpperCase(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(_engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return new JsString(s.ToUpper());
        }

        private JsValue ToUpperCase(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(_engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return new JsString(s.ToUpperInvariant());
        }

        private JsValue ToLocaleLowerCase(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(_engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return new JsString(s.ToLower());
        }

        private JsValue ToLowerCase(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(_engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            return s.ToLowerInvariant();
        }

        private static int ToIntegerSupportInfinity(JsValue numberVal)
        {
            return numberVal._type == InternalTypes.Integer
                ? numberVal.AsInteger()
                : ToIntegerSupportInfinityUnlikely(numberVal);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ToIntegerSupportInfinityUnlikely(JsValue numberVal)
        {
            var doubleVal = TypeConverter.ToInteger(numberVal);
            int intVal;
            if (double.IsPositiveInfinity(doubleVal))
                intVal = int.MaxValue;
            else if (double.IsNegativeInfinity(doubleVal))
                intVal = int.MinValue;
            else
                intVal = (int) doubleVal;
            return intVal;
        }

        private JsValue Substring(JsValue thisObj, JsValue[] arguments)
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
            var intStart = ToIntegerSupportInfinity(start);

            var intEnd = arguments.At(1).IsUndefined() ? len : ToIntegerSupportInfinity(end);
            var finalStart = System.Math.Min(len, System.Math.Max(intStart, 0));
            var finalEnd = System.Math.Min(len, System.Math.Max(intEnd, 0));
            // Swap value if finalStart < finalEnd
            var from = System.Math.Min(finalStart, finalEnd);
            var to = System.Math.Max(finalStart, finalEnd);
            var length = to - from;

            if (length == 0)
            {
                return JsString.Empty;
            }

            if (length == 1)
            {
                return JsString.Create(s[from]);
            }

            return new JsString(s.Substring(from, length));
        }

        private JsValue Substr(JsValue thisObj, JsValue[] arguments)
        {
            var s = TypeConverter.ToString(thisObj);
            var start = TypeConverter.ToInteger(arguments.At(0));
            var length = arguments.At(1).IsUndefined()
                ? double.PositiveInfinity
                : TypeConverter.ToInteger(arguments.At(1));

            start = start >= 0 ? start : System.Math.Max(s.Length + start, 0);
            length = System.Math.Min(System.Math.Max(length, 0), s.Length - start);
            if (length <= 0)
            {
                return JsString.Empty;
            }

            var startIndex = TypeConverter.ToInt32(start);
            var l = TypeConverter.ToInt32(length);
            if (l == 1)
            {
                return TypeConverter.ToString(s[startIndex]);
            }
            return s.Substring(startIndex, l);
        }

        private JsValue Split(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);

            var separator = arguments.At(0);
            var limit = arguments.At(1);

            // fast path for empty regexp
            if (separator is RegExpInstance R && R.Source == RegExpInstance.regExpForMatchingAllCharacters)
            {
                separator = JsString.Empty;
            }

            if (separator is ObjectInstance oi)
            {
                var splitter = GetMethod(_realm, oi, GlobalSymbolRegistry.Split);
                if (splitter != null)
                {
                    return splitter.Call(separator, new[] { thisObj, limit });
                }
            }

            // Coerce into a number, true will become 1
            var lim = limit.IsUndefined() ? uint.MaxValue : TypeConverter.ToUint32(limit);

            if (lim == 0)
            {
                return _realm.Intrinsics.Array.Construct(Arguments.Empty);
            }

            if (separator.IsNull())
            {
                separator = Native.Null.Text;
            }
            else if (separator.IsUndefined())
            {
                var arrayInstance = _realm.Intrinsics.Array.ConstructFast(1);
                arrayInstance.SetIndexValue(0, s, updateLength: false);
                return arrayInstance;
            }
            else
            {
                if (!separator.IsRegExp())
                {
                    separator = TypeConverter.ToJsString(separator); // Coerce into a string, for an object call toString()
                }
            }

            return SplitWithStringSeparator(_realm, separator, s, lim);
        }

        internal static JsValue SplitWithStringSeparator(Realm realm, JsValue separator, string s, uint lim)
        {
            var segments = StringExecutionContext.Current.SplitSegmentList;
            segments.Clear();
            var sep = TypeConverter.ToString(separator);

            if (sep == string.Empty)
            {
                if (s.Length > segments.Capacity)
                {
                    segments.Capacity = s.Length;
                }

                for (var i = 0; i < s.Length; i++)
                {
                    segments.Add(TypeConverter.ToString(s[i]));
                }
            }
            else
            {
                var array = StringExecutionContext.Current.SplitArray1;
                array[0] = sep;
                segments.AddRange(s.Split(array, StringSplitOptions.None));
            }

            var length = (uint) System.Math.Min(segments.Count, lim);
            var a = realm.Intrinsics.Array.ConstructFast(length);
            for (int i = 0; i < length; i++)
            {
                a.SetIndexValue((uint) i, segments[i], updateLength: false);
            }

            a.SetLength(length);
            return a;
        }

        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var start = TypeConverter.ToNumber(arguments.At(0));
            if (double.IsNegativeInfinity(start))
            {
                start = 0;
            }
            if (double.IsPositiveInfinity(start))
            {
                return JsString.Empty;
            }

            var s = TypeConverter.ToString(thisObj);
            var end = TypeConverter.ToNumber(arguments.At(1));
            if (double.IsPositiveInfinity(end))
            {
                end = s.Length;
            }

            var len = s.Length;
            var intStart = (int) start;
            var intEnd = arguments.At(1).IsUndefined() ? len : (int) TypeConverter.ToInteger(end);
            var from = intStart < 0 ? System.Math.Max(len + intStart, 0) : System.Math.Min(intStart, len);
            var to = intEnd < 0 ? System.Math.Max(len + intEnd, 0) : System.Math.Min(intEnd, len);
            var span = System.Math.Max(to - from, 0);

            if (span == 0)
            {
                return JsString.Empty;
            }

            if (span == 1)
            {
                return JsString.Create(s[from]);
            }

            return new JsString(s.Substring(from, span));
        }

        private JsValue Search(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var regex = arguments.At(0);

            if (regex is ObjectInstance oi)
            {
                var searcher = GetMethod(_realm, oi, GlobalSymbolRegistry.Search);
                if (searcher != null)
                {
                    return searcher.Call(regex, new[] { thisObj });
                }
            }

            var rx = (RegExpInstance) _realm.Intrinsics.RegExp.Construct(new[] {regex});
            var s = TypeConverter.ToString(thisObj);
            return _engine.Invoke(rx, GlobalSymbolRegistry.Search, new JsValue[] { s });
        }

        private JsValue Replace(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var searchValue = arguments.At(0);
            var replaceValue = arguments.At(1);

            if (!searchValue.IsNullOrUndefined())
            {
                var replacer = GetMethod(_realm, searchValue, GlobalSymbolRegistry.Replace);
                if (replacer != null)
                {
                    return replacer.Call(searchValue, new[] { thisObj, replaceValue});
                }
            }

            var thisString = TypeConverter.ToJsString(thisObj);
            var searchString = TypeConverter.ToString(searchValue);
            var functionalReplace = replaceValue is ICallable;

            if (!functionalReplace)
            {
                replaceValue = TypeConverter.ToJsString(replaceValue);
            }

            var pos = thisString.IndexOf(searchString, StringComparison.Ordinal);
            var matched = searchString;
            if (pos < 0)
            {
                return thisString;
            }

            string replStr;
            if (functionalReplace)
            {
                var replValue = ((ICallable) replaceValue).Call(Undefined, new JsValue[] {matched, pos, thisString});
                replStr = TypeConverter.ToString(replValue);
            }
            else
            {
                var captures = System.Array.Empty<string>();
                replStr =  RegExpPrototype.GetSubstitution(matched, thisString.ToString(), pos, captures, Undefined, TypeConverter.ToString(replaceValue));
            }

            var tailPos = pos + matched.Length;
            var newString = thisString.Substring(0, pos) + replStr + thisString.Substring(tailPos);

            return newString;
        }

        private JsValue Match(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var regex = arguments.At(0);
            if (regex is ObjectInstance oi)
            {
                var matcher = GetMethod(_realm, oi, GlobalSymbolRegistry.Match);
                if (matcher != null)
                {
                    return matcher.Call(regex, new[] { thisObj });
                }
            }

            var rx = (RegExpInstance) _realm.Intrinsics.RegExp.Construct(new[] {regex});

            var s = TypeConverter.ToString(thisObj);
            return _engine.Invoke(rx, GlobalSymbolRegistry.Match, new JsValue[] { s });
        }

        private JsValue MatchAll(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(_engine, thisObj);

            var regex = arguments.At(0);
            if (!regex.IsNullOrUndefined())
            {
                if (regex.IsRegExp())
                {
                    var flags = regex.Get(RegExpPrototype.PropertyFlags);
                    TypeConverter.CheckObjectCoercible(_engine, flags);
                    if (TypeConverter.ToString(flags).IndexOf('g') < 0)
                    {
                        ExceptionHelper.ThrowTypeError(_realm);
                    }
                }
                var matcher = GetMethod(_realm, (ObjectInstance) regex, GlobalSymbolRegistry.MatchAll);
                if (matcher != null)
                {
                    return matcher.Call(regex, new[] { thisObj });
                }
            }

            var s = TypeConverter.ToString(thisObj);
            var rx = (RegExpInstance) _realm.Intrinsics.RegExp.Construct(new[] { regex, "g" });

            return _engine.Invoke(rx, GlobalSymbolRegistry.MatchAll, new JsValue[] { s });
        }

        private JsValue LocaleCompare(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var that = TypeConverter.ToString(arguments.At(0));

            return string.CompareOrdinal(s, that);
        }

        private JsValue LastIndexOf(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var searchStr = TypeConverter.ToString(arguments.At(0));
            double numPos = double.NaN;
            if (arguments.Length > 1 && !arguments[1].IsUndefined())
            {
                numPos = TypeConverter.ToNumber(arguments[1]);
            }

            var pos = double.IsNaN(numPos) ? double.PositiveInfinity : TypeConverter.ToInteger(numPos);

            var len = s.Length;
            var start = (int)System.Math.Min(System.Math.Max(pos, 0), len);
            var searchLen = searchStr.Length;

            var i = start;
            bool found;

            do
            {
                found = true;
                var j = 0;

                while (found && j < searchLen)
                {
                    if ((i + searchLen > len) || (s[i + j] != searchStr[j]))
                    {
                        found = false;
                    }
                    else
                    {
                        j++;
                    }
                }
                if (!found)
                {
                    i--;
                }

            } while (!found && i >= 0);

            return i;
        }

        private JsValue IndexOf(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);
            var searchStr = TypeConverter.ToString(arguments.At(0));
            double pos = 0;
            if (arguments.Length > 1 && !arguments[1].IsUndefined())
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

        private JsValue Concat(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            // try to hint capacity if possible
            int capacity = 0;
            for (int i = 0; i < arguments.Length; ++i)
            {
                if (arguments[i].Type == Types.String)
                {
                    capacity += arguments[i].ToString().Length;
                }
            }

            var value = TypeConverter.ToString(thisObj);
            capacity += value.Length;
            if (!(thisObj is JsString jsString))
            {
                jsString = new JsString.ConcatenatedString(value, capacity);
            }
            else
            {
                jsString = jsString.EnsureCapacity(capacity);
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                jsString = jsString.Append(arguments[i]);
            }

            return jsString;
        }

        private JsValue CharCodeAt(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            JsValue pos = arguments.Length > 0 ? arguments[0] : 0;
            var s = TypeConverter.ToString(thisObj);
            var position = (int)TypeConverter.ToInteger(pos);
            if (position < 0 || position >= s.Length)
            {
                return JsNumber.DoubleNaN;
            }
            return (long) s[position];
        }

        private JsValue CodePointAt(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            JsValue pos = arguments.Length > 0 ? arguments[0] : 0;
            var s = TypeConverter.ToString(thisObj);
            var position = (int)TypeConverter.ToInteger(pos);
            if (position < 0 || position >= s.Length)
            {
                return Undefined;
            }

            var first = (long) s[position];
            if (first >= 0xD800 && first <= 0xDBFF && s.Length > position + 1)
            {
                long second = s[position + 1];
                if (second >= 0xDC00 && second <= 0xDFFF)
                {
                    return (first - 0xD800) * 0x400 + second - 0xDC00 + 0x10000;
                }
            }
            return first;
        }

        private JsValue CharAt(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var s = TypeConverter.ToString(thisObj);
            var position = TypeConverter.ToInteger(arguments.At(0));
            var size = s.Length;
            if (position >= size || position < 0)
            {
                return JsString.Empty;
            }
            return JsString.Create(s[(int) position]);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is StringInstance si)
            {
                return si.PrimitiveValue;
            }

            if (thisObj is JsString)
            {
                return thisObj;
            }

            ExceptionHelper.ThrowTypeError(_realm);
            return Undefined;
        }

        /// <summary>
        /// https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/String/padStart
        /// </summary>
        /// <param name="thisObj">The original string object</param>
        /// <param name="arguments">
        ///     argument[0] is the target length of the output string
        ///     argument[1] is the string to pad with
        /// </param>
        /// <returns></returns>
        private JsValue PadStart(JsValue thisObj, JsValue[] arguments)
        {
            return Pad(thisObj, arguments, true);
        }

        /// <summary>
        /// https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/String/padEnd
        /// </summary>
        /// <param name="thisObj">The original string object</param>
        /// <param name="arguments">
        ///     argument[0] is the target length of the output string
        ///     argument[1] is the string to pad with
        /// </param>
        /// <returns></returns>
        private JsValue PadEnd(JsValue thisObj, JsValue[] arguments)
        {
            return Pad(thisObj, arguments, false);
        }

        private JsValue Pad(JsValue thisObj, JsValue[] arguments, bool padStart)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var targetLength = TypeConverter.ToInt32(arguments.At(0));
            var padStringValue = arguments.At(1);

            var padString = padStringValue.IsUndefined()
                ? " "
                : TypeConverter.ToString(padStringValue);

            var s = TypeConverter.ToJsString(thisObj);
            if (s.Length > targetLength || padString.Length == 0)
            {
                return s;
            }

            targetLength = targetLength - s.Length;
            if (targetLength > padString.Length)
            {
                padString = string.Join("", Enumerable.Repeat(padString, (targetLength / padString.Length) + 1));
            }

            return padStart
                ? $"{padString.Substring(0, targetLength)}{s}"
                : $"{s}{padString.Substring(0, targetLength)}";
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/#sec-string.prototype.startswith
        /// </summary>
        private JsValue StartsWith(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);

            var searchString = arguments.At(0);
            if (ReferenceEquals(searchString, Null))
            {
                searchString = Native.Null.Text;
            }
            else
            {
                if (searchString.IsRegExp())
                {
                    ExceptionHelper.ThrowTypeError(_realm);
                }
            }

            var searchStr = TypeConverter.ToString(searchString);

            var pos = TypeConverter.ToInt32(arguments.At(1));

            var len = s.Length;
            var start = System.Math.Min(System.Math.Max(pos, 0), len);
            var searchLength = searchStr.Length;
            if (searchLength + start > len)
            {
                return false;
            }

            for (var i = 0; i < searchLength; i++)
            {
                if (s[start + i] != searchStr[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/#sec-string.prototype.endswith
        /// </summary>
        private JsValue EndsWith(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s = TypeConverter.ToString(thisObj);

            var searchString = arguments.At(0);
            if (ReferenceEquals(searchString, Null))
            {
                searchString = Native.Null.Text;
            }
            else
            {
                if (searchString.IsRegExp())
                {
                    ExceptionHelper.ThrowTypeError(_realm);
                }
            }

            var searchStr = TypeConverter.ToString(searchString);

            var len = s.Length;
            var pos = TypeConverter.ToInt32(arguments.At(1, len));
            var end = System.Math.Min(System.Math.Max(pos, 0), len);
            var searchLength = searchStr.Length;
            var start = end - searchLength;

            if (start < 0)
            {
                return false;
            }

            for (var i = 0; i < searchLength; i++)
            {
                if (s[start + i] != searchStr[i])
                {
                    return false;
                }
            }

            return true;
        }

        private JsValue Includes(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);

            var s1 = TypeConverter.ToString(thisObj);
            var searchString = arguments.At(0);

            if (searchString.IsRegExp())
            {
                ExceptionHelper.ThrowTypeError(_realm, "First argument to String.prototype.includes must not be a regular expression");
            }

            var searchStr = TypeConverter.ToString(searchString);
            double pos = 0;
            if (arguments.Length > 1 && !arguments[1].IsUndefined())
            {
                pos = TypeConverter.ToInteger(arguments[1]);
            }

            if (searchStr.Length == 0)
            {
                return true;
            }

            if (pos >= s1.Length)
            {
                return false;
            }

            if (pos < 0)
            {
                pos = 0;
            }

            return s1.IndexOf(searchStr, (int) pos, StringComparison.Ordinal) > -1;
        }

        private JsValue Normalize(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var str = TypeConverter.ToString(thisObj);

            var param = arguments.At(0);

            var form = "NFC";
           if (!param.IsUndefined())
           {
               form = TypeConverter.ToString(param);
           }

            var nf = NormalizationForm.FormC;
            switch (form)
            {
                case "NFC":
                    nf = NormalizationForm.FormC;
                    break;
                case "NFD":
                    nf = NormalizationForm.FormD;
                    break;
                case "NFKC":
                    nf = NormalizationForm.FormKC;
                    break;
                case "NFKD":
                    nf = NormalizationForm.FormKD;
                    break;
                default:
                    ExceptionHelper.ThrowRangeError(
                        _realm,
                        "The normalization form should be one of NFC, NFD, NFKC, NFKD.");
                    break;
            }

            return str.Normalize(nf);
        }

        private JsValue Repeat(JsValue thisObj, JsValue[] arguments)
        {
            TypeConverter.CheckObjectCoercible(Engine, thisObj);
            var str = TypeConverter.ToString(thisObj);
            var n = (int) TypeConverter.ToInteger(arguments.At(0));

            if (n < 0)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid count value");
            }

            if (n == 0 || str.Length == 0)
            {
                return JsString.Empty;
            }

            if (str.Length == 1)
            {
                return new string(str[0], n);
            }

            using (var sb = StringBuilderPool.Rent())
            {
                sb.Builder.EnsureCapacity(n * str.Length);
                for (var i = 0; i < n; ++i)
                {
                    sb.Builder.Append(str);
                }

                return sb.ToString();
            }
        }
    }
}
