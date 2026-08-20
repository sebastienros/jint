#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Base64;

/// <summary>
/// The global <c>atob</c> and <c>btoa</c> functions.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#atob
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// They are the two operations of HTML's <c>WindowOrWorkerGlobalScope</c> mixin that have nothing to do
/// with a window, which is why they belong on any global at all. One type serves both because the pair is
/// symmetric and each is a single call: two function objects per realm, built only if a script names one.
/// </para>
/// <para>
/// <c>btoa(data)</c> takes a <c>DOMString</c>, not a <c>USVString</c>: a lone surrogate is not silently
/// replaced by U+FFFD, it is a code point above U+00FF like any other and raises an
/// <c>InvalidCharacterError</c>. <c>atob</c> returns a <c>ByteString</c>, so every code unit of the result
/// is in the range U+0000 to U+00FF.
/// </para>
/// </remarks>
internal sealed class Base64Function : Native.Function.Function
{
    private static readonly JsString _atobName = new("atob");
    private static readonly JsString _btoaName = new("btoa");

    private readonly bool _isAtob;

    internal Base64Function(Engine engine, Realm realm, FunctionPrototype functionPrototype, bool isAtob)
        : base(engine, realm, isAtob ? _atobName : _btoaName)
    {
        _isAtob = isAtob;
        _prototype = functionPrototype;
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var data = arguments.At(0);
        return _isAtob ? Atob(data) : Btoa(data);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-atob — "forgiving-base64 decode", and an
    /// <c>InvalidCharacterError</c> for its failure.
    /// </summary>
    private JsString Atob(JsValue data)
    {
        var text = TypeConverter.ToString(data);

        if (!ForgivingBase64.TryDecode(text, out var bytes))
        {
            ThrowInvalidCharacterError("The string to be decoded is not correctly encoded");
            return null!;
        }

        if (bytes.Length == 0)
        {
            return JsString.Empty;
        }

        // A ByteString: one code unit per byte, so Latin-1 is the encoding by definition rather than by
        // choice.
        return JsString.Create(System.Text.Encoding.Latin1.GetString(bytes));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-btoa — one byte per code unit, then
    /// "forgiving-base64 encode", which is plain RFC 4648 base64.
    /// </summary>
    private JsString Btoa(JsValue data)
    {
        var text = TypeConverter.ToString(data);

        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c > 0xFF)
            {
                ThrowInvalidCharacterError("The string to be encoded contains characters outside of the Latin1 range");
            }

            bytes[i] = (byte) c;
        }

        return JsString.Create(Convert.ToBase64String(bytes));
    }

    /// <summary>
    /// Raises the <c>"InvalidCharacterError"</c> <c>DOMException</c> both operations report their one
    /// failure with. It is thrown as the error <i>value</i>, so script catches a real <c>DOMException</c>
    /// and can read its <c>name</c> and <c>code</c>.
    /// </summary>
    [DoesNotReturn]
    private void ThrowInvalidCharacterError(string message)
    {
        var error = _realm.Intrinsics.DomException.CreateException(DomExceptionNames.InvalidCharacter, message);
        Throw.JavaScriptException(_engine, error, _engine.GetLastSyntaxElement()?.Location ?? default);
    }
}
#endif
