using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jint.SourceGenerators;

/// <summary>
/// Helpers from mostly based Andrew Lock's work: https://andrewlock.net/series/creating-a-source-generator/
/// </summary>
internal static class SourceGenerationHelper
{
    public const string Attributes = @"
namespace Jint;

[System.AttributeUsage(System.AttributeTargets.Class)]
internal class JsObjectAttribute : System.Attribute
{
}

[System.AttributeUsage(System.AttributeTargets.Method)]
internal class JsFunctionAttribute : System.Attribute
{
    public string Name { get; set; }
    public int Length { get; set; }
}

[System.AttributeUsage(System.AttributeTargets.Property)]
internal class JsPropertyAttribute : System.Attribute
{
    public string Name { get; set; }
    public Jint.Runtime.Descriptors.PropertyFlag Flags { get; set; }
}
";

    /// <summary>
    /// Builds optimized value lookup using known facts about keys.
    /// </summary>
    internal static string GenerateLookups(
        bool value,
        ObjectDefinition obj,
        string source,
        Action<StringBuilder, string, (string Name, string Accessor)> doAction,
        string fieldPostfix,
        int indent)
    {
        var sb = new StringBuilder();

        var indentStr = new string(' ', indent);

        sb.Append("switch (");
        sb.Append(source);
        sb.AppendLine(".Length)");
        sb.Append(indentStr);
        sb.AppendLine("{");

        var definitions = obj.Functions
            .Select(x => (x.Name, Accessor: "__" + x.Name + fieldPostfix))
            .Concat(obj.Properties.Select(x => (x.Name, Accessor: value ? x.Accessor : "__" + x.Name + fieldPostfix)));

        foreach (var group in definitions.ToLookup(x => x.Name.Length).OrderBy(x => x.Key))
        {
            sb.Append(indentStr);
            sb.Append("    case ");
            sb.Append(group.Key);
            sb.AppendLine(":");

            var discriminatorIndex = group.Count() > 1 ? FindDiscriminatorIndex(group) : -1;

            if (discriminatorIndex != -1)
            {
                sb.Append(indentStr);
                sb.Append("        var disc").Append(group.Key).Append(" = ").Append(source).Append("[").Append(discriminatorIndex).AppendLine("];");
            }
            else if (group.Count() > 1)
            {
                // going to be slow
            }

            var first = true;
            foreach (var item in group)
            {
                sb.Append(indentStr);
                sb.Append("        ");
                if (!first)
                {
                    sb.Append("else ");
                }
                sb.Append("if (");
                if (discriminatorIndex != -1)
                {
                    sb.Append("disc").Append(group.Key).Append(" == '").Append(item.Name[discriminatorIndex]).Append("' && ");
                }
                sb.Append(source);
                sb.Append(" == \"");
                sb.Append(item.Name);
                sb.AppendLine("\")");

                doAction(sb, indentStr, item);

                sb.Append(indentStr);
                sb.AppendLine("        }");

                first = false;
            }

            sb.AppendLine("                    break;");
            sb.AppendLine();
        }


        sb.AppendLine("            }");

        return sb.ToString();
    }

    private static int FindDiscriminatorIndex(IGrouping<int, (string Name, string Accessor)> grouping)
    {
        var chars = new HashSet<char>();
        for (var i = 0; i < grouping.Key; ++i)
        {
            chars.Clear();
            var allDifferent = true;
            foreach (var item in grouping)
            {
                allDifferent &= chars.Add(item.Name[i]);
                if (!allDifferent)
                {
                    break;
                }
            }

            if (allDifferent)
            {
                return i;
            }
        }

        // not found
        return -1;
    }

    // determine the namespace the class/enum/struct is declared in, if any
    public static string GetNamespace(BaseTypeDeclarationSyntax syntax)
    {
        // If we don't have a namespace at all we'll return an empty string
        // This accounts for the "default namespace" case
        var nameSpace = string.Empty;

        // Get the containing syntax node for the type declaration
        // (could be a nested type, for example)
        var potentialNamespaceParent = syntax.Parent;

        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            // We have a namespace. Use that as the type
            nameSpace = namespaceParent.Name.ToString();

            // Keep moving "out" of the namespace declarations until we
            // run out of nested namespace declarations
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
                {
                    break;
                }

                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }
        }

        // return the final namespace
        return nameSpace;
    }

    public static Dictionary<string, string> GetAttributes(AttributeSyntax syntax)
    {
        var attributes = new Dictionary<string, string>();
        foreach (var item in syntax.ChildNodes())
        {
            if (item is AttributeArgumentListSyntax arguments)
            {
                foreach (var ag in arguments.Arguments)
                {
                    attributes.Add(ag.NameEquals!.Name.NormalizeWhitespace().ToFullString(), ag!.Expression.ChildTokens().FirstOrDefault().Value?.ToString() ?? "");
                }
            }
        }

        return attributes;
    }
}
