using System.Globalization;
using System.Text;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Values;
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
/// <b><c>supports</c> uses the native property and selector grammars.</b> Pending
/// grammar answers false. A capability query does not execute a stylesheet or evaluate a DOM match.
/// </para>
/// </remarks>
internal static class JsCssNamespace
{
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
    internal static JsValue Supports(DomRealm realm, JsValue[] arguments)
    {
        // WebIDL converts the arguments in order before starting native work.
        var first = TypeConverter.ToString(arguments.At(0));
        var second = arguments.Length >= 2 ? TypeConverter.ToString(arguments.At(1)) : null;
        var token = realm.Engine.Constraints.Find<Jint.Constraints.CancellationConstraint>()?.Token ?? default;
        var work = new CssValueWork(token, realm.Engine.Constraints.Check);
        var result = second is null
            ? CssSupports.EvaluateCondition(first, null, work)
            : CssSupports.EvaluateDeclaration(first, second, null, work);
        return JsBoolean.Create(result);
    }

    private static bool IsDigit(char character) => character >= '0' && character <= '9';
}
