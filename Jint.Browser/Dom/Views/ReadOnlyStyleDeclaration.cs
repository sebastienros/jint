using System.Collections;
using AngleSharp;
using AngleSharp.Css.Dom;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// What <c>getComputedStyle</c> answers: the cascade's declarations, with every way of writing to them
/// refused.
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
/// Reads pass straight through, and what they can answer is the cascade with no layout behind it: a property
/// the stylesheets, the inline style or the user-agent defaults settled resolves, and a used value that would
/// need a box — <c>width</c>, <c>height</c>, anything resolved against a containing block — is the empty
/// string. <c>display</c> resolves, because it comes from the cascade and not from layout.
/// </para>
/// </remarks>
internal sealed class ReadOnlyStyleDeclaration : ICssStyleDeclaration
{
    private readonly ICssStyleDeclaration _computed;
    private readonly Engine _engine;

    internal ReadOnlyStyleDeclaration(Engine engine, ICssStyleDeclaration computed)
    {
        _engine = engine;
        _computed = computed;
    }

    /// <inheritdoc />
    public string this[int index] => _computed[index];

    /// <inheritdoc />
    public string this[string name] => _computed[name];

    /// <inheritdoc />
    public int Length => _computed.Length;

    /// <inheritdoc />
    public ICssRule? Parent => _computed.Parent;

    /// <inheritdoc />
    public string CssText
    {
        get => _computed.CssText;
        set => Refuse("cssText");
    }

    /// <inheritdoc />
    public event Action<string>? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public string GetPropertyValue(string propertyName) => _computed.GetPropertyValue(propertyName);

    /// <inheritdoc />
    public ICssProperty GetProperty(string propertyName) => _computed.GetProperty(propertyName)!;

    /// <inheritdoc />
    public string GetPropertyPriority(string propertyName) => _computed.GetPropertyPriority(propertyName);

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
    public void ToCss(TextWriter writer, IStyleFormatter formatter) => _computed.ToCss(writer, formatter);

    /// <inheritdoc />
    public IEnumerator<ICssProperty> GetEnumerator() => _computed.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Refuse(string member)
    {
        var error = _engine._mainRealm.Intrinsics.DomException.CreateException(
            DomExceptionNames.NoModificationAllowed,
            "Failed to execute '" + member + "' on 'CSSStyleDeclaration': These styles are computed, and therefore read-only.");

        Throw.JavaScriptException(_engine, error, _engine.GetLastSyntaxElement()?.Location ?? default);
    }
}
