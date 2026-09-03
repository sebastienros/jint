using System.Globalization;
using System.Text;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// CSSOM's <c>CSS</c> namespace: <c>escape</c> and <c>supports</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a namespace object, not an interface</b> — <a href="https://drafts.csswg.org/cssom/#namespacedef-css">
/// CSSOM</a> declares <c>namespace CSS</c>, so there is no constructor, no prototype and no instances, and
/// <c>Object.prototype.toString.call(CSS)</c> is <c>[object CSS]</c>. <c>NodeFilter</c> is the same shape of
/// thing here for the same reason.
/// </para>
/// <para>
/// <b>Both members are here because half of one is a trap.</b> htmx 2 needs <c>CSS.escape</c> — it builds
/// <c>'#' + CSS.escape(id)</c> for every out-of-band swap — and a page that tests
/// <c>window.CSS &amp;&amp; CSS.supports(…)</c>, which is how the feature is detected, would find a truthy
/// <c>CSS</c> whose <c>supports</c> is <see langword="undefined"/> and fail on the <i>second</i> half.
/// </para>
/// <para>
/// <b><c>supports</c> is AngleSharp.Css's own condition evaluator</b>, reached the only way the library
/// exposes it: the condition is parsed as an <c>@supports</c> rule and
/// <c>IConditionFunction.Check</c> answers it. So the set of properties this claims to support is exactly the
/// set that library implements, which is also the set the cascade can act on — one answer rather than two.
/// </para>
/// </remarks>
internal static class JsCssNamespace
{
    private static readonly CssParser _parser = new();

    /// <summary>
    /// https://drafts.csswg.org/cssom/#the-css.escape()-method — CSS's "serialize an identifier".
    /// </summary>
    internal static JsValue Escape(JsValue[] arguments)
    {
        var identifier = TypeConverter.ToString(arguments.At(0));
        var builder = new StringBuilder(identifier.Length);

        for (var i = 0; i < identifier.Length; i++)
        {
            var character = identifier[i];

            // https://drafts.csswg.org/cssom/#serialize-an-identifier, in its own order.
            if (character == '\0')
            {
                builder.Append('\uFFFD');
            }
            else if (character <= '\u001F' || character == '\u007F'
                || (i == 0 && IsDigit(character))
                || (i == 1 && IsDigit(character) && identifier[0] == '-'))
            {
                builder.Append('\\')
                    .Append(((int) character).ToString("x", CultureInfo.InvariantCulture))
                    .Append(' ');
            }
            else if (i == 0 && character == '-' && identifier.Length == 1)
            {
                builder.Append("\\-");
            }
            else if (character >= '\u0080' || character == '-' || character == '_' || IsDigit(character)
                || (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z'))
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('\\').Append(character);
            }
        }

        return JsString.Create(builder.ToString());
    }

    /// <summary>
    /// https://drafts.csswg.org/css-conditional-3/#dom-css-supports — the one-argument and two-argument forms.
    /// </summary>
    /// <remarks>
    /// The two-argument form is a declaration wrapped in parentheses, which is what the standard says it is;
    /// a value carrying an unbalanced parenthesis therefore makes the whole condition unparseable and answers
    /// <see langword="false"/>, where a browser reads the value with a proper value parser and answers the
    /// same thing for a different reason.
    /// </remarks>
    internal static JsValue Supports(JsValue[] arguments)
    {
        var condition = arguments.Length >= 2
            ? "(" + TypeConverter.ToString(arguments.At(0)) + ":" + TypeConverter.ToString(arguments.At(1)) + ")"
            : TypeConverter.ToString(arguments.At(0));

        return JsBoolean.Create(IsSupported(condition));
    }

    private static bool IsSupported(string condition)
    {
        try
        {
            var sheet = _parser.ParseStyleSheet("@supports " + condition + " { }");

            foreach (var rule in sheet.Rules)
            {
                if (rule is ICssSupportsRule supports)
                {
                    return supports.Condition.Check(new DefaultRenderDevice());
                }
            }
        }
        catch (Exception exception) when (exception is not JavaScriptException)
        {
            // A condition the parser cannot read is one this does not support, which is what the standard's
            // "return false" step amounts to. AngleSharp raises rather than answering for some of them.
            return false;
        }

        return false;
    }

    private static bool IsDigit(char character) => character >= '0' && character <= '9';
}
