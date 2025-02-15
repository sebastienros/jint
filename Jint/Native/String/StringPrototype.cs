#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Collections;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.String;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-string-prototype-object
/// </summary>
internal sealed class StringPrototype : StringInstance
{
    private readonly Realm _realm;
    private readonly StringConstructor _constructor;
    internal ClrFunction? _originalIteratorFunction;

    internal StringPrototype(
        Engine engine,
        Realm realm,
        StringConstructor constructor,
        ObjectPrototype objectPrototype)
        : base(engine, JsString.Empty)
    {
        _prototype = objectPrototype;
        _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
        _realm = realm;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        const PropertyFlag propertyFlags = lengthFlags | PropertyFlag.Writable;

        var trimStart = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "trimStart", prototype.TrimStart, 0, lengthFlags), propertyFlags);
        var trimEnd = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "trimEnd", prototype.TrimEnd, 0, lengthFlags), propertyFlags);
        var properties = new PropertyDictionary(37, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["toString"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "toString", prototype.ToStringString, 0, lengthFlags), propertyFlags),
            ["valueOf"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "valueOf", prototype.ValueOf, 0, lengthFlags), propertyFlags),
            ["charAt"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "charAt", prototype.CharAt, 1, lengthFlags), propertyFlags),
            ["charCodeAt"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "charCodeAt", prototype.CharCodeAt, 1, lengthFlags), propertyFlags),
            ["codePointAt"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "codePointAt", prototype.CodePointAt, 1, lengthFlags), propertyFlags),
            ["concat"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "concat", prototype.Concat, 1, lengthFlags), propertyFlags),
            ["indexOf"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "indexOf", prototype.IndexOf, 1, lengthFlags), propertyFlags),
            ["endsWith"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "endsWith", prototype.EndsWith, 1, lengthFlags), propertyFlags),
            ["startsWith"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "startsWith", prototype.StartsWith, 1, lengthFlags), propertyFlags),
            ["lastIndexOf"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "lastIndexOf", prototype.LastIndexOf, 1, lengthFlags), propertyFlags),
            ["localeCompare"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "localeCompare", prototype.LocaleCompare, 1, lengthFlags), propertyFlags),
            ["match"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "match", prototype.Match, 1, lengthFlags), propertyFlags),
            ["matchAll"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "matchAll", prototype.MatchAll, 1, lengthFlags), propertyFlags),
            ["replace"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "replace", prototype.Replace, 2, lengthFlags), propertyFlags),
            ["replaceAll"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "replaceAll", prototype.ReplaceAll, 2, lengthFlags), propertyFlags),
            ["search"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "search", prototype.Search, 1, lengthFlags), propertyFlags),
            ["slice"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "slice", prototype.Slice, 2, lengthFlags), propertyFlags),
            ["split"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "split", prototype.Split, 2, lengthFlags), propertyFlags),
            ["substr"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "substr", Substr, 2), propertyFlags),
            ["substring"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "substring", prototype.Substring, 2, lengthFlags), propertyFlags),
            ["toLowerCase"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "toLowerCase", prototype.ToLowerCase, 0, lengthFlags), propertyFlags),
            ["toLocaleLowerCase"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "toLocaleLowerCase", prototype.ToLocaleLowerCase, 0, lengthFlags), propertyFlags),
            ["toUpperCase"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "toUpperCase", prototype.ToUpperCase, 0, lengthFlags), propertyFlags),
            ["toLocaleUpperCase"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "toLocaleUpperCase", prototype.ToLocaleUpperCase, 0, lengthFlags), propertyFlags),
            ["trim"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "trim", prototype.Trim, 0, lengthFlags), propertyFlags),
            ["trimStart"] = trimStart,
            ["trimEnd"] = trimEnd,
            ["trimLeft"] = trimStart,
            ["trimRight"] = trimEnd,
            ["padStart"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "padStart", prototype.PadStart, 1, lengthFlags), propertyFlags),
            ["padEnd"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "padEnd", prototype.PadEnd, 1, lengthFlags), propertyFlags),
            ["includes"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "includes", prototype.Includes, 1, lengthFlags), propertyFlags),
            ["normalize"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "normalize", prototype.Normalize, 0, lengthFlags), propertyFlags),
            ["repeat"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "repeat", prototype.Repeat, 1, lengthFlags), propertyFlags),
            ["at"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "at", prototype.At, 1, lengthFlags), propertyFlags),
            ["isWellFormed"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "isWellFormed", prototype.IsWellFormed, 0, lengthFlags), propertyFlags),
            ["toWellFormed"] = new LazyPropertyDescriptor<StringPrototype>(this, static prototype => new ClrFunction(prototype._engine, "toWellFormed", prototype.ToWellFormed, 0, lengthFlags), propertyFlags),
        };
        SetProperties(properties);

        _originalIteratorFunction = new ClrFunction(_engine, "[Symbol.iterator]", Iterator, 0, lengthFlags);
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(_originalIteratorFunction, propertyFlags)
        };
        SetSymbols(symbols);
    }

    internal override bool HasOriginalIterator => ReferenceEquals(Get(GlobalSymbolRegistry.Iterator), _originalIteratorFunction);

    private ObjectInstance Iterator(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var str = TypeConverter.ToString(thisObject);
        return _realm.Intrinsics.StringIteratorPrototype.Construct(str);
    }

    private JsValue ToStringString(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject.IsString())
        {
            return thisObject;
        }

        var s = TypeConverter.ToObject(_realm, thisObject) as StringInstance;
        if (s is null)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        return s.StringData;
    }

    // http://msdn.microsoft.com/en-us/library/system.char.iswhitespace(v=vs.110).aspx
    // http://en.wikipedia.org/wiki/Byte_order_mark
    const char BOM_CHAR = '\uFEFF';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhiteSpaceEx(char c)
    {
        return char.IsWhiteSpace(c) || c == BOM_CHAR;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string TrimEndEx(string s)
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
    internal static string TrimStartEx(string s)
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
    internal static string TrimEx(string s)
    {
        return TrimEndEx(TrimStartEx(s));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.trim
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JsValue Trim(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var s = TypeConverter.ToJsString(thisObject);
        if (s.Length == 0 || (!IsWhiteSpaceEx(s[0]) && !IsWhiteSpaceEx(s[s.Length - 1])))
        {
            return s;
        }
        return TrimEx(s.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.trimstart
    /// </summary>
    private JsValue TrimStart(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var s = TypeConverter.ToJsString(thisObject);
        if (s.Length == 0 || !IsWhiteSpaceEx(s[0]))
        {
            return s;
        }
        return TrimStartEx(s.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.trimend
    /// </summary>
    private JsValue TrimEnd(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var s = TypeConverter.ToJsString(thisObject);
        if (s.Length == 0 || !IsWhiteSpaceEx(s[s.Length - 1]))
        {
            return s;
        }
        return TrimEndEx(s.ToString());
    }

    private JsValue ToLocaleUpperCase(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var s = TypeConverter.ToString(thisObject);
        var culture = CultureInfo.InvariantCulture;
        if (arguments.Length > 0 && arguments[0].IsString())
        {
            try
            {
                var cultureArgument = arguments[0].ToString();
                culture = CultureInfo.GetCultureInfo(cultureArgument);
            }
            catch (CultureNotFoundException)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Incorrect culture information provided");
            }
        }
        if (string.Equals("lt", culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            s = StringInlHelper.LithuanianStringProcessor(s);
#if NET462
            // Code specific to .NET Framework 4.6.2.
            // For no good reason this verison does not upper case these characters correctly.
            return new JsString(s.ToUpper(culture)
                .Replace("ϳ", "Ϳ")
                .Replace("ʝ", "Ʝ"));
#endif
        }

        return new JsString(s.ToUpper(culture));
    }

    private JsValue ToUpperCase(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var s = TypeConverter.ToString(thisObject);
        return new JsString(s.ToUpperInvariant());
    }

    private JsValue ToLocaleLowerCase(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var s = TypeConverter.ToString(thisObject);
        return new JsString(s.ToLower(CultureInfo.InvariantCulture));
    }

    private JsValue ToLowerCase(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var s = TypeConverter.ToString(thisObject);
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

    private JsValue Substring(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var s = TypeConverter.ToString(thisObject);
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

    private static JsValue Substr(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.split
    /// </summary>
    private JsValue Split(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var separator = arguments.At(0);
        var limit = arguments.At(1);

        // fast path for empty regexp
        if (separator is JsRegExp R && string.Equals(R.Source, JsRegExp.regExpForMatchingAllCharacters, StringComparison.Ordinal))
        {
            separator = JsString.Empty;
        }

        if (separator is ObjectInstance oi)
        {
            var splitter = GetMethod(_realm, oi, GlobalSymbolRegistry.Split);
            if (splitter != null)
            {
                return splitter.Call(separator, thisObject, limit);
            }
        }

        var s = TypeConverter.ToString(thisObject);

        // Coerce into a number, true will become 1
        var lim = limit.IsUndefined() ? uint.MaxValue : TypeConverter.ToUint32(limit);

        if (separator.IsNull())
        {
            separator = "null";
        }
        else if (!separator.IsUndefined())
        {
            if (!separator.IsRegExp())
            {
                separator = TypeConverter.ToJsString(separator); // Coerce into a string, for an object call toString()
            }
        }

        if (lim == 0)
        {
            return _realm.Intrinsics.Array.ArrayCreate(0);
        }

        if (separator.IsUndefined())
        {
            var arrayInstance = _realm.Intrinsics.Array.ArrayCreate(1);
            arrayInstance.SetIndexValue(0, s, updateLength: false);
            return arrayInstance;
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
        var a = realm.Intrinsics.Array.ArrayCreate(length);
        for (int i = 0; i < length; i++)
        {
            a.SetIndexValue((uint) i, segments[i], updateLength: false);
        }

        a.SetLength(length);
        return a;
    }

    /// <summary>
    /// https://tc39.es/proposal-relative-indexing-method/#sec-string-prototype-additions
    /// </summary>
    private JsValue At(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var start = arguments.At(0);

        var o = thisObject.ToString();
        long len = o.Length;

        var relativeIndex = TypeConverter.ToInteger(start);
        int k;

        if (relativeIndex < 0)
        {
            k = (int) (len + relativeIndex);
        }
        else
        {
            k = (int) relativeIndex;
        }

        if (k < 0 || k >= len)
        {
            return Undefined;
        }

        return o[k];
    }

    private JsValue Slice(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var start = TypeConverter.ToNumber(arguments.At(0));
        if (double.IsNegativeInfinity(start))
        {
            start = 0;
        }
        if (double.IsPositiveInfinity(start))
        {
            return JsString.Empty;
        }

        var s = TypeConverter.ToJsString(thisObject);
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

        return s.Substring(from, span);
    }

    private JsValue Search(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var regex = arguments.At(0);

        if (regex is ObjectInstance oi)
        {
            var searcher = GetMethod(_realm, oi, GlobalSymbolRegistry.Search);
            if (searcher != null)
            {
                return searcher.Call(regex, thisObject);
            }
        }

        var rx = (JsRegExp) _realm.Intrinsics.RegExp.Construct(new[] {regex});
        var s = TypeConverter.ToJsString(thisObject);
        return _engine.Invoke(rx, GlobalSymbolRegistry.Search, [s]);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.replace
    /// </summary>
    private JsValue Replace(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var searchValue = arguments.At(0);
        var replaceValue = arguments.At(1);

        if (!searchValue.IsNullOrUndefined())
        {
            var replacer = GetMethod(_realm, searchValue, GlobalSymbolRegistry.Replace);
            if (replacer != null)
            {
                return replacer.Call(searchValue, thisObject, replaceValue);
            }
        }

        var thisString = TypeConverter.ToJsString(thisObject);
        var searchString = TypeConverter.ToString(searchValue);
        var functionalReplace = replaceValue is ICallable;

        if (!functionalReplace)
        {
            replaceValue = TypeConverter.ToJsString(replaceValue);
        }

        var position = thisString.IndexOf(searchString);
        if (position < 0)
        {
            return thisString;
        }

        string replStr;
        if (functionalReplace)
        {
            var replValue = ((ICallable) replaceValue).Call(Undefined, searchString, position, thisString);
            replStr = TypeConverter.ToString(replValue);
        }
        else
        {
            var captures = System.Array.Empty<string>();
            replStr =  RegExpPrototype.GetSubstitution(searchString, thisString.ToString(), position, captures, Undefined, TypeConverter.ToString(replaceValue));
        }

        var tailPos = position + searchString.Length;
        var newString = thisString.Substring(0, position) + replStr + thisString.Substring(tailPos);

        return newString;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.replaceall
    /// </summary>
    private JsValue ReplaceAll(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var searchValue = arguments.At(0);
        var replaceValue = arguments.At(1);

        if (!searchValue.IsNullOrUndefined())
        {
            if (searchValue.IsRegExp())
            {
                var flags = searchValue.Get(RegExpPrototype.PropertyFlags);
                TypeConverter.RequireObjectCoercible(_engine, flags);
                if (!TypeConverter.ToString(flags).Contains('g'))
                {
                    ExceptionHelper.ThrowTypeError(_realm, "String.prototype.replaceAll called with a non-global RegExp argument");
                }
            }

            var replacer = GetMethod(_realm, searchValue, GlobalSymbolRegistry.Replace);
            if (replacer != null)
            {
                return replacer.Call(searchValue, thisObject, replaceValue);
            }
        }

        var thisString = TypeConverter.ToString(thisObject);
        var searchString = TypeConverter.ToString(searchValue);

        var functionalReplace = replaceValue is ICallable;

        if (!functionalReplace)
        {
            replaceValue = TypeConverter.ToJsString(replaceValue);

            // check fast case
            var newValue = replaceValue.ToString();
            if (!newValue.Contains('$') && searchString.Length > 0)
            {
                // just plain old string replace
                return thisString.Replace(searchString, newValue);
            }
        }

        // https://tc39.es/ecma262/#sec-stringindexof
        static int StringIndexOf(string s, string search, int fromIndex)
        {
            if (search.Length == 0 && fromIndex <= s.Length)
            {
                return fromIndex;
            }

            return fromIndex < s.Length
                ? s.IndexOf(search, fromIndex, StringComparison.Ordinal)
                : -1;
        }

        var searchLength = searchString.Length;
        var advanceBy = System.Math.Max(1, searchLength);

        var endOfLastMatch = 0;
        using var result = new ValueStringBuilder();

        var position = StringIndexOf(thisString, searchString, 0);
        while (position != -1)
        {
            string replacement;
            var preserved = thisString.Substring(endOfLastMatch, position - endOfLastMatch);
            if (functionalReplace)
            {
                var replValue = ((ICallable) replaceValue).Call(Undefined, searchString, position, thisString);
                replacement = TypeConverter.ToString(replValue);
            }
            else
            {
                var captures = System.Array.Empty<string>();
                replacement =  RegExpPrototype.GetSubstitution(searchString, thisString, position, captures, Undefined, TypeConverter.ToString(replaceValue));
            }

            result.Append(preserved);
            result.Append(replacement);

            endOfLastMatch = position + searchLength;

            position = StringIndexOf(thisString, searchString, position + advanceBy);
        }

        if (endOfLastMatch < thisString.Length)
        {
            result.Append(thisString.AsSpan(endOfLastMatch));
        }

        return result.ToString();
    }

    private JsValue Match(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var regex = arguments.At(0);
        if (regex is ObjectInstance oi)
        {
            var matcher = GetMethod(_realm, oi, GlobalSymbolRegistry.Match);
            if (matcher != null)
            {
                return matcher.Call(regex, thisObject);
            }
        }

        var rx = (JsRegExp) _realm.Intrinsics.RegExp.Construct(new[] {regex});

        var s = TypeConverter.ToJsString(thisObject);
        return _engine.Invoke(rx, GlobalSymbolRegistry.Match, [s]);
    }

    private JsValue MatchAll(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);

        var regex = arguments.At(0);
        if (!regex.IsNullOrUndefined())
        {
            if (regex.IsRegExp())
            {
                var flags = regex.Get(RegExpPrototype.PropertyFlags);
                TypeConverter.RequireObjectCoercible(_engine, flags);
                if (!TypeConverter.ToString(flags).Contains('g'))
                {
                    ExceptionHelper.ThrowTypeError(_realm);
                }
            }
            var matcher = GetMethod(_realm, (ObjectInstance) regex, GlobalSymbolRegistry.MatchAll);
            if (matcher != null)
            {
                return matcher.Call(regex, thisObject);
            }
        }

        var s = TypeConverter.ToJsString(thisObject);
        var rx = (JsRegExp) _realm.Intrinsics.RegExp.Construct(new[] { regex, "g" });

        return _engine.Invoke(rx, GlobalSymbolRegistry.MatchAll, [s]);
    }

    private JsValue LocaleCompare(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var s = TypeConverter.ToString(thisObject);
        var that = TypeConverter.ToString(arguments.At(0));

        var culture = Engine.Options.Culture;

        if (arguments.Length > 1 && arguments[1].IsString())
        {
            culture = CultureInfo.GetCultureInfo(arguments.At(1).AsString());
        }

        return culture.CompareInfo.Compare(s.Normalize(NormalizationForm.FormKD), that.Normalize(NormalizationForm.FormKD));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.lastindexof
    /// </summary>
    private JsValue LastIndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var jsString = TypeConverter.ToJsString(thisObject);
        var searchStr = TypeConverter.ToString(arguments.At(0));
        double numPos = double.NaN;
        if (arguments.Length > 1 && !arguments[1].IsUndefined())
        {
            numPos = TypeConverter.ToNumber(arguments[1]);
        }

        var pos = double.IsNaN(numPos) ? double.PositiveInfinity : TypeConverter.ToInteger(numPos);

        var len = jsString.Length;
        var start = (int)System.Math.Min(System.Math.Max(pos, 0), len);
        var searchLen = searchStr.Length;

        if (searchLen > len)
        {
            return JsNumber.IntegerNegativeOne;
        }

        var s = jsString.ToString();
        var i = start;
        bool found;

        do
        {
            found = true;
            var j = 0;

            while (found && j < searchLen)
            {
                if (i + searchLen > len || s[i + j] != searchStr[j])
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.indexof
    /// </summary>
    private JsValue IndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var s = TypeConverter.ToJsString(thisObject);
        var searchStr = TypeConverter.ToString(arguments.At(0));
        double pos = 0;
        if (arguments.Length > 1 && !arguments[1].IsUndefined())
        {
            pos = TypeConverter.ToInteger(arguments[1]);
        }

        if (pos > s.Length)
        {
            pos = s.Length;
        }

        if (pos < 0)
        {
            pos = 0;
        }

        return s.IndexOf(searchStr, (int) pos);
    }

    private JsValue Concat(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        if (thisObject is not JsString jsString)
        {
            jsString = new JsString.ConcatenatedString(TypeConverter.ToString(thisObject));
        }
        else
        {
            jsString = jsString.EnsureCapacity(0);
        }

        foreach (var argument in arguments)
        {
            jsString = jsString.Append(argument);
        }

        return jsString;
    }

    private JsValue CharCodeAt(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        JsValue pos = arguments.Length > 0 ? arguments[0] : 0;
        var s = TypeConverter.ToJsString(thisObject);
        var position = (int) TypeConverter.ToInteger(pos);
        if (position < 0 || position >= s.Length)
        {
            return JsNumber.DoubleNaN;
        }
        return (long) s[position];
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.codepointat
    /// </summary>
    private JsValue CodePointAt(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        JsValue pos = arguments.Length > 0 ? arguments[0] : 0;
        var s = TypeConverter.ToString(thisObject);
        var position = (int)TypeConverter.ToInteger(pos);
        if (position < 0 || position >= s.Length)
        {
            return Undefined;
        }

        return CodePointAt(s, position).CodePoint;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct CodePointResult(int CodePoint, int CodeUnitCount, bool IsUnpairedSurrogate);

    private static CodePointResult CodePointAt(string s, int position)
    {
        var size = s.Length;
        var first = s.CharCodeAt(position);
        var cp = s.CharCodeAt(position);

        var firstIsLeading = char.IsHighSurrogate(first);
        var firstIsTrailing = char.IsLowSurrogate(first);
        if (!firstIsLeading && !firstIsTrailing)
        {
            return new CodePointResult(cp, 1, false);
        }

        if (firstIsTrailing || position + 1 == size)
        {
            return new CodePointResult(cp, 1, true);
        }

        var second = s.CharCodeAt(position + 1);
        if (!char.IsLowSurrogate(second))
        {
            return new CodePointResult(cp, 1, true);
        }

        return new CodePointResult(char.ConvertToUtf32(first, second), 2, false);
    }

    private JsValue CharAt(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var s = TypeConverter.ToJsString(thisObject);
        var position = TypeConverter.ToInteger(arguments.At(0));
        var size = s.Length;
        if (position >= size || position < 0)
        {
            return JsString.Empty;
        }
        return JsString.Create(s[(int) position]);
    }

    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is StringInstance si)
        {
            return si.StringData;
        }

        if (thisObject is JsString)
        {
            return thisObject;
        }

        ExceptionHelper.ThrowTypeError(_realm);
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.padstart
    /// </summary>
    private JsValue PadStart(JsValue thisObject, JsCallArguments arguments)
    {
        return StringPad(thisObject, arguments, true);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.padend
    /// </summary>
    private JsValue PadEnd(JsValue thisObject, JsCallArguments arguments)
    {
        return StringPad(thisObject, arguments, false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-stringpad
    /// </summary>
    private JsValue StringPad(JsValue thisObject, JsCallArguments arguments, bool padStart)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var s = TypeConverter.ToJsString(thisObject);

        var targetLength = TypeConverter.ToInt32(arguments.At(0));
        var padStringValue = arguments.At(1);

        var padString = padStringValue.IsUndefined()
            ? " "
            : TypeConverter.ToString(padStringValue);

        if (s.Length > targetLength || padString.Length == 0)
        {
            return s;
        }

        targetLength -= s.Length;
        if (targetLength > padString.Length)
        {
            padString = string.Join("", System.Linq.Enumerable.Repeat(padString, (targetLength / padString.Length) + 1));
        }

        return padStart
            ? $"{padString.Substring(0, targetLength)}{s}"
            : $"{s}{padString.Substring(0, targetLength)}";
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.startswith
    /// </summary>
    private JsValue StartsWith(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var s = TypeConverter.ToJsString(thisObject);

        var searchString = arguments.At(0);
        if (ReferenceEquals(searchString, Null))
        {
            searchString = "null";
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

        return s.StartsWith(searchStr, start);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.endswith
    /// </summary>
    private JsValue EndsWith(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var s = TypeConverter.ToJsString(thisObject);

        var searchString = arguments.At(0);
        if (ReferenceEquals(searchString, Null))
        {
            searchString = "null";
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

        return s.EndsWith(searchStr, end);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.includes
    /// </summary>
    private JsValue Includes(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);

        var s = TypeConverter.ToJsString(thisObject);
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
            return JsBoolean.True;
        }

        if (pos < 0)
        {
            pos = 0;
        }

        return s.IndexOf(searchStr, (int) pos) > -1;
    }

    private JsValue Normalize(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var str = TypeConverter.ToString(thisObject);

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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.repeat
    /// </summary>
    private JsValue Repeat(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(Engine, thisObject);
        var s = TypeConverter.ToString(thisObject);
        var count = arguments.At(0);

        var n = TypeConverter.ToIntegerOrInfinity(count);

        if (n < 0 || double.IsPositiveInfinity(n))
        {
            ExceptionHelper.ThrowRangeError(_realm, "Invalid count value");
        }

        if (n == 0 || s.Length == 0)
        {
            return JsString.Empty;
        }

        if (s.Length == 1)
        {
            return new string(s[0], (int) n);
        }

        var sb = new ValueStringBuilder((int) (n * s.Length));
        for (var i = 0; i < n; ++i)
        {
            sb.Append(s);
        }

        return sb.ToString();
    }

    private JsValue IsWellFormed(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var s = TypeConverter.ToString(thisObject);

        return IsStringWellFormedUnicode(s);
    }

    private JsValue ToWellFormed(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var s = TypeConverter.ToString(thisObject);

        var strLen = s.Length;
        var k = 0;

        var result = new ValueStringBuilder();
        while (k < strLen)
        {
            var cp = CodePointAt(s, k);
            if (cp.IsUnpairedSurrogate)
            {
                // \uFFFD
                result.Append('�');
            }
            else
            {
                result.Append(s.AsSpan(k, cp.CodeUnitCount));
            }
            k += cp.CodeUnitCount;
        }

        return result.ToString();
    }

    private static bool IsStringWellFormedUnicode(string s)
    {
        for (var i = 0; i < s.Length; ++i)
        {
            var isSurrogate = (s.CharCodeAt(i) & 0xF800) == 0xD800;
            if (!isSurrogate)
            {
                continue;
            }

            var isLeadingSurrogate = s.CharCodeAt(i) < 0xDC00;
            if (!isLeadingSurrogate)
            {
                return false; // unpaired trailing surrogate
            }

            var isFollowedByTrailingSurrogate = i + 1 < s.Length && (s.CharCodeAt(i + 1) & 0xFC00) == 0xDC00;
            if (!isFollowedByTrailingSurrogate)
            {
                return false; // unpaired leading surrogate
            }

            ++i;
        }

        return true;
    }
}
