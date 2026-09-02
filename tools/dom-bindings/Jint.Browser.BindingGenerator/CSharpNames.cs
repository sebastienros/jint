using System.Reflection;
using System.Text;

namespace Jint.Browser.BindingGenerator;

/// <summary>Renders a <see cref="System.Reflection.MetadataLoadContext"/> type as C# source.</summary>
internal static class CSharpNames
{
    /// <summary>A fully qualified, alias-proof type name — always <c>global::</c>-rooted.</summary>
    internal static string Render(Type type)
    {
        if (type.IsArray)
        {
            return Render(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var name = definition.FullName!;
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick >= 0)
            {
                name = name[..tick];
            }

            // An open generic definition can only appear inside `typeof(...)`, where C# spells it with the
            // arity as commas — `IHtmlCollection<>`. AngleSharp has exactly one, and it is HTMLCollection.
            if (type.IsGenericTypeDefinition)
            {
                return "global::" + name.Replace('+', '.') + "<" + new string(',', type.GetGenericArguments().Length - 1) + ">";
            }

            var builder = new StringBuilder("global::").Append(name.Replace('+', '.')).Append('<');
            var arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Render(arguments[i]));
            }

            return builder.Append('>').ToString();
        }

        return "global::" + type.FullName!.Replace('+', '.');
    }

    /// <summary>A C# string literal, escaped.</summary>
    internal static string Literal(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\r': builder.Append("\\r"); break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>
    /// How a member is reached from a variable holding the declaring interface. An explicitly named property
    /// — AngleSharp re-declares <c>IReadOnlyList&lt;T&gt;.Item</c> on several of its collections — has to be
    /// reached through a cast rather than by name.
    /// </summary>
    internal static bool IsExplicitlyNamed(MemberInfo member) => member.Name.Contains('.', StringComparison.Ordinal);

    /// <summary>A C# identifier built from a DOM name, for a generated field or method.</summary>
    internal static string Identifier(string domName)
    {
        var builder = new StringBuilder(domName.Length);
        foreach (var c in domName)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}
