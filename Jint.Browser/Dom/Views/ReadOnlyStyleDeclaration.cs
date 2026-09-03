using System.Collections;
using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// What <c>getComputedStyle</c> answers: the cascade's declarations over ten resolved values, with every way
/// of writing to them refused.
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://drafts.csswg.org/cssom/#dom-window-getcomputedstyle">CSSOM</a> gives the returned
/// declaration a <i>computed flag</i>, and a declaration carrying it throws a
/// <c>NoModificationAllowedError</c> from <c>setProperty</c>, <c>removeProperty</c>, the <c>cssText</c> setter
/// and every CSS property setter. AngleSharp's <c>ComputeCurrentStyle()</c> hands back an ordinary writable
/// declaration that is also <em>detached</em> — a fresh object per call, not the element's style — so writing
/// to it in a browser throws and here would silently change nothing anybody can read. Refusing the write is
/// the difference between a page learning it has a bug and a page not.
/// </para>
/// <para>
/// The refusal is a real <c>DOMException</c> thrown as the error value, so <c>catch (e) { e.name }</c>
/// answers <c>"NoModificationAllowedError"</c>. That is only possible because this object is reachable from
/// script alone: nothing inside AngleSharp ever calls it, so a JavaScript exception can never unwind through
/// AngleSharp's own frames from here.
/// </para>
/// <para>
/// A read is the cascade first: a property the stylesheets, the inline style or the user-agent defaults
/// settled answers exactly what they settled, inheritance included. Where the cascade declared nothing,
/// <see cref="ResolvedStyle"/> answers for the ten properties an automation client reads to decide that an
/// element can be interacted with, and the empty string for everything else — that file argues which ten and
/// why no more.
/// </para>
/// <para>
/// <b><c>length</c> and <c>item(i)</c> stay the declared set.</b> CSSOM enumerates every supported longhand
/// there, which is some three hundred names a browser answers and this has no values for; publishing ten of
/// them as if they were the list would be a worse answer than the honest short one. A page reads a resolved
/// value by name.
/// </para>
/// <para>
/// <b>The cascade can be absent altogether</b>, because AngleSharp.Css raises rather than answers for a
/// relative length — <c>Runtime/WindowInstaller.Cascade</c> has the whole of it. Then every read is the
/// resolved value or the empty string, and nothing throws.
/// </para>
/// </remarks>
internal sealed class ReadOnlyStyleDeclaration : ICssStyleDeclaration
{
    private readonly ICssStyleDeclaration? _computed;
    private readonly Engine _engine;
    private readonly IElement _element;
    private readonly PageRuntime _runtime;

    internal ReadOnlyStyleDeclaration(PageRuntime runtime, IElement element, ICssStyleDeclaration? computed)
    {
        _runtime = runtime;
        _engine = runtime.Engine;
        _element = element;
        _computed = computed;
    }

    /// <inheritdoc />
    public string this[int index] => _computed is null ? "" : _computed[index];

    /// <inheritdoc />
    public string this[string name] => Resolve(name, _computed?[name]);

    /// <inheritdoc />
    public int Length => _computed?.Length ?? 0;

    /// <inheritdoc />
    public ICssRule? Parent => _computed?.Parent;

    /// <inheritdoc />
    public string CssText
    {
        get => _computed?.CssText ?? "";
        set => Refuse("cssText");
    }

    /// <inheritdoc />
    public event Action<string>? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public string GetPropertyValue(string propertyName)
        => Resolve(propertyName, _computed?.GetPropertyValue(propertyName));

    /// <inheritdoc />
    public ICssProperty GetProperty(string propertyName) => _computed?.GetProperty(propertyName)!;

    /// <inheritdoc />
    public string GetPropertyPriority(string propertyName) => _computed?.GetPropertyPriority(propertyName) ?? "";

    /// <inheritdoc />
    public void SetProperty(string propertyName, string propertyValue, string? priority = null) => Refuse("setProperty");

    /// <inheritdoc />
    public string RemoveProperty(string propertyName)
    {
        Refuse("removeProperty");
        return "";
    }

    /// <inheritdoc />
    public void SetParent(ICssRule? rule)
    {
    }

    /// <inheritdoc />
    public void Update(string value) => Refuse("cssText");

    /// <inheritdoc />
    public void ToCss(TextWriter writer, IStyleFormatter formatter) => _computed?.ToCss(writer, formatter);

    /// <inheritdoc />
    public IEnumerator<ICssProperty> GetEnumerator()
        => _computed?.GetEnumerator() ?? Enumerable.Empty<ICssProperty>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The cascade's answer, or the resolved value where the cascade declared nothing.
    /// </summary>
    /// <param name="propertyName">The property that was read.</param>
    /// <param name="declared">What the cascade answered, which is the empty string for an undeclared one.</param>
    private string Resolve(string propertyName, string? declared)
        => string.IsNullOrEmpty(declared)
            ? ResolvedStyle.ValueOf(propertyName, _element, _runtime) ?? ""
            : declared;

    private void Refuse(string member)
    {
        var error = _engine._mainRealm.Intrinsics.DomException.CreateException(
            DomExceptionNames.NoModificationAllowed,
            "Failed to execute '" + member + "' on 'CSSStyleDeclaration': These styles are computed, and therefore read-only.");

        Throw.JavaScriptException(_engine, error, _engine.GetLastSyntaxElement()?.Location ?? default);
    }
}
