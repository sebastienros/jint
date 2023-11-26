using System.Collections.Immutable;
using System.Text;
using Fluid;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Jint.SourceGenerators;

[Generator]
public class ObjectGenerator : IIncrementalGenerator
{
    private IFluidTemplate _objectTemplate = null!;

    private const string JsObjectAttribute = "Jint.JsObjectAttribute";
    private const string JsFunctionAttribute = "Jint.JsFunctionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        using var stream = new StreamReader(typeof(ObjectGenerator).Assembly.GetManifestResourceStream(typeof(ObjectGenerator), "Templates.JsObject.liquid")!);
        var template = stream.ReadToEnd();

        var parser = new FluidParser();
        _objectTemplate = parser.Parse(template);

        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "Attributes.g.cs", SourceText.From(SourceGenerationHelper.Attributes, Encoding.UTF8)));

        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses,
            (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax) context.Node;

        // loop through all the attributes on the method
        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attributeSyntax);
                IMethodSymbol symbol;
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                {
                    symbol = methodSymbol;
                }
                else if (symbolInfo.CandidateSymbols.Length > 0 && symbolInfo.CandidateSymbols[0] is IMethodSymbol fromCandidate)
                {
                    symbol = fromCandidate;
                }
                else
                {
                    // weird, we couldn't get the symbol, ignore it
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = symbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                // Is the attribute the [JsObject] attribute?
                if (fullName == JsObjectAttribute)
                {
                    // return the class
                    return classDeclarationSyntax;
                }
            }
        }

        // we didn't find the attribute we were looking for
        return null;
    }

    private void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var distinctClasses = classes.Distinct();
        var toGenerate = GetTypesToGenerate(compilation, distinctClasses, context.CancellationToken);

        var templateOptions = new TemplateOptions { MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
        foreach (var target in toGenerate)
        {
            // generate the source code and add it to the output
            var templateContext = new TemplateContext(target, templateOptions);
            var result = _objectTemplate.Render(templateContext);
            context.AddSource(target.Name + ".g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    private static List<ObjectDefinition> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> classes, CancellationToken ct)
    {
        var classesToGenerate = new List<ObjectDefinition>();
        var objectAttribute = compilation.GetTypeByMetadataName(JsObjectAttribute);
        if (objectAttribute == null)
        {
            // nothing to do if this type isn't available
            return classesToGenerate;
        }

        foreach (var classDeclarationSyntax in classes)
        {
            // stop if we're asked to
            ct.ThrowIfCancellationRequested();

            SemanticModel semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
            {
                // something went wrong
                continue;
            }

            var allMembers = classSymbol.GetMembers();
            var functions = new List<FunctionDefinition>();
            var properties = new List<PropertyDefinition>();

            foreach (ISymbol member in allMembers)
            {
                if (member is IMethodSymbol method)
                {
                    foreach (var attribute in method.GetAttributes())
                    {
                        if (attribute.AttributeClass?.Name == "JsFunctionAttribute")
                        {
                            functions.Add(new FunctionDefinition(method, attribute));
                            break;
                        }
                    }
                }

                if (member is IPropertySymbol property)
                {
                    foreach (var attribute in property.GetAttributes())
                    {
                        if (attribute.AttributeClass?.Name == "JsPropertyAttribute")
                        {
                            properties.Add(new PropertyDefinition(property, attribute));
                            break;
                        }
                    }
                }
            }

            functions.Sort();
            classesToGenerate.Add(new ObjectDefinition(classDeclarationSyntax, functions, properties));
        }

        return classesToGenerate;
    }
}
