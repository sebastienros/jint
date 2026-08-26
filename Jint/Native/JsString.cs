using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
/// Construct one with <see cref="Create(string)"/> — or with the <see cref="JsString(string)"/>
/// constructor — whenever the text already exists. Both are for a string a host <em>has</em>; the
/// value is flat from the start and none of the rest of this applies.
/// </para>
/// <para>
/// <b>When the text is expensive to produce, derive from <see cref="LazyJsString"/> instead of from
/// this class.</b> That is the supported way to expose a host string representation — a native handle,
/// an encoded buffer, a projected document field — without eagerly producing a .NET
/// <see cref="string"/>. A subclass supplies its length and one <c>Materialize()</c> method, and the
/// base class does the rest: it memoizes, it answers <see cref="Length"/> and truthiness without ever
/// asking for the text, and it refuses a <see langword="null"/> result with a message that names the
/// type.
/// </para>
/// <para>
/// <b>Subclassing this class directly.</b> Passing a <see langword="null"/> backing value to
/// <see cref="JsString(string)"/> still works and is how a lazy host string was written before
/// <see cref="LazyJsString"/> existed; Jint's own sliced, concatenated and deferred-concatenation
/// representations are built on the same mechanism. It is the harder path — the parameter is typed
/// <see cref="string"/>, so the <see langword="null"/> is a suppression against a contract that lives
/// only in prose, and the
/// subclass owns its own memoization. A subclass that does it <b>must</b> override every member that
/// would otherwise observe the backing value:
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
    /// <summary>
    /// The largest number of UTF-16 code units a JavaScript string may hold. The value is V8's
    /// <c>String::kMaxLength</c>, so a script that builds a longer string fails identically on both
    /// engines — with a <c>RangeError: Invalid string length</c> a <c>catch</c> block can handle,
    /// rather than with a CLR exception escaping the host's <c>Evaluate</c> call.
    /// </summary>
    /// <remarks>
    /// This is the <em>language</em> limit and is deliberately far below
    /// <see cref="ClrLimits.MaxArrayLength"/>, which remains the CLR allocation ceiling for
    /// everything that is not a JavaScript string.
    /// </remarks>
    internal const int MaxLength = (1 << 29) - 24; // 536_870_888

    /// <summary>
    /// Throws <c>RangeError: Invalid string length</c> when a string of <paramref name="length"/>
    /// code units would exceed <see cref="MaxLength"/>. The length is a <see cref="long"/> so a
    /// caller can add the lengths of the pieces it is about to concatenate without the sum wrapping,
    /// and so the check happens <em>before</em> anything of that size is allocated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfLengthExceeded(Realm realm, long length)
    {
        if (length > MaxLength)
        {
            Throw.RangeError(realm, "Invalid string length");
        }
    }

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

    /// <summary>
    /// Creates a string for text the host already has. <see cref="Create(string)"/> is the preferred
    /// spelling — it hands back a shared instance for the empty string and for single ASCII characters.
    /// </summary>
    /// <param name="value">
    /// The text. A subclass may pass <see langword="null"/> to mean "not materialized yet" and take on
    /// the overriding obligations listed on this class, but <see cref="LazyJsString"/> is the supported
    /// way to write such a string and requires none of them.
    /// </param>
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

    /// <summary>
    /// The shortest concatenation result that is worth deferring instead of copying. Below it a
    /// <see cref="RopeString"/> would cost a second object for a copy that is already cheaper than the
    /// allocation, and every consumer of the result would pay the flattening indirection for nothing.
    /// </summary>
    /// <remarks>
    /// Only the asymptotic behaviour above this line matters, and the exact value does not change it: a
    /// loop that accumulates in <c>n</c>-character pieces copies for the first
    /// <c>MinDeferredConcatenationLength / n</c> iterations and defers from then on, so the whole
    /// quadratic term is bounded by <c>MinDeferredConcatenationLength²</c> characters however long the
    /// loop runs. It is a knob for the small-string case, not for the fix.
    /// </remarks>
    internal const int MinDeferredConcatenationLength = 512;

    /// <summary>
    /// Concatenates two strings, deferring the copy into a <see cref="RopeString"/> once the result is
    /// long enough to be worth a node. This is what <c>a + b</c> produces; it is deliberately not what
    /// <c>a += b</c> produces, which stays on <see cref="ConcatenatedString"/>'s builder.
    /// </summary>
    /// <remarks>
    /// The caller has already refused a result longer than <see cref="MaxLength"/> — it holds both
    /// lengths for the sum it just checked, so re-reading them here would be two virtual dispatches for
    /// a number it already has. That check is what lets the addition below be a plain <see cref="int"/>.
    /// </remarks>
    internal static JsString Concat(JsString left, JsString right)
    {
        var leftLength = left.Length;
        var rightLength = right.Length;
        Debug.Assert((long) leftLength + rightLength <= MaxLength, "the caller must refuse an over-long result before building it");

        var length = leftLength + rightLength;
        if (length < MinDeferredConcatenationLength)
        {
            return Create(string.Concat(left.ToString(), right.ToString()));
        }

        if (leftLength == 0)
        {
            return Immutable(right);
        }

        if (rightLength == 0)
        {
            return Immutable(left);
        }

        return new RopeString(Immutable(left), Immutable(right), length);
    }

    /// <summary>
    /// A value that is safe to hold on to. <see cref="ConcatenatedString"/> is mutated in place by
    /// <c>+=</c>, so a node that kept one would change content behind whoever else is still reading it;
    /// everything else <see cref="JsString"/> produces is immutable and is returned as it is. The
    /// snapshot is a wrapper around the string the builder has already flattened, not a character copy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsString Immutable(JsString value)
    {
        return (value._type & InternalTypes.RequiresCloning) == InternalTypes.Empty ? value : new JsString(value.ToString());
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

    /// <summary>
    /// Concatenates <paramref name="value"/> onto this string, refusing a result longer than
    /// <see cref="MaxLength"/> before allocating it. The caller performs the coercion and passes the
    /// <see cref="Realm"/> the error is raised from.
    /// </summary>
    /// <remarks>
    /// The check lives here rather than at the call site because the receiver's length is only cheap
    /// from inside: <see cref="Length"/> is virtual and <c>ConcatenatedString</c> overrides it with a
    /// two-branch null coalesce, so reading it before the call added a dispatch to every <c>s += t</c>
    /// — measurable on the SunSpider <c>string-base64</c> and <c>string-fasta</c> rows, which are that
    /// loop and nothing else. Each override instead reads a field it was already loading, so the guard
    /// costs an add and a compare and the realm is touched only on the throw.
    /// </remarks>
    internal virtual JsString Append(Realm realm, string value)
    {
        // ToString() rather than _value: a subclass may carry a null backing value until it
        // materializes, and it is called here anyway, so its length is free and non-virtual.
        var self = ToString();
        ThrowIfLengthExceeded(realm, (long) self.Length + value.Length);
        return new ConcatenatedString(string.Concat(self, value));
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

        internal override JsString Append(Realm realm, string value)
        {
            if (_stringBuilder == null)
            {
                ThrowIfLengthExceeded(realm, (long) _value.Length + value.Length);

                // The line above has established that the combined length fits in MaxLength, so this
                // int sum cannot overflow.
                _stringBuilder = new StringBuilder(_value, _value.Length + value.Length);
            }
            else
            {
                ThrowIfLengthExceeded(realm, (long) _stringBuilder.Length + value.Length);
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

    /// <summary>
    /// An immutable binary concatenation node: the result of an <c>a + b</c> whose operands are long
    /// enough that copying them is worth deferring. It holds the two operands and the total length; the
    /// flat text is produced once, on the first read that actually needs characters, and memoized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why not <see cref="ConcatenatedString"/>.</b> That one is a mutable builder, and <c>s += t</c>
    /// may append into it only because the assignment replaces the receiver. A non-assignment <c>+</c>
    /// has no such guarantee — both operands stay reachable from wherever they were read — so the
    /// deferred form it can use has to be immutable. Same reason an operand that <em>is</em> a
    /// <see cref="ConcatenatedString"/> is snapshotted on the way in (<see cref="Immutable"/>): a later
    /// <c>+=</c> on it appends in place, and a node holding it would change content behind its reader.
    /// </para>
    /// <para>
    /// <b>Only the length is answered from the node.</b> <see cref="Length"/> — and therefore
    /// truthiness, and the length comparison <see cref="JsString.Equals(JsString)"/> performs first — is
    /// free. Everything else flattens, including <see cref="this[int]"/>, which does so deliberately
    /// rather than descending the tree: descending is O(depth), so a <c>charCodeAt</c> scan over a
    /// freshly accumulated value would become a new quadratic — the very shape this class exists to
    /// remove. Flattening costs one copy, which is what the old code performed on every concatenation
    /// anyway, and the memo makes every later read O(1). The base <see cref="JsString"/> bodies for
    /// equality and hashing are correct as they stand: they read <c>_value</c> only when it is either
    /// <see langword="null"/> or the exact flat text, which is the invariant this class keeps.
    /// </para>
    /// <para>
    /// <b>Depth is not capped, and that is the design.</b> Flushing the tree at a depth bound would
    /// re-copy the whole accumulated value every N concatenations — the quadratic behaviour again, with
    /// a smaller constant, which is the thing being fixed. The hazard a cap would have addressed, a
    /// recursive flatten overflowing the CLR stack on the unbalanced tree a long loop produces, is
    /// removed at its source instead: <see cref="CopyInto"/> walks iteratively with an explicit,
    /// heap-allocated stack, so depth costs 8 bytes per pending node — against the ~40 the node itself
    /// already costs — and no stack frames at all. The walk descends right-first, so the append shape
    /// (<c>s = s + x</c>, a left-leaning spine) never has more than one node pending; the prepend shape
    /// (<c>s = x + s</c>) is the one that pays for the array.
    /// </para>
    /// </remarks>
    internal sealed class RopeString : JsString
    {
        /// <summary>
        /// Where the pending-node stack starts. A tree that never leans right stays inside one entry, so
        /// this is only ever reached by prepend-shaped or genuinely bushy trees, which then double.
        /// </summary>
        private const int InitialPendingCapacity = 16;

        // Not readonly, and released once the flat value is memoized: a flattened rope must stop
        // retaining the tree it was built from, which for an accumulator loop is one node per iteration.
        private JsString? _left;
        private JsString? _right;

        private readonly int _length;

        internal RopeString(JsString left, JsString right, int length) : base(null!, InternalTypes.String)
        {
            _left = left;
            _right = right;
            _length = length;
        }

        public override int Length => _length;

        public override string ToString() => _value ?? Flatten();

        // Flattens rather than descending; see the class remarks for why.
        public override char this[int index] => ToString()[index];

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string Flatten()
        {
            // Not routed through a polyfill, unlike the rest of the assembly's downlevel gaps: what
            // net472 and netstandard2.0 lack is not string.Create but System.Buffers.SpanAction<T, TArg>,
            // its callback type, and a "polyfill" that declared a delegate of its own would be inventing
            // API rather than backfilling it. The downlevel form fills an array and copies it into the
            // string, which is the extra copy those targets already pay everywhere a string is built
            // from parts; the modern form writes the characters into the string as it is allocated, and
            // that single copy is what keeps a flattened concatenation no more expensive than the
            // string.Concat it replaced.
#if NETFRAMEWORK || NETSTANDARD2_0
            var buffer = new char[_length];
            CopyInto(buffer);
            var value = new string(buffer);
#else
            var value = string.Create(_length, this, static (span, rope) => rope.CopyInto(span));
#endif

            _value = value;
            _left = null;
            _right = null;

            return value;
        }

        /// <summary>
        /// Writes the whole tree into <paramref name="destination"/>, filling it from the back so that a
        /// left-leaning spine — the <c>s = s + x</c> accumulator — keeps at most one node pending.
        /// </summary>
        private void CopyInto(Span<char> destination)
        {
            var pending = System.Array.Empty<JsString>();
            var pendingCount = 0;
            var node = (JsString) this;
            var position = _length;

            while (true)
            {
                // A node that has already memoized its own flat value is a leaf as far as this walk is
                // concerned: ToString() below hands back the memo without touching the (released) children.
                if (node is RopeString { _value: null } rope)
                {
                    if (pendingCount == pending.Length)
                    {
                        System.Array.Resize(ref pending, pendingCount == 0 ? InitialPendingCapacity : pendingCount * 2);
                    }

                    pending[pendingCount++] = rope._left!;
                    node = rope._right!;
                    continue;
                }

                var text = node.ToString();
                position -= text.Length;
                text.AsSpan().CopyTo(destination.Slice(position));

                if (pendingCount == 0)
                {
                    break;
                }

                node = pending[--pendingCount];
            }

            Debug.Assert(position == 0, "the node's length must be the sum of its operands' lengths");
        }
    }
}
