using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jint.SourceGenerators;

internal class PropertyDefinition : IComparable
{
    public PropertyDefinition(IPropertySymbol property, AttributeData attribute)
    {
        var attributes = SourceGenerationHelper.GetAttributes((AttributeSyntax) attribute.ApplicationSyntaxReference!.GetSyntax());

        attributes.TryGetValue("Name", out var name);
       
        if (string.IsNullOrWhiteSpace(name))
        {
            name = property.Name;
            if (char.IsUpper(name[0]))
            {
                name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
        }
        Name = name ?? throw new InvalidOperationException("Could not get name");
        ClrName = property.Name;
        IsStatic = property.IsStatic;

        Accessor = IsStatic ? property.ContainingType.Name + "." + ClrName : ClrName;
    }

    public string Name { get; }
    public string ClrName { get; }
    public string Accessor { get; }
    public bool IsStatic { get; }

    public int CompareTo(object obj)
    {
        return string.Compare(Name, (obj as PropertyDefinition)?.Name, StringComparison.Ordinal);
    }
}