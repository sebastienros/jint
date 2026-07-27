using System.Collections;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using Jint.Runtime;

namespace Jint;

/// <summary>
/// The identifier names a prepared program references without declaring them: for each listed name, at least one
/// reference site in the prepared source is not bound by any enclosing declaration, so at run time that reference
/// resolves against the global environment (or an enclosing <c>with</c> object), or throws a <c>ReferenceError</c>.
/// <para>
/// The intended use is letting a host decide <em>what to build</em> before it builds it: prepare the script once,
/// intersect this set with the ambient API the host could expose, and construct and register only the intersection.
/// That is a decision the engine cannot defer on the host's behalf, because the expensive part is usually the
/// host-side construction of the value rather than its registration.
/// </para>
/// <para>
/// Read-only, immutable and safe to share across engines and threads, exactly like the <see cref="Prepared{TProgram}"/>
/// it came from. Obtained from <see cref="Prepared{TProgram}.ReferencedGlobals"/> after preparing with
/// <see cref="ScriptPreparationOptions.CollectReferencedGlobals"/> (or
/// <see cref="ModulePreparationOptions.CollectReferencedGlobals"/>) set.
/// </para>
///
/// <para>
/// <b>Exactly what is reported.</b> A name is reported if and only if the program contains at least one
/// <c>Identifier</c> in a <em>reference position</em> whose name is not bound by any lexically enclosing declaration
/// within the prepared source. Resolution is performed per reference site against the real scope structure, not by
/// subtracting "every name declared anywhere in the file": in <c>function f(x) { return x; } x;</c> the parameter
/// <c>x</c> does not hide the free trailing <c>x</c>, which is reported.
/// </para>
/// <para>
/// <b>Reference positions (reported).</b> Reads; writes, including compound assignment and the targets of
/// destructuring <em>assignment</em>; <c>typeof x</c> and <c>delete x</c>; update operands; callees; arguments and
/// spread operands; computed member keys (<c>a[b]</c> reports both <c>a</c> and <c>b</c>); computed object and class
/// keys; shorthand property values (<c>{ a }</c> reports <c>a</c>); default-value expressions; template-literal
/// interpolations and tags; class <c>extends</c> operands; decorators.
/// </para>
/// <para>
/// <b>Not reference positions (never reported for that occurrence).</b> Declaration identifiers themselves —
/// <c>var</c>/<c>let</c>/<c>const</c> binding patterns including nested, rest and default targets, function and class
/// declaration names, function and class <em>expression</em> self-names, parameters, catch parameters and import
/// bindings; non-computed member property names (<c>a.b</c> reports only <c>a</c>); non-computed object and class
/// literal keys; labels, both the declaration and the <c>break</c>/<c>continue</c> reference; the <c>a</c> of
/// <c>import { a as b }</c> and the <c>b</c> of <c>export { a as b }</c>, plus every name in a re-export
/// (<c>export { a } from "m"</c>); and the parts of a meta-property (<c>new.target</c>, <c>import.meta</c>).
/// <c>this</c>, <c>super</c> and <c>#private</c> names are not identifiers and can never appear.
/// </para>
/// <para>
/// <b>Writes count.</b> In sloppy mode <c>undeclared = 1</c> creates a global at run time, so <c>undeclared</c> is
/// reported. The set does not distinguish a read from a write; a host that only wants to install read-only context
/// may therefore over-install.
/// </para>
/// <para>
/// <b><c>arguments</c>.</b> Excluded when the reference sits inside any non-arrow function, where it is implicitly
/// bound. Reported at the top level and inside a top-level arrow function, where it is not.
/// </para>
/// <para>
/// <b><c>with</c> does not close the set.</b> Inside <c>with (o) { x }</c> the name <c>x</c> is lexically free and is
/// reported even though it may resolve on <c>o</c> at run time. This over-approximates, which is the harmless
/// direction for the install-only-what-is-referenced use.
/// </para>
/// <para>
/// <b>Annex B block functions.</b> A sloppy-mode <c>{ function f() {} }</c> declares <c>f</c> in the block, and a
/// later top-level <c>f()</c> is reported even though Annex B B.3.3 also creates a global <c>var f</c> at run time.
/// This too over-approximates.
/// </para>
/// <para>
/// <b>Determinism.</b> The same source prepared with the same options always yields the same set, ordinal-sorted and
/// deduplicated.
/// </para>
/// </summary>
/// <seealso cref="Prepared{TProgram}.ReferencedGlobals"/>
/// <seealso cref="ScriptPreparationOptions.CollectReferencedGlobals"/>
public sealed class ReferencedGlobals : IReadOnlyCollection<string>
{
    private readonly string[] _names;
#if NET8_0_OR_GREATER
    private readonly FrozenSet<string> _set;
#else
    private readonly HashSet<string> _set;
#endif

    internal ReferencedGlobals(string[] sortedNames, bool hasDirectEvalCall)
    {
        _names = sortedNames;
#if NET8_0_OR_GREATER
        _set = sortedNames.ToFrozenSet(StringComparer.Ordinal);
#else
        _set = new HashSet<string>(sortedNames, StringComparer.Ordinal);
#endif
        HasDirectEvalCall = hasDirectEvalCall;
    }

    /// <summary>
    /// The number of distinct free identifier names.
    /// </summary>
    public int Count => _names.Length;

    /// <summary>
    /// Whether the program contains a direct <c>eval</c> call — a call whose callee is the identifier <c>eval</c>,
    /// including the optional-call form <c>eval?.()</c>.
    /// <para>
    /// Dynamically evaluated code can reference names outside this set, so a host that installs only the listed
    /// globals should treat this flag as <em>install everything instead</em>. It is the honest limit of a static
    /// contract rather than a defect in the analysis.
    /// </para>
    /// <para>
    /// Indirect eval (<c>(0, eval)(src)</c>) and the <c>Function</c> constructor also evaluate dynamic code and are
    /// deliberately not flagged; <c>eval</c> or <c>Function</c> then appears in the set itself, which is the signal a
    /// host can act on.
    /// </para>
    /// </summary>
    public bool HasDirectEvalCall { get; }

    /// <summary>
    /// Whether <paramref name="name"/> is one of the free identifier names. Ordinal comparison, O(1).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool Contains(string name)
    {
        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        return _set.Contains(name);
    }

    /// <summary>
    /// Enumerates the free identifier names, ordinal-sorted and deduplicated.
    /// </summary>
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>) _names).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _names.GetEnumerator();
}
