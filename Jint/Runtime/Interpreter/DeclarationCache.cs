using System.Runtime.InteropServices;

namespace Jint.Runtime.Interpreter;

internal readonly record struct DeclarationCache(List<ScopedDeclaration> Declarations, bool AllLexicalScoped);

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ScopedDeclaration(Key[] BoundNames, bool IsConstantDeclaration, Node Declaration);

internal static class DeclarationCacheBuilder
{
    public static DeclarationCache Build(List<Declaration>? lexicalDeclarations)
    {
        if (lexicalDeclarations is null)
        {
            return new DeclarationCache([], AllLexicalScoped: true);
        }

        var allLexical = true;
        List<Key> boundNames = [];
        List<ScopedDeclaration> declarations = [];
        for (var i = 0; i < lexicalDeclarations.Count; i++)
        {
            var d = lexicalDeclarations[i];
            Collect(boundNames, d, ref allLexical, declarations);
        }

        return new DeclarationCache(declarations, allLexical);
    }

    public static DeclarationCache Build(BlockStatement statement)
    {
        var allLexical = true;
        List<Key> boundNames = [];
        List<ScopedDeclaration> declarations = [];

        ref readonly var statementListItems = ref statement.Body;
        foreach (var node in statementListItems.AsSpan())
        {
            if (node.Type != NodeType.VariableDeclaration && node.Type != NodeType.FunctionDeclaration && node.Type != NodeType.ClassDeclaration)
            {
                continue;
            }

            if (node is VariableDeclaration { Kind: VariableDeclarationKind.Var })
            {
                continue;
            }

            Collect(boundNames, node, ref allLexical, declarations);
        }

        return new DeclarationCache(declarations, allLexical);
    }

    public static DeclarationCache Build(SwitchCase statement)
    {
        var allLexical = true;
        List<Key> boundNames = [];
        List<ScopedDeclaration> declarations = [];

        ref readonly var statementListItems = ref statement.Consequent;
        foreach (var node in statementListItems.AsSpan())
        {
            if (node.Type != NodeType.VariableDeclaration)
            {
                continue;
            }

            var rootVariable = (VariableDeclaration)node;
            if (rootVariable.Kind == VariableDeclarationKind.Var)
            {
                continue;
            }

            Collect(boundNames, node, ref allLexical, declarations);
        }

        return new DeclarationCache(declarations, allLexical);
    }

    private static void Collect(
        List<Key> boundNames,
        Node node,
        ref bool allLexical,
        List<ScopedDeclaration> declarations)
    {
        boundNames.Clear();
        node.GetBoundNames(boundNames);

        var isConstantDeclaration = false;
        if (node is VariableDeclaration variableDeclaration)
        {
            isConstantDeclaration = variableDeclaration.Kind == VariableDeclarationKind.Const;
            allLexical &= variableDeclaration.Kind != VariableDeclarationKind.Var;
        }
        else
        {
            allLexical = false;
        }

        declarations.Add(new ScopedDeclaration(boundNames.ToArray(), isConstantDeclaration, node));
    }
}

