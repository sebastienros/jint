using System.Text;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Interop;

/// <summary>
/// Builds detailed error messages for failed interop member resolution. Cold path only,
/// invoked right before throwing.
/// </summary>
internal static class InteropErrorHelper
{
    private const int MaxCandidatesToReport = 5;

    public static string CreateNoMatchingMethodMessage(Type targetType, string name, JsCallArguments arguments, MethodDescriptor[] methods)
    {
        var sb = new StringBuilder("No public methods with the specified arguments were found. Target: ");
        sb.Append(targetType).Append('.').Append(name);
        AppendArguments(sb, arguments);
        AppendCandidates(sb, methods);
        return sb.ToString();
    }

    public static string CreateNoMatchingConstructorMessage(Type type, JsCallArguments arguments, MethodDescriptor[]? constructors)
    {
        var sb = new StringBuilder("Could not resolve a constructor for type ");
        sb.Append(type).Append(" for given arguments");
        AppendArguments(sb, arguments);
        AppendCandidates(sb, constructors ?? []);
        return sb.ToString();
    }

    private static void AppendArguments(StringBuilder sb, JsCallArguments arguments)
    {
        sb.Append("; provided arguments: (");
        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append(DescribeArgument(arguments[i]));
        }
        sb.Append(')');
    }

    private static void AppendCandidates(StringBuilder sb, MethodDescriptor[] methods)
    {
        sb.Append("; candidate signatures: ");
        if (methods.Length == 0)
        {
            sb.Append("none");
            return;
        }

        var reported = Math.Min(methods.Length, MaxCandidatesToReport);
        for (var i = 0; i < reported; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append(methods[i]);
        }

        if (methods.Length > reported)
        {
            sb.Append(" and ").Append(methods.Length - reported).Append(" more");
        }
    }

    /// <summary>
    /// Describes an argument without calling ToObject(), which can invoke getters and
    /// deep-convert objects - unwanted side effects on an error path.
    /// </summary>
    private static string DescribeArgument(JsValue value)
    {
        return value switch
        {
            null => "<missing>",
            JsUndefined => "undefined",
            JsNull => "null",
            JsBoolean => "boolean",
            JsString => "string",
            JsNumber => "number",
            JsBigInt => "bigint",
            JsSymbol => "symbol",
            // TypeReference implements IObjectWrapper with Target == the Type itself, match it first
            TypeReference typeReference => typeReference.ReferenceType.Name,
            IObjectWrapper wrapper => wrapper.Target?.GetType().Name ?? "null",
            JsArray => "Array",
            JsDate => "Date",
            Function => "function",
            ObjectInstance => "object",
            _ => value.Type.ToString(),
        };
    }
}
