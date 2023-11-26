using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jint.SourceGenerators;

internal class FunctionDefinition : IComparable
{
    public FunctionDefinition(IMethodSymbol method, AttributeData attribute)
    {
        var attributes = SourceGenerationHelper.GetAttributes((AttributeSyntax) attribute.ApplicationSyntaxReference!.GetSyntax());
        attributes.TryGetValue("Name", out var name);
        attributes.TryGetValue("Length", out var length);

        if (string.IsNullOrWhiteSpace(name))
        {
            name = method.Name;
            if (char.IsUpper(name[0]))
            {
                name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
        }

        Name = name ?? throw new InvalidOperationException("Could not get name");
        ClrName = method.Name;

        ProvideThis = method.Parameters.Any(x => x.Name.StartsWith("thisObj"));
        ProvideArguments = method.Parameters.Any(x => x.Name == "args" ||  x.Name.StartsWith("arguments"));

        IsStatic = method.IsStatic;

        if (string.IsNullOrWhiteSpace(length))
        {
            Length = method.Parameters.Length;
            if (ProvideThis)
            {
                Length--;
            }
        }
        else
        {
            Length = Convert.ToInt32(length);
        }

        ParametersString = "";
        var needsComma = false;
        if (ProvideThis)
        {
            needsComma = true;
            ParametersString += "thisObject";
        }

        var tmp = method.Parameters.Length;
        if (ProvideThis)
        {
            tmp--;
        }

        if (ProvideArguments)
        {
            tmp--;
        }

        for (var i = 0; i < tmp; ++i)
        {
            if (i > 0 || needsComma)
            {
                ParametersString += ", ";
            }

            ParametersString += "arguments.At(" + i + ")";
        }

        // arguments always last
        if (ProvideArguments)
        {
            if (needsComma)
            {
                ParametersString += ", ";
            }
            ParametersString += "arguments";
        }
    }

    public string Name { get; }
    public string ClrName { get; }
    public bool IsStatic { get; }
    public int Length { get; }

    public bool ProvideThis { get; }
    public bool ProvideArguments { get; }

    public string ParametersString { get; }

    public int CompareTo(object obj)
    {
        return string.Compare(Name, (obj as FunctionDefinition)?.Name, StringComparison.Ordinal);
    }
}
