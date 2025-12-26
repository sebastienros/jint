using System.Text.RegularExpressions;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintLiteralExpression : JintExpression
{
    private static readonly object _nullMarker = new();

    // Regex to match unicode escape sequences like \u2028, \u2029
    private static readonly Regex UnicodeEscapePattern = new Regex(@"\\u([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    private JintLiteralExpression(Literal expression) : base(expression)
    {
    }

    /// <summary>
    /// Converts unicode escape sequences (e.g., \u2028) to actual unicode characters.
    /// </summary>
    private static string UnescapeUnicode(string pattern)
    {
        return UnicodeEscapePattern.Replace(pattern, match =>
        {
            var hex = match.Groups[1].Value;
            var codePoint = int.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            return ((char)codePoint).ToString();
        });
    }

    internal static JintExpression Build(Literal expression)
    {
        var value = expression.UserData ??= ConvertToJsValue(expression) ?? _nullMarker;

        if (value is JsValue constant)
        {
            return new JintConstantExpression(expression, constant);
        }

        return new JintLiteralExpression(expression);
    }

    internal static JsValue? ConvertToJsValue(Literal literal)
    {
        switch (literal.Kind)
        {
            case TokenKind.BooleanLiteral:
                return ((BooleanLiteral) literal).Value ? JsBoolean.True : JsBoolean.False;
            case TokenKind.NullLiteral:
                return JsValue.Null;
            case TokenKind.NumericLiteral:
                {
                    var numericValue = ((NumericLiteral) literal).Value;
                    var intValue = (int) numericValue;
                    return numericValue == intValue
                           && (intValue != 0 || BitConverter.DoubleToInt64Bits(numericValue) != JsNumber.NegativeZeroBits)
                        ? JsNumber.Create(intValue)
                        : JsNumber.Create(numericValue);
                }
            case TokenKind.StringLiteral:
                return JsString.Create(((StringLiteral) literal).Value);
            case TokenKind.BigIntLiteral:
                return JsBigInt.Create(((BigIntLiteral) literal).Value);
            case TokenKind.RegExpLiteral:
                break;
        }

        return null;
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;
        return ResolveValue(context);
    }

    protected override object EvaluateInternal(EvaluationContext context) => ResolveValue(context);

    private JsValue ResolveValue(EvaluationContext context)
    {
        var expression = (Literal) _expression;
        if (expression is RegExpLiteral regExpLiteral)
        {
            var regExpParseResult = regExpLiteral.ParseResult;
            if (regExpParseResult.Success)
            {
                var regex = regExpLiteral.UserData as Regex ?? regExpParseResult.Regex;

                // Fallback: if Acornima failed to create Regex (e.g., due to unicode escapes), create it manually
                if (regex is null)
                {
                    var pattern = regExpLiteral.RegExp.Pattern;
                    var flags = regExpLiteral.RegExp.Flags;

                    // Convert unicode escape sequences to actual characters
                    var unescapedPattern = UnescapeUnicode(pattern);

                    var options = RegexOptions.ECMAScript;
                    if (flags.Contains('i')) options |= RegexOptions.IgnoreCase;
                    if (flags.Contains('m')) options |= RegexOptions.Multiline;

                    try
                    {
                        regex = new Regex(unescapedPattern, options);
                    }
                    catch
                    {
                        Throw.SyntaxError(context.Engine.Realm, $"Invalid regular expression pattern: {pattern}");
                    }
                }

                return context.Engine.Realm.Intrinsics.RegExp.Construct(regex!, regExpLiteral.RegExp.Pattern, regExpLiteral.RegExp.Flags, regExpParseResult);
            }

            Throw.SyntaxError(context.Engine.Realm, $"Unsupported regular expression. {regExpParseResult.ConversionError!.Description}");
        }

        return JsValue.FromObject(context.Engine, expression.Value);
    }
}
