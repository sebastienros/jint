using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Jint.Diagnostics;

/// <summary>
/// What a described value is, using the vocabulary of the Chrome DevTools Protocol's
/// <c>Runtime.RemoteObject</c> <c>type</c> and <c>subtype</c> pair.
/// </summary>
/// <remarks>
/// The enum may gain members: treat one you do not recognize as <see cref="Object"/> rather than as an
/// error. Two members are Jint's rather than the protocol's — <see cref="HostObject"/> for a CLR value
/// projected into script, and <see cref="Accessor"/> for a property whose getter the inspector refuses to
/// call.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public enum ValueKind
{
    /// <summary><c>undefined</c>.</summary>
    Undefined,

    /// <summary><c>null</c>.</summary>
    Null,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A <c>BigInt</c>.</summary>
    BigInt,

    /// <summary>A string.</summary>
    String,

    /// <summary>A symbol.</summary>
    Symbol,

    /// <summary>Any callable, including a class constructor and a host function.</summary>
    Function,

    /// <summary>An <c>Array</c>.</summary>
    Array,

    /// <summary>A typed array — <c>Uint8Array</c> and its siblings.</summary>
    TypedArray,

    /// <summary>An <c>ArrayBuffer</c> or <c>SharedArrayBuffer</c>.</summary>
    ArrayBuffer,

    /// <summary>A <c>DataView</c>.</summary>
    DataView,

    /// <summary>A <c>Map</c>.</summary>
    Map,

    /// <summary>A <c>Set</c>.</summary>
    Set,

    /// <summary>A <c>WeakMap</c>, whose contents nothing can enumerate.</summary>
    WeakMap,

    /// <summary>A <c>WeakSet</c>, whose contents nothing can enumerate.</summary>
    WeakSet,

    /// <summary>A <c>WeakRef</c>, whose target is never dereferenced.</summary>
    WeakRef,

    /// <summary>A promise.</summary>
    Promise,

    /// <summary>An error object.</summary>
    Error,

    /// <summary>A <c>Date</c>.</summary>
    Date,

    /// <summary>A regular expression.</summary>
    RegExp,

    /// <summary>A proxy, described by its kind alone because every trap on it is script.</summary>
    Proxy,

    /// <summary>A generator or async generator object.</summary>
    Generator,

    /// <summary>An iterator object other than a generator.</summary>
    Iterator,

    /// <summary>An <c>arguments</c> object.</summary>
    Arguments,

    /// <summary>An ordinary object, including one boxing a primitive.</summary>
    Object,

    /// <summary>A CLR value projected into script, whose members are not read.</summary>
    HostObject,

    /// <summary>A property whose value comes from a function, reported instead of being called.</summary>
    Accessor,
}

/// <summary>
/// Which of the three states a promise is in, as reported by
/// <see cref="ValueDescription.PromiseState"/>.
/// </summary>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public enum ValuePromiseState
{
    /// <summary>Neither resolved nor rejected yet.</summary>
    Pending,

    /// <summary>Resolved.</summary>
    Fulfilled,

    /// <summary>Rejected.</summary>
    Rejected,
}

/// <summary>
/// A getter-free, trap-free, bounded description of one <see cref="Native.JsValue"/>, produced by
/// <see cref="ValueInspector.Describe"/>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here was produced by running script. An accessor property is reported rather than called, a
/// proxy is described by its kind alone, and a CLR value is named rather than read, so a description can
/// be taken of any value at any time without side effects.
/// </para>
/// <para>
/// The instance is a snapshot of strings and further descriptions: it holds no
/// <see cref="Native.JsValue"/>, so unlike the value it describes it may be kept, logged, serialized and
/// read from another thread.
/// </para>
/// <para>
/// <b>This is a preview surface and not part of Jint's compatibility contract</b>, declared to the compiler
/// as <c>JINT0002</c>; see <see cref="JintDiagnosticIds"/> for how a host acknowledges it. The type may
/// gain members and the exact text of <see cref="Description"/> may change in any release.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class ValueDescription
{
    internal ValueDescription(
        ValueKind kind,
        string description,
        string? className = null,
        IReadOnlyList<ValueEntry>? entries = null,
        bool overflow = false,
        ValuePromiseState? promiseState = null,
        ValueDescription? promiseResult = null)
    {
        Kind = kind;
        Description = description;
        ClassName = className;
        Entries = entries ?? [];
        Overflow = overflow;
        PromiseState = promiseState;
        PromiseResult = promiseResult;
    }

    /// <summary>Gets what the value is.</summary>
    public ValueKind Kind { get; }

    /// <summary>
    /// Gets the name of the constructor the value was built by, or <see langword="null"/> when no step of
    /// that walk could be taken without calling something.
    /// </summary>
    /// <remarks>
    /// It is read as a descriptor off the prototype chain, never through <c>Get</c>, so a script that made
    /// <c>constructor</c> an accessor produces <see langword="null"/> here instead of a call. For
    /// <see cref="ValueKind.HostObject"/> it is the CLR type's name.
    /// </remarks>
    public string? ClassName { get; }

    /// <summary>Gets the one-line header for the value, such as <c>Array(3)</c> or <c>Map(2)</c>.</summary>
    public string Description { get; }

    /// <summary>
    /// Gets the value's bounded entries: own enumerable properties, array elements, or collection members.
    /// </summary>
    /// <remarks>
    /// Empty for every value that has nothing to descend into, and for the kinds whose members cannot be
    /// read without running something — a proxy, a host object, and the weak collections.
    /// </remarks>
    public IReadOnlyList<ValueEntry> Entries { get; }

    /// <summary>Whether the value has more entries than <see cref="Entries"/> was allowed to carry.</summary>
    public bool Overflow { get; }

    /// <summary>
    /// Gets the state of a promise, or <see langword="null"/> when the value is not one.
    /// </summary>
    public ValuePromiseState? PromiseState { get; }

    /// <summary>
    /// Gets a settled promise's value or rejection reason, or <see langword="null"/> when the value is not
    /// a settled promise.
    /// </summary>
    public ValueDescription? PromiseResult { get; }

    /// <summary>Returns <see cref="Description"/>.</summary>
    public override string ToString() => Description;
}

/// <summary>
/// One entry of a <see cref="ValueDescription"/>: a property, an array element, or a collection member.
/// </summary>
/// <remarks>
/// An accessor property carries a <see cref="Value"/> of <see cref="ValueKind.Accessor"/> whose
/// <see cref="ValueDescription.Description"/> is <c>[Getter]</c>, <c>[Setter]</c> or
/// <c>[Getter/Setter]</c>; neither function is ever invoked.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
[StructLayout(LayoutKind.Auto)]
public readonly struct ValueEntry
{
    internal ValueEntry(string key, ValueDescription value, bool isAccessor = false, ValueDescription? entryKey = null)
    {
        Key = key;
        Value = value;
        IsAccessor = isAccessor;
        EntryKey = entryKey;
    }

    /// <summary>
    /// Gets the property name, the array index, or the position of a collection member, as text.
    /// </summary>
    /// <remarks>A symbol-keyed property is spelled the way a script writes the symbol, <c>Symbol(tag)</c>.</remarks>
    public string Key { get; }

    /// <summary>Gets the description of the entry's value.</summary>
    public ValueDescription Value { get; }

    /// <summary>Whether the entry is an accessor property, whose functions are never invoked.</summary>
    public bool IsAccessor { get; }

    /// <summary>
    /// Gets the description of a <c>Map</c> entry's key, or <see langword="null"/> for every other entry.
    /// </summary>
    public ValueDescription? EntryKey { get; }
}

/// <summary>
/// The bounds <see cref="ValueInspector.Describe"/> works within.
/// </summary>
/// <remarks>
/// Every bound is clamped rather than validated: a negative value is read as zero, and
/// <see cref="MaxDepth"/> is additionally capped at 32 so a cyclic graph cannot be walked into a stack
/// overflow by asking for it.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class ValueInspectorOptions
{
    /// <summary>Creates a set of bounds, each of which starts at its default.</summary>
    public ValueInspectorOptions()
    {
    }

    /// <summary>Gets how many entries one description may carry. Defaults to 100.</summary>
    public int MaxEntries { get; init; } = 100;

    /// <summary>Gets how many levels of entries are produced below the described value. Defaults to 2.</summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>Gets how long a string may be before it is truncated with an ellipsis. Defaults to 100.</summary>
    public int MaxStringLength { get; init; } = 100;

    /// <summary>
    /// Gets whether a function is described by its source text rather than by a short label. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The short label is <c>ƒ name()</c>, which is what a console prints. The source text is what
    /// <c>Function.prototype.toString</c> returns — the declaration itself when
    /// <see cref="Options.RetainFunctionSourceText"/> kept it, and
    /// <c>function name() { [native code] }</c> otherwise — which is what a debugger protocol's client
    /// parses a function out of.
    /// </para>
    /// <para>
    /// Either way the answer is produced without running script: a <c>name</c> a script replaced with an
    /// accessor is reported as absent rather than read, so a function described while the engine is paused
    /// stays exactly as it was. The host's <c>Options.Host.FunctionToStringHandler</c> is not consulted
    /// either, for the same reason.
    /// </para>
    /// </remarks>
    public bool FunctionSourceText { get; init; }
}
