using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// A JavaScript string value.
/// </summary>
/// <remarks>
/// <para>
/// This type is designed to be subclassed so that a host can expose its own string representation
/// (a native handle, an encoded buffer, a view over a larger buffer) without eagerly producing a
/// .NET <see cref="string"/>. Jint's own sliced and concatenated string representations use the same
/// mechanism.
/// </para>
/// <para>
/// <b>Subclassing contract.</b> Passing a <see langword="null"/> backing value to the constructor is
/// supported and is the intended way to express "not materialized yet". A subclass that does so
/// <b>must</b> override every member that would otherwise observe the backing value:
/// <list type="bullet">
/// <item><see cref="ToString()"/> — produces (and normally caches) the flat value. Every other
/// member of this class that needs the text routes through it, so overriding it alone is enough for
/// correctness; the remaining overrides exist only to avoid materializing.</item>
/// <item><see cref="Length"/> — must answer without materializing, otherwise a length comparison
/// (which <see cref="Equals(JsString)"/> performs first) defeats the laziness.</item>
/// <item><see cref="this[int]"/> — character access.</item>
/// <item><see cref="Equals(JsString)"/>, <see cref="Equals(string)"/> and
/// <see cref="GetHashCode()"/> — only needed to stay allocation-free; the base implementations are
/// correct but materialize. An overridden <see cref="GetHashCode()"/> must produce the same hash as
/// the equivalent flat string (<see cref="StringComparer.Ordinal"/>), otherwise the value cannot be
/// used interchangeably as a property key or collection key.</item>
/// </list>
/// </para>
/// <para>
/// A subclass that leaves the backing value <see langword="null"/> and does not override
/// <see cref="ToString()"/> is not usable — the base implementation returns
/// <see langword="null"/>.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString()}")]
public class JsString : JsValue, IEquatable<JsString>, IEquatable<string>
{
    private const int AsciiMax = 126;
    private static readonly JsString[] _charToJsValue;
    private static readonly JsString[] _charToStringJsValue;
    private static readonly JsString[] _intToStringJsValue;

    public static readonly JsString Empty;
    internal static readonly JsString NullString;
    internal static readonly JsString UndefinedString;
    internal static readonly JsString ObjectString;
    internal static readonly JsString FunctionString;
    internal static readonly JsString BooleanString;
    internal static readonly JsString StringString;
    internal static readonly JsString NumberString;
    internal static readonly JsString BigIntString;
    internal static readonly JsString SymbolString;
    internal static readonly JsString DefaultString;
    internal static readonly JsString NumberZeroString;
    internal static readonly JsString NumberOneString;
    internal static readonly JsString TrueString;
    internal static readonly JsString FalseString;
    internal static readonly JsString LengthString;
    internal static readonly JsValue CommaString;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal string _value;

    private static readonly ConcurrentDictionary<string, JsString> _stringCache;

    static JsString()
    {
        _charToJsValue = new JsString[AsciiMax + 1];
        _charToStringJsValue = new JsString[AsciiMax + 1];

        for (var i = 0; i <= AsciiMax; i++)
        {
            _charToJsValue[i] = new JsString((char) i);
            _charToStringJsValue[i] = new JsString(((char) i).ToString());
        }

        _intToStringJsValue = new JsString[1024];
        for (var i = 0; i < _intToStringJsValue.Length; ++i)
        {
            _intToStringJsValue[i] = new JsString(TypeConverter.ToString(i));
        }


        _stringCache = new ConcurrentDictionary<string, JsString>(StringComparer.Ordinal);
        Empty = new JsString("", InternalTypes.String);
        NullString = CachedCreate("null");
        UndefinedString = CachedCreate("undefined");
        ObjectString = CachedCreate("object");
        FunctionString = CachedCreate("function");
        BooleanString = CachedCreate("boolean");
        StringString = CachedCreate("string");
        NumberString = CachedCreate("number");
        BigIntString = CachedCreate("bigint");
        SymbolString = CachedCreate("symbol");
        DefaultString = CachedCreate("default");
        NumberZeroString = CachedCreate("0");
        NumberOneString = CachedCreate("1");
        TrueString = CachedCreate("true");
        FalseString = CachedCreate("false");
        LengthString = CachedCreate("length");
        CommaString = CachedCreate(",");
    }

    public JsString(string value) : this(value, InternalTypes.String)
    {
    }

    private JsString(string value, InternalTypes type) : base(type)
    {
        _value = value;
    }

    public JsString(char value) : base(Types.String)
    {
        _value = value.ToString();
    }

    public static bool operator ==(JsString? a, JsString? b)
    {
        if (a is not null)
        {
            return a.Equals(b);
        }

        if (a is null)
        {
            return b is null;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator ==(JsValue? a, JsString? b)
    {
        if (a is JsString s && b is not null)
        {
            return s.Equals(b);
        }

        if (a is null)
        {
            return b is null;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator ==(JsString? a, JsValue? b)
    {
        if (a is not null)
        {
            return a.Equals(b);
        }

        return b is null;
    }

    public static bool operator !=(JsString a, JsValue b)
    {
        return !(a == b);
    }

    public static bool operator ==(JsString? a, string? b)
    {
        if (a is not null)
        {
            return a.Equals(b);
        }

        return b is null;
    }

    public static bool operator !=(JsString? a, string? b)
    {
        return !(a == b);
    }

    public static bool operator !=(JsValue a, JsString b)
    {
        return !(a == b);
    }

    public static bool operator !=(JsString a, JsString b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Creates a <see cref="JsString"/> for a .NET string. The counterpart of <see cref="JsNumber.Create(double)"/>,
    /// and the preferred way for a host to produce a string value: it returns a shared interned instance for the
    /// empty string and for single-ASCII-character strings instead of allocating a new one.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A <see cref="JsString"/> for <paramref name="value" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
    public static JsString Create(string value)
    {
        if (value is null)
        {
            Throw.ArgumentNullException(nameof(value));
        }

        if (value.Length > 1)
        {
            return new JsString(value);
        }

        if (value.Length == 0)
        {
            return Empty;
        }

        var i = (uint) value[0];
        var temp = _charToStringJsValue;
        if (i < (uint) temp.Length)
        {
            return temp[i];
        }
        return new JsString(value);
    }

    internal static JsString CachedCreate(string value)
    {
        if (value.Length is < 2 or > 10)
        {
            return Create(value);
        }

        return _stringCache.GetOrAdd(value, static x => new JsString(x));
    }

    internal static JsString Create(char value)
    {
        var temp = _charToJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(value);
    }

    // A zero-copy view pins the whole source string, so a slice only becomes a view when retention
    // stays bounded: either it covers at least half the source (≤ 2× the result is pinned), or the
    // bytes it pins but never uses (source.Length - length) stay within this fixed budget. The latter
    // catches a moderate slice of a large source — e.g. substring(12000, -1) of a ~128K string, the
    // dromaeo-object-string shape — which otherwise copies on every call.
    private const int SliceViewMaxWastedChars = 128 * 1024;

    /// <summary>
    /// Creates a string for a substring of <paramref name="source"/>. Large slices that cover most of
    /// the source, or moderate slices whose unused pinned remainder stays within
    /// <see cref="SliceViewMaxWastedChars"/>, are returned as zero-copy views (<see cref="SlicedString"/>);
    /// smaller slices are copied so a short-lived view can never pin a much larger backing string.
    /// </summary>
    internal static JsString CreateSliced(string source, int start, int length)
    {
        if (start == 0 && length == source.Length)
        {
            // Create keeps 0/1-char sources on the cached instances (e.g. ''.split('x'))
            return Create(source);
        }

        if (length >= 512
            && (length * 2 >= source.Length || source.Length - length <= SliceViewMaxWastedChars))
        {
            return new SlicedString(source, start, length);
        }

        // Copy fallback goes through Create so short results (e.g. single-char or empty split
        // segments) still hit the cached single-char / empty instances. slice/substring/substr
        // never reach here with length < 2 (they short-circuit first), so Create is equivalent to
        // new JsString for those callers.
        return Create(source.Substring(start, length));
    }

    /// <summary>
    /// Creates a string for a substring of <paramref name="source"/>. When <paramref name="source"/>
    /// is a not-yet-materialized <see cref="SlicedString"/>, the slice is rebased directly onto the
    /// original backing string so a slice-of-slice never materializes the intermediate view, and the
    /// retention policy is evaluated against that original backing string so chained views cannot
    /// compound the pinning of a large source. A materialized view or any other receiver is treated
    /// as flat.
    /// </summary>
    internal static JsString CreateSliced(JsString source, int start, int length)
    {
        // A view still carrying its lazy (unmaterialized) value is rebased onto its own backing
        // string: offset past the view's own start and let the policy weigh retention against the
        // original source. Once a view has materialized its flat value, reusing that string is
        // cheaper than pinning the (possibly much larger) original backing string, so fall through.
        if (source is SlicedString { _value: null } sliced)
        {
            return CreateSliced(sliced._source, sliced._start + start, length);
        }

        return CreateSliced(source.ToString(), start, length);
    }

    internal static JsString Create(int value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(TypeConverter.ToString(value));
    }

    internal static JsValue Create(uint value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(TypeConverter.ToString(value));
    }

    internal static JsValue Create(ulong value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(TypeConverter.ToString(value));
    }


    public virtual char this[int index] => _value[index];

    public virtual int Length => _value.Length;

    internal virtual JsString Append(JsValue jsValue)
    {
        return new ConcatenatedString(string.Concat(ToString(), TypeConverter.ToString(jsValue)));
    }

    internal virtual JsString EnsureCapacity(int capacity)
    {
        // ToString() rather than _value: a subclass can carry a null backing value until it
        // materializes, and ConcatenatedString cannot start from null. For a flat string
        // ToString() just returns _value, so the common path is unchanged.
        return new ConcatenatedString(ToString(), capacity);
    }

    public sealed override object ToObject() => ToString();

    internal sealed override bool ToBoolean()
    {
        return Length > 0;
    }

    public override string ToString() => _value;

    internal virtual bool Contains(char c)
    {
        if (c == 0)
        {
            return false;
        }
        return ToString().Contains(c);
    }

    internal virtual int IndexOf(string value, int startIndex = 0)
    {
        if (Length - startIndex < value.Length)
        {
            return -1;
        }
        return ToString().IndexOf(value, startIndex, StringComparison.Ordinal);
    }

    internal virtual bool StartsWith(string value, int start = 0)
    {
        return value.Length + start <= Length && ToString().AsSpan(start).StartsWith(value.AsSpan(), StringComparison.Ordinal);
    }

    internal virtual bool EndsWith(string value, int end = 0)
    {
        var start = end - value.Length;
        return start >= 0 && ToString().AsSpan(start, value.Length).EndsWith(value.AsSpan(), StringComparison.Ordinal);
    }

    internal string Substring(int startIndex, int length)
    {
        return ToString().Substring(startIndex, length);
    }

    internal string Substring(int startIndex)
    {
        return ToString().Substring(startIndex);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getiterator — the @@iterator lookup for a primitive string receiver,
    /// with the fast lane that iterates the text directly instead of driving %StringIteratorPrototype%.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exactly one read of @@iterator, and its result is what picks the lane.</b> The lane used to be
    /// chosen by <c>StringPrototype.HasOriginalIterator</c>, which performed a read of its own and then let
    /// the general path read again, so an accessor installed on <c>String.prototype[Symbol.iterator]</c> saw
    /// two gets for one <c>Array.from("hello")</c> where the specification and every other engine produce one.
    /// </para>
    /// <para>
    /// The read resolves off <c>String.prototype</c> with this string as the receiver, which is what
    /// <c>GetV(V, @@iterator)</c> asks for: the wrapper <c>ToObject(V)</c> would build is fresh and carries no
    /// own @@iterator, so the property resolves at the same place either way — and a getter observes the
    /// primitive as its <c>this</c>, as it does in V8, rather than a wrapper this lane never needed to build.
    /// </para>
    /// <para>
    /// A caller that has already resolved the method — <c>Array.from</c> and <c>Iterator.from</c> both perform
    /// <c>GetMethod</c> first — hands it in, and classifying it by identity costs no read at all. That is the
    /// second half of the fix: re-reading to answer "is this still the original?" was the extra get.
    /// </para>
    /// </remarks>
    internal override bool TryGetIterator(
        Realm realm,
        [NotNullWhen(true)] out IteratorInstance? iterator,
        GeneratorKind hint = GeneratorKind.Sync,
        ICallable? method = null)
    {
        // The async hint looks up @@asyncIterator first and has no string fast lane of its own.
        if (hint == GeneratorKind.Sync)
        {
            var stringPrototype = realm.Intrinsics.String.PrototypeObject;

            if (method is null)
            {
                var iteratorMethod = stringPrototype.Get(GlobalSymbolRegistry.Iterator, this);
                if (ReferenceEquals(iteratorMethod, stringPrototype._originalIteratorFunction))
                {
                    iterator = new IteratorInstance.StringIterator(stringPrototype.Engine, ToString());
                    return true;
                }

                if (iteratorMethod.IsNullOrUndefined())
                {
                    iterator = null;
                    return false;
                }

                method = iteratorMethod as ICallable;
                if (method is null)
                {
                    Throw.TypeError(realm, $"Value returned for property '{GlobalSymbolRegistry.Iterator}' of object is not a function");
                }
            }
            else if (ReferenceEquals(method, stringPrototype._originalIteratorFunction))
            {
                iterator = new IteratorInstance.StringIterator(stringPrototype.Engine, ToString());
                return true;
            }
        }

        return base.TryGetIterator(realm, out iterator, hint, method);
    }

    public sealed override bool Equals(object? obj) => Equals(obj as JsString);

    public sealed override bool Equals(JsValue? other) => Equals(other as JsString);

    public virtual bool Equals(string? other) => other != null && string.Equals(ToString(), other, StringComparison.Ordinal);

    public virtual bool Equals(JsString? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Length != other.Length)
        {
            // every Length override answers without materializing, so a mismatched compare
            // (e.g. a short literal against a large unmaterialized view) stays allocation-free
            return false;
        }

        // A present backing value is the flat text, so it is compared directly; only a subclass that
        // has not materialized yet pays the virtual ToString(). See the note on GetHashCode for why
        // reading the field here is safe and why the pattern must not be copied elsewhere.
        var value = _value;
        return string.Equals(value ?? ToString(), other.ToString(), StringComparison.Ordinal);
    }

    protected internal override bool IsLooselyEqual(JsValue value)
    {
        if (value is JsString jsString)
        {
            return Equals(jsString);
        }

        if (value.IsBigInt())
        {
            return value.IsBigInt() && TypeConverter.TryStringToBigInt(ToString(), out var temp) && temp == value.AsBigInt();
        }

        return base.IsLooselyEqual(value);
    }

    // A null backing value means a subclass has not materialized yet, and only then is the virtual
    // ToString() needed (it hashes the content instead of throwing); a flat string hashes its field
    // directly, which is what every '===' and every Map/Set probe on a plain string pays for.
    //
    // INVARIANT, and the reason this null-test may NOT be copied into other members: a dirty
    // ConcatenatedString carries a non-null but STALE _value (the appends live in its StringBuilder
    // until ToString() flushes them). Reading the field is correct here only because
    // ConcatenatedString overrides every member that does so — ToString, Length, this[int],
    // Equals(string), Equals(JsString) and GetHashCode — so these base bodies are unreachable for
    // it. Any member ConcatenatedString does not override must keep routing through ToString().
    public override int GetHashCode()
    {
        var value = _value;
        return StringComparer.Ordinal.GetHashCode(value ?? ToString());
    }

    internal sealed class ConcatenatedString : JsString
    {
        private StringBuilder? _stringBuilder;
        private bool _dirty;

        internal ConcatenatedString(string value, int capacity = 0)
            : base(value, InternalTypes.String | InternalTypes.RequiresCloning)
        {
            if (capacity > 0)
            {
                _stringBuilder = new StringBuilder(value, capacity);
            }
            else
            {
                _value = value;
            }
        }

        public override string ToString()
        {
            if (_dirty)
            {
                _value = _stringBuilder!.ToString();
                _dirty = false;
            }

            return _value;
        }

        public override char this[int index] => _stringBuilder?[index] ?? _value[index];

        internal override JsString Append(JsValue jsValue)
        {
            var value = TypeConverter.ToString(jsValue);
            if (_stringBuilder == null)
            {
                _stringBuilder = new StringBuilder(_value, _value.Length + value.Length);
            }

            _stringBuilder.Append(value);
            _dirty = true;

            return this;
        }

        // No override of EnsureCapacity: the inherited one hands back a fresh instance seeded with
        // this value's text. Growing this instance's buffer and returning "this" instead would let
        // the caller append into a value that is still reachable from wherever it was read, and
        // appending mutates in place -- so the receiver of the concatenation would change too.
        // The buffer is also only created on the first append, so a value produced by a single
        // concatenation has none to grow.

        public override int Length => _stringBuilder?.Length ?? _value?.Length ?? 0;

        public override bool Equals(string? s)
        {
            if (s is null || Length != s.Length)
            {
                return false;
            }

            // we cannot use StringBuilder.Equals as it also checks Capacity on full framework / pre .NET Core 3
            if (_stringBuilder != null)
            {
                for (var i = 0; i < _stringBuilder.Length; ++i)
                {
                    if (_stringBuilder[i] != s[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return string.Equals(_value, s, StringComparison.Ordinal);
        }

        public override bool Equals(JsString? other)
        {
            if (other is ConcatenatedString cs)
            {
                var stringBuilder = _stringBuilder;
                var csStringBuilder = cs._stringBuilder;

                // we cannot use StringBuilder.Equals as it also checks Capacity on full framework / pre .NET Core 3
                if (stringBuilder != null && csStringBuilder != null && stringBuilder.Length == csStringBuilder.Length)
                {
                    for (var i = 0; i < stringBuilder.Length; ++i)
                    {
                        if (stringBuilder[i] != csStringBuilder[i])
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return string.Equals(ToString(), cs.ToString(), StringComparison.Ordinal);
            }

            if (other is null || other.Length != Length)
            {
                return false;
            }

            return string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        // Hash the content, exactly like the flat base implementation: StringBuilder does not
        // override GetHashCode, so hashing the buffer would hash its identity and an equal value
        // would land in a different bucket than the flat string Equals says it matches.
        // Materializing is unavoidable for a content hash, and Equals already materializes, so no
        // previously allocation-free path becomes allocating.
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ToString());

        internal override JsValue DoClone()
        {
            return new JsString(ToString());
        }
    }

    /// <summary>
    /// Zero-copy view over a section of a backing string, produced by slice/substring/substr
    /// for large results. Immutable (unlike <see cref="ConcatenatedString"/>), so it never
    /// requires cloning; the flat value is materialized lazily on first <see cref="ToString"/>.
    /// </summary>
    internal sealed class SlicedString : JsString
    {
        // internal (not private) so the enclosing JsString.CreateSliced can rebase a slice-of-slice
        // directly onto this view's backing string without materializing the intermediate.
        internal readonly string _source;
        internal readonly int _start;
        private readonly int _length;

        internal SlicedString(string source, int start, int length)
            : base(null!, InternalTypes.String)
        {
            _source = source;
            _start = start;
            _length = length;
        }

        public override string ToString()
        {
            return _value ??= _source.Substring(_start, _length);
        }

        public override char this[int index] => _source[_start + index];

        public override int Length => _length;

        private ReadOnlySpan<char> AsSpan() => _value is not null ? _value.AsSpan() : _source.AsSpan(_start, _length);

        // Search directly over the slice's span. The inherited base implementations route through
        // ToString(), which materializes (and allocates) the whole substring on every search; these
        // overrides keep a discarded slice zero-copy. Ordinal char comparison is binary/sequence
        // equality, so the parameterless span overloads match the base StringComparison.Ordinal paths.
        internal override int IndexOf(string value, int startIndex = 0)
        {
            if (_length - startIndex < value.Length)
            {
                return -1;
            }

            var index = AsSpan().Slice(startIndex).IndexOf(value.AsSpan());
            return index < 0 ? -1 : index + startIndex;
        }

        internal override bool StartsWith(string value, int start = 0)
        {
            return value.Length + start <= _length && AsSpan().Slice(start).StartsWith(value.AsSpan());
        }

        internal override bool EndsWith(string value, int end = 0)
        {
            var start = end - value.Length;
            return start >= 0 && AsSpan().Slice(start, value.Length).EndsWith(value.AsSpan());
        }

        internal override bool Contains(char c)
        {
            return c != 0 && AsSpan().IndexOf(c) >= 0;
        }

        public override bool Equals(string? other)
        {
            return other is not null && _length == other.Length && AsSpan().SequenceEqual(other.AsSpan());
        }

        public override bool Equals(JsString? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other.Length != _length)
            {
                return false;
            }

            var otherSpan = other is SlicedString otherSlice ? otherSlice.AsSpan() : other.ToString().AsSpan();
            return AsSpan().SequenceEqual(otherSpan);
        }

        public override int GetHashCode()
        {
            // same hash as the equivalent flat string instance
            return string.GetHashCode(AsSpan(), StringComparison.Ordinal);
        }
    }
}
