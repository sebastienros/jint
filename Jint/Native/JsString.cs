using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Interpreter;

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
    /// <para>
    /// Only the asymptotic behaviour above this line matters, and the exact value does not change it: a
    /// loop that accumulates in <c>n</c>-character pieces copies for the first
    /// <c>MinDeferredConcatenationLength / n</c> iterations and defers from then on, so the whole
    /// quadratic term is bounded by <c>MinDeferredConcatenationLength²</c> characters however long the
    /// loop runs. It is a knob for the small-string case, not for the fix.
    /// </para>
    /// <para>
    /// The arithmetic under the value: a <see cref="RopeString"/> is 56 bytes on 64-bit — header, the
    /// inherited type and memo fields, two operand references and the length — and it does not remove
    /// the flat allocation, it postpones it. A result that is read once therefore costs those 56 bytes
    /// and one indirection <em>more</em> than copying it would have; the node earns them back only
    /// across iterations, where the operand it holds would otherwise be copied again. So the line sits
    /// far above the point where a node is merely affordable — and what the path <em>below</em> the line
    /// allocates matters more than where the line is. A chain that stays below it has to cost what it
    /// cost before this representation existed; when it did not, SunSpider's date-format rows paid for a
    /// deferral they never took (#3527).
    /// </para>
    /// </remarks>
    internal const int MinDeferredConcatenationLength = 512;

    /// <summary>
    /// Concatenates two strings, deferring the copy into a <see cref="RopeString"/> once the result is
    /// long enough to be worth a node. This is what <c>a + b</c> produces; it is deliberately not what
    /// <c>a += b</c> produces, which shares <see cref="ConcatenatedString"/>'s chunks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller has already refused a result longer than <see cref="MaxLength"/> — it holds both
    /// lengths for the sum it just checked, so re-reading them here would be two virtual dispatches for
    /// a number it already has. That check is what lets the addition below be a plain <see cref="int"/>.
    /// </para>
    /// <para>
    /// <paramref name="context"/> is read on the deferring branch alone, and only for the engine's memory
    /// limit: a node is the same 56 bytes whatever length it stands for, and its characters are allocated
    /// by whoever flattens it — possibly a host, after every constraint has been disarmed. So under
    /// <c>LimitMemory</c> the node is charged when it is built; see
    /// <see cref="Constraints.MemoryLimitConstraint.ChargeDeferredConcatenation"/> for how much. Taking the
    /// context rather than its engine keeps that load off the short branch, which every short <c>+</c> takes.
    /// </para>
    /// </remarks>
    internal static JsString Concat(JsString left, JsString right, EvaluationContext context)
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
            return right;
        }

        if (rightLength == 0)
        {
            return left;
        }

        // A constant fold evaluates with no engine: the node is then made from literals in the source text,
        // which no budget pays for either.
        if (context.Engine?._memoryLimitConstraint is { } memoryLimit)
        {
            memoryLimit.ChargeDeferredConcatenation(leftLength, rightLength);
        }

        return new RopeString(left, right, length);
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
    /// Returns a new string, this one followed by <paramref name="value"/>, refusing a result longer than
    /// <see cref="MaxLength"/> before allocating it. The caller performs the coercion and passes the
    /// <see cref="Realm"/> the error is raised from. This string is left as it was.
    /// </summary>
    /// <remarks>
    /// The check lives here rather than at the call site because the receiver's length is only cheap
    /// from inside: <see cref="Length"/> is virtual, so reading it before the call added a dispatch to
    /// every <c>s += t</c> — measurable on the SunSpider <c>string-base64</c> and <c>string-fasta</c>
    /// rows, which are that loop and nothing else. Each override instead reads a field it was already
    /// loading, so the guard costs an add and a compare.
    /// </remarks>
    internal virtual JsString Append(Realm realm, string value)
    {
        // ToString() rather than _value: a subclass may carry a null backing value until it
        // materializes, and it is called here anyway, so its length is free and non-virtual.
        var self = ToString();
        ThrowIfLengthExceeded(realm, (long) self.Length + value.Length);
        return new ConcatenatedString(string.Concat(self, value));
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
        // has not materialized yet pays the virtual ToString(). See the note on GetHashCode for the
        // invariant that makes reading the field here safe.
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
    // INVARIANT: every representation Jint builds keeps _value either null or the exact flat text —
    // including ConcatenatedString, whose text never changes once the instance exists, so its memo cannot
    // go stale. That is what makes this null-test (and the one in Equals(JsString)) correct for all of them, and a
    // new representation must keep it: a non-null value that is not the text would be returned as the
    // text here, and by the plain loads its own ToString() relies on for thread safety.
    public override int GetHashCode()
    {
        var value = _value;
        return StringComparer.Ordinal.GetHashCode(value ?? ToString());
    }

    /// <summary>
    /// What <c>s += t</c> produces: a string whose characters live in chunks it shares with the value it was
    /// appended to, so a loop of appends is amortised linear, and which never changes once anything but the
    /// binding that produced it can hold it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why immutable.</b> This used to be a mutable builder that <c>+=</c> appended into in place, which was
    /// correct only while the binding being assigned was the instance's sole holder. Copying it at every place
    /// a value is duplicated kept that true for declarations, identifier assignments, arguments and returns,
    /// but not for a member assignment, any built-in that copies values (<c>slice</c>, spread,
    /// <c>Object.assign</c>, …) or any exit to the host, and each of those then saw its string change when
    /// script appended to the original (sebastienros/jint#4259). A value that never changes needs no copy
    /// anywhere, so there are no exits left to find.
    /// </para>
    /// <para>
    /// <b>How the chunks are shared.</b> An instance is a chunk and a length: its text is the first
    /// <c>length - chunk.Offset</c> characters of its chunk, preceded by the chunks before it. Characters are
    /// only ever written past the end of every instance a chunk already holds, so the text below any instance's
    /// length is never written again once that instance exists. The append that may write is the one whose
    /// receiver ends exactly where its chunk's written characters end (<see cref="Chunk.Used"/>): it writes
    /// in place and hands back a new instance, and the receiver keeps its own length. Any other receiver —
    /// one somebody has already appended past, or one whose chunk another engine writes — starts a chunk of
    /// its own after its end and shares everything before it, which is why a chunk may have more than one
    /// successor and a reader never reads a chunk past where the next one starts.
    /// </para>
    /// <para>
    /// <b>Who may write.</b> Only an append made under the realm that owns a chunk writes into it, and one
    /// engine never runs two appends at once, so claiming the end of a chunk needs no interlocked operation. A
    /// string handed from one engine to another — which a primitive may be — is extended by the second one in a
    /// chunk of its own, rather than both racing to write the same characters.
    /// </para>
    /// <para>
    /// <b>The one value that is extended in place.</b> A new value per append costs an object per append, so a
    /// <c>+=</c> whose result went into a declarative binding (a function or block variable, a catch or eval
    /// variable) stores it <em>owned</em> (<see cref="Runtime.Environments.BindingFlags.OwnsString"/>) and marks it
    /// <see cref="IsExtensibleInPlace"/>; the binding's next <c>+=</c> then extends that instance itself
    /// (<see cref="AppendInPlace"/>) instead of allocating, and stores nothing. That is sound because nothing but
    /// the binding can hold it: the value of the <c>+=</c> expression is marked shared when it is used, and every
    /// read of a binding's value — <see cref="Runtime.Environments.Binding.Value"/>, an identifier read (through the
    /// <see cref="Runtime.InternalTypes.ReadHook"/> bit an owned instance carries), or a slot reader's out-of-line
    /// path — marks an owned string shared before handing it on, after which it is never written again. So no
    /// variable, property, collection, closure, debugger or host ever holds a value that changes, and a host may
    /// still read any string it holds on any thread.
    /// </para>
    /// <para>
    /// <b>Reading.</b> A reader touches only characters below its instance's length, which nothing writes any
    /// more, so a host may read a string on any thread while its engine keeps appending to the binding it came
    /// from. The flat text is memoized in <c>_value</c> by the first read that needs it, with a plain reference
    /// store: storing a reference publishes the string's contents to whoever loads it, so a reader that finds
    /// the memo has the whole text, and two first reads racing each build and store the same text
    /// (sebastienros/jint#4171).
    /// </para>
    /// </remarks>
    internal sealed class ConcatenatedString : JsString
    {
        /// <summary>The smallest chunk worth its object header.</summary>
        private const int MinChunkLength = 16;

        /// <summary>
        /// Where chunk growth stops doubling, as <see cref="StringBuilder"/>'s does: 8000 characters keep a chunk
        /// off the large object heap, and bound what a value that stopped growing leaves unused.
        /// </summary>
        private const int MaxChunkLength = 8000;

        // Null for a value produced by one concatenation, whose text is _value from the start; the first
        // append to it copies that text into a chunk. Both fields change only in AppendInPlace, while the
        // binding that owns this instance is the only place that holds it.
        private Chunk? _chunk;
        private int _length;

        // Unclaimed until a binding takes ownership of this instance (Binding.ChangeValueOwned); Shared for good from
        // the first read that hands it anywhere (MarkShared), after which no binding can claim it again. A byte in the
        // padding after _length, so the object stays the size it was.
        private Ownership _ownership;

        private enum Ownership : byte
        {
            Unclaimed,
            Owned,
            Shared,
        }

        internal ConcatenatedString(string value) : base(value)
        {
            _length = value.Length;
        }

        private ConcatenatedString(Chunk chunk, int length) : base(null!)
        {
            _chunk = chunk;
            _length = length;
        }

        public override int Length => _length;

        /// <summary>
        /// Whether the binding that owns this instance may still extend it in place: nothing has read it out of that
        /// binding since the <c>+=</c> that produced it.
        /// </summary>
        internal bool IsExtensibleInPlace => _ownership == Ownership.Owned;

        /// <summary>
        /// Taken by a binding storing a value its <c>+=</c> has just created, which nothing else holds. Refused for an
        /// instance that has ever been handed on: shared is final, so even a caller that wrongly took an existing
        /// value for a new one cannot make it extensible again.
        /// </summary>
        internal bool TryClaimForBinding()
        {
            if (_ownership == Ownership.Unclaimed)
            {
                // ReadHook makes an identifier read of the owning binding mark this instance shared before handing
                // it on (JintIdentifierExpression.HandOn), with the one _type test that lane already makes.
                _ownership = Ownership.Owned;
                _type |= InternalTypes.ReadHook;
            }

            return _ownership == Ownership.Owned;
        }

        /// <summary>
        /// Called by every read that hands this instance on: from here on it is never written again.
        /// </summary>
        internal void MarkShared()
        {
            _ownership = Ownership.Shared;
            _type &= ~InternalTypes.ReadHook;
        }

        public override string ToString() => _value ?? Materialize();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string Materialize()
        {
            var chunk = _chunk!;
            string value;
            if (chunk.Previous is null)
            {
                value = new string(chunk.Chars, 0, _length);
            }
            else
            {
                // Same split as RopeString.Flatten: downlevel lacks string.Create's callback type, so it fills
                // an array and copies it into the string.
#if NETFRAMEWORK || NETSTANDARD2_0
                var buffer = new char[_length];
                CopyTo(buffer);
                value = new string(buffer);
#else
                value = string.Create(_length, this, static (span, self) => self.CopyTo(span));
#endif
            }

            _value = value;
            return value;
        }

        /// <summary>Writes this value's text into <paramref name="destination"/>, walking its chunks back to front.</summary>
        private void CopyTo(Span<char> destination)
        {
            var end = _length;
            for (var chunk = _chunk; chunk is not null; chunk = chunk.Previous)
            {
                var start = chunk.Offset;
                chunk.Chars.AsSpan(0, end - start).CopyTo(destination.Slice(start));
                end = start;
            }
        }

        /// <remarks>
        /// The memo test and a string index, with the chunk walk out of line: a method with a loop is compiled with
        /// patchpoints and its own instrumentation, and inlined where a caller guards on this type it left that
        /// caller (charAt, charCodeAt, a computed member read) on synthesized rather than measured profile data.
        /// </remarks>
        public override char this[int index]
        {
            get
            {
                var value = _value;
                return value is not null ? value[index] : CharInChunks(index);
            }
        }

        /// <summary>The character at <paramref name="index"/> of a value with no memo, read from its chunks.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private char CharInChunks(int index)
        {
            // The chunk holds characters past this value's end that belong to other values, so the bound is this
            // value's own length rather than the array's.
            if ((uint) index >= (uint) _length)
            {
                Throw.ArgumentOutOfRangeException(nameof(index), "Index was outside the bounds of the string.");
            }

            // Walks back from the end, as StringBuilder's indexer does, so a read near the end of a value being built
            // stays cheap.
            var chunk = _chunk!;
            while (index < chunk.Offset)
            {
                chunk = chunk.Previous!;
            }

            return chunk.Chars[index - chunk.Offset];
        }

        internal override JsString Append(Realm realm, string value)
        {
            var length = _length;
            ThrowIfLengthExceeded(realm, (long) length + value.Length);
            if (value.Length == 0)
            {
                return this;
            }

            return new ConcatenatedString(Extend(realm, value), length + value.Length);
        }

        /// <summary>
        /// Appends <paramref name="value"/> to this instance itself rather than returning a new one. Only for the
        /// <c>+=</c> lane, and only while <see cref="IsExtensibleInPlace"/> and the binding that owns this instance
        /// still holds it: nothing else can then see it change.
        /// </summary>
        internal void AppendInPlace(Realm realm, string value)
        {
            Debug.Assert(_ownership == Ownership.Owned, "only an instance a binding owns, and nothing has read, is extended in place");

            var length = _length;
            ThrowIfLengthExceeded(realm, (long) length + value.Length);
            if (value.Length == 0)
            {
                return;
            }

            _chunk = Extend(realm, value);
            _length = length + value.Length;
            _value = null!;
        }

        /// <summary>
        /// Writes <paramref name="value"/> after this value's end and returns the chunk the result ends in: into this
        /// value's own chunk when it ends that chunk's written characters and the realm owns it, otherwise into a new
        /// chunk. Changes no instance; the caller decides whether the result is a new value or this one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Chunk Extend(Realm realm, string value)
        {
            var chunk = _chunk;
            if (chunk is not null)
            {
                var used = _length - chunk.Offset;
                if (chunk.Used == used && ReferenceEquals(chunk.Owner, realm._concatenationOwner))
                {
                    var chars = chunk.Chars;
                    if (value.Length <= chars.Length - used)
                    {
                        // The characters first, then the claim: a result is handed out only after both, and no
                        // reader looks at Used. One or two characters are stored directly, as StringBuilder.Append
                        // does, rather than through a copy call.
                        if (value.Length <= 2)
                        {
                            chars[used] = value[0];
                            if (value.Length > 1)
                            {
                                chars[used + 1] = value[1];
                            }
                        }
                        else
                        {
                            value.AsSpan().CopyTo(chars.AsSpan(used));
                        }

                        chunk.Used = used + value.Length;
                        return chunk;
                    }

                    return FillAndStartChunk(realm, chunk, used, value);
                }

                return StartBranchChunk(realm, value);
            }

            return StartFirstChunk(realm, value);
        }

        /// <summary>
        /// This value ends its chunk and <paramref name="value"/> does not fit in what is left: fills the chunk, as
        /// <see cref="StringBuilder"/> does, so that no chunk but the last is left part-empty, and starts the next
        /// one sized with the text, doubling up to <see cref="MaxChunkLength"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Chunk FillAndStartChunk(Realm realm, Chunk chunk, int used, string value)
        {
            var head = chunk.Chars.Length - used;
            value.AsSpan(0, head).CopyTo(chunk.Chars.AsSpan(used));
            chunk.Used = chunk.Chars.Length;

            var offset = _length + head;
            var rest = value.Length - head;
            var chars = GC.AllocateUninitializedArray<char>(System.Math.Max(rest, System.Math.Max(MinChunkLength, System.Math.Min(offset, MaxChunkLength))));
            value.AsSpan(head).CopyTo(chars);
            return new Chunk(chars, chunk, offset, realm._concatenationOwner, rest);
        }

        /// <summary>
        /// Somebody already appended past this value, or another engine writes its chunk: starts a chunk of this
        /// append's own after this value's end, sharing everything before it. A branch is usually a copy that grows
        /// by a little or not at all, so its chunk is small; the doubling takes over if it does keep growing.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Chunk StartBranchChunk(Realm realm, string value)
        {
            var chars = GC.AllocateUninitializedArray<char>(System.Math.Max(value.Length, MinChunkLength));
            value.AsSpan().CopyTo(chars);
            return new Chunk(chars, _chunk, _length, realm._concatenationOwner, value.Length);
        }

        /// <summary>
        /// The first append to a value one concatenation produced: copies its text into a chunk with room to grow.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Chunk StartFirstChunk(Realm realm, string value)
        {
            var text = _value;
            var length = text.Length + value.Length;
            var chars = GC.AllocateUninitializedArray<char>(length + System.Math.Max(MinChunkLength, System.Math.Min(length, MaxChunkLength)));
            text.AsSpan().CopyTo(chars);
            value.AsSpan().CopyTo(chars.AsSpan(text.Length));
            return new Chunk(chars, previous: null, offset: 0, realm._concatenationOwner, length);
        }

        public override bool Equals(string? other)
        {
            if (other is null || other.Length != _length)
            {
                return false;
            }

            var value = _value;
            return value is not null
                ? string.Equals(value, other, StringComparison.Ordinal)
                : ContentEquals(other.AsSpan());
        }

        public override bool Equals(JsString? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is null || other.Length != _length)
            {
                return false;
            }

            // The other side's text — for a flat string, the field it already holds — against this one's chunks,
            // so a comparison does not materialize this value.
            var value = _value;
            return value is not null ? other.Equals(value) : ContentEquals(other.ToString().AsSpan());
        }

        /// <summary>Compares this value's chunks with <paramref name="other"/>, which is as long as this value.</summary>
        private bool ContentEquals(ReadOnlySpan<char> other)
        {
            var end = _length;
            for (var chunk = _chunk; chunk is not null; chunk = chunk.Previous)
            {
                var start = chunk.Offset;
                if (!chunk.Chars.AsSpan(0, end - start).SequenceEqual(other.Slice(start, end - start)))
                {
                    return false;
                }

                end = start;
            }

            return true;
        }

        // GetHashCode is the base one: it hashes _value, or ToString() while that is null, which is the content.

        /// <summary>
        /// A run of characters shared by every value appended from the one that started it.
        /// </summary>
        private sealed class Chunk
        {
            internal readonly char[] Chars;

            /// <summary>The text before <see cref="Offset"/>, read up to <see cref="Offset"/> and no further.</summary>
            internal readonly Chunk? Previous;

            /// <summary>Where <c>Chars[0]</c> sits in the whole text.</summary>
            internal readonly int Offset;

            /// <summary>The realm whose appends may write into <see cref="Chars"/>.</summary>
            internal readonly object Owner;

            /// <summary>
            /// How many of <see cref="Chars"/> are written. Written and read by the owner's appends only; a reader
            /// goes by its own instance's length.
            /// </summary>
            internal int Used;

            internal Chunk(char[] chars, Chunk? previous, int offset, object owner, int used)
            {
                Chars = chars;
                Previous = previous;
                Offset = offset;
                Owner = owner;
                Used = used;
            }
        }
    }

    /// <summary>
    /// Zero-copy view over a section of a backing string, produced by slice/substring/substr
    /// for large results. Immutable, like every representation here; the flat value is materialized
    /// lazily on first <see cref="ToString"/>.
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
    /// <b>Why not <see cref="ConcatenatedString"/>.</b> Both are immutable, but they defer different
    /// things. <c>s += t</c> copies the appended text into chunks it shares with the value it extended, so
    /// its result reads back without a walk — which works only at the end of a value, and copies the right
    /// operand. A node copies neither operand, so it also serves <c>s = x + s</c>, the shape <c>+=</c> cannot
    /// express. An operand that is itself a <see cref="ConcatenatedString"/> is held as it is: its text never
    /// changes once it exists.
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
    /// <para>
    /// <b>The memory budget is charged when a node is built, never when it is flattened.</b> The flatten can
    /// run anywhere — a host's <c>ToString()</c> after the run, another thread — and none of those places has
    /// an operation to charge, so <see cref="Concat"/> charges the characters a node appends before it exists
    /// (sebastienros/jint#4162). A new deferred representation owes the same, at its own construction.
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
        // Released only after the memo is published, and read in CopyInto as "the memo is there" — see
        // Flatten for why both halves of that handshake are volatile.
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

            // The first read of a node can happen on two threads at once, and without any host sharing a
            // result: a literal-plus-literal folded at preparation is one node on a Prepared<Script>, which
            // every engine running that preparation reads (#4161). Two flattens racing is harmless — each
            // builds the same text — but a walk that passed the memo test in CopyInto just before this one
            // published must not then find the operands gone. So the memo is stored first, and the operands
            // are released with volatile writes: an ordinary store may become visible ahead of an earlier
            // one, and a reader that saw an operand released before the memo would have neither. The memo
            // itself needs no barrier — storing a reference publishes the string's contents to whoever
            // loads it — so ToString() stays a plain load of _value.
            _value = value;
            Volatile.Write(ref _left, null);
            Volatile.Write(ref _right, null);

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
                string text;
                if (node is RopeString { _value: null } rope)
                {
                    // Another thread may flatten this node between the memo test above and these reads.
                    // They acquire what Flatten's releases publish, so an operand seen released means the
                    // memo is visible — never null here — and the node is copied from it instead of
                    // descended.
                    var left = Volatile.Read(ref rope._left);
                    var right = Volatile.Read(ref rope._right);
                    if (left is not null && right is not null)
                    {
                        if (pendingCount == pending.Length)
                        {
                            System.Array.Resize(ref pending, pendingCount == 0 ? InitialPendingCapacity : pendingCount * 2);
                        }

                        pending[pendingCount++] = left;
                        node = right;
                        continue;
                    }

                    text = Volatile.Read(ref rope._value);
                }
                else
                {
                    text = node.ToString();
                }

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
