using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jint.SourceGenerators;

internal class ObjectDefinition
{
    public ObjectDefinition(
        ClassDeclarationSyntax syntax,
        List<FunctionDefinition> functions,
        List<PropertyDefinition> properties)
    {
        Namespace = SourceGenerationHelper.GetNamespace(syntax);
        Name = syntax.Identifier.ToString();
        Syntax = syntax;
        Functions = functions;
        Properties = properties;

        PropertyLookup = SourceGenerationHelper.GenerateLookups(false, this, "str", static (sb, indentStr, item) =>
        {
            sb.Append(indentStr);
            sb.AppendLine("        {");
            sb.Append(indentStr);
            sb.Append("            ").Append("match").Append(" = ");
            sb.Append(item.Accessor);
            sb.AppendLine(";");
        }, "_property", 12);

        PropertySet = SourceGenerationHelper.GenerateLookups(false, this, "str", static (sb, indentStr, item) =>
        {
            sb.Append(indentStr);
            sb.AppendLine("        {");
            sb.Append(indentStr);
            sb.Append("            ").Append("match").Append(" = ");
            sb.Append(item.Accessor);
            sb.AppendLine(";");
        }, "_property", 12);

        ValueLookup = SourceGenerationHelper.GenerateLookups(true, this, "str", static (sb, indentStr, item) =>
        {
            sb.Append(indentStr);
            sb.AppendLine("        {");
            sb.Append(indentStr);
            sb.Append("            ").Append(item.Accessor).Append("_backingField").Append(" = ");
            sb.Append("value");
            sb.AppendLine(";");
        }, "", 12);
    }

    public string Namespace { get; }

    public string Name { get; }
    public ClassDeclarationSyntax Syntax { get; }

    public List<FunctionDefinition> Functions { get; }
    public List<PropertyDefinition> Properties { get; }
    public string PropertyLookup { get; }
    public string PropertySet { get; }
    public string ValueLookup { get; }

}
