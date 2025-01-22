using Jint.Runtime.Modules;

namespace Jint;

internal sealed class HoistingScope
{
    internal readonly List<FunctionDeclaration>? _functionDeclarations;

    internal readonly List<VariableDeclaration>? _variablesDeclarations;
    internal readonly List<Key>? _varNames;

    internal readonly List<Declaration>? _lexicalDeclarations;
    internal readonly List<string>? _lexicalNames;

    private HoistingScope(
        List<FunctionDeclaration>? functionDeclarations,
        List<Key>? varNames,
        List<VariableDeclaration>? variableDeclarations,
        List<Declaration>? lexicalDeclarations,
        List<string>? lexicalNames)
    {
        _functionDeclarations = functionDeclarations;
        _varNames = varNames;
        _variablesDeclarations = variableDeclarations;
        _lexicalDeclarations = lexicalDeclarations;
        _lexicalNames = lexicalNames;
    }

    public static HoistingScope GetProgramLevelDeclarations(
        Program script,
        bool collectVarNames = false,
        bool collectLexicalNames = false)
    {
        var treeWalker = new ScriptWalker(collectVarNames, collectLexicalNames);
        treeWalker.Visit(script, null);

        return new HoistingScope(
            treeWalker._functions,
            treeWalker._varNames,
            treeWalker._variableDeclarations,
            treeWalker._lexicalDeclarations,
            treeWalker._lexicalNames);
    }

    public static HoistingScope GetFunctionLevelDeclarations(bool strict, IFunction node)
    {
        var treeWalker = new ScriptWalker(collectVarNames: true, collectLexicalNames: true);
        treeWalker.Visit(node.Body, null);

        return new HoistingScope(
            treeWalker._functions,
            treeWalker._varNames,
            treeWalker._variableDeclarations,
            treeWalker._lexicalDeclarations,
            treeWalker._lexicalNames);
    }

    public static HoistingScope GetModuleLevelDeclarations(
        AstModule module,
        bool collectVarNames = false,
        bool collectLexicalNames = false)
    {
        // modules area always strict
        var treeWalker = new ScriptWalker(collectVarNames, collectLexicalNames);
        treeWalker.Visit(module, null);
        return new HoistingScope(
            treeWalker._functions,
            treeWalker._varNames,
            treeWalker._variableDeclarations,
            treeWalker._lexicalDeclarations,
            treeWalker._lexicalNames);
    }

    public static void GetImportsAndExports(
        AstModule module,
        out HashSet<ModuleRequest> requestedModules,
        out List<ImportEntry>? importEntries,
        out List<ExportEntry> localExportEntries,
        out List<ExportEntry> indirectExportEntries,
        out List<ExportEntry> starExportEntries)
    {
        var treeWalker = new ModuleWalker();
        treeWalker.Visit(module);

        importEntries = treeWalker._importEntries;
        requestedModules = treeWalker._requestedModules ?? [];
        var importedBoundNames = new HashSet<string?>(StringComparer.Ordinal);

        if (importEntries != null)
        {
            for (var i = 0; i < importEntries.Count; i++)
            {
                var ie = importEntries[i];

                if (ie.LocalName is not null)
                {
                    importedBoundNames.Add(ie.LocalName);
                }
            }
        }

        var exportEntries = treeWalker._exportEntries;
        localExportEntries = new();
        indirectExportEntries = new();
        starExportEntries = new();

        if (exportEntries != null)
        {
            for (var i = 0; i < exportEntries.Count; i++)
            {
                var ee = exportEntries[i];

                if (ee.ModuleRequest is null)
                {
                    if (!importedBoundNames.Contains(ee.LocalName))
                    {
                        localExportEntries.Add(ee);
                    }
                    else
                    {
                        for (var j = 0; j < importEntries!.Count; j++)
                        {
                            var ie = importEntries[j];
                            if (string.Equals(ie.LocalName, ee.LocalName, StringComparison.Ordinal))
                            {
                                if (string.Equals(ie.ImportName, "*", StringComparison.Ordinal))
                                {
                                    localExportEntries.Add(ee);
                                }
                                else
                                {
                                    indirectExportEntries.Add(new(ee.ExportName, ie.ModuleRequest, ie.ImportName, null));
                                }

                                break;
                            }
                        }
                    }
                }
                else if (string.Equals(ee.ImportName, "*", StringComparison.Ordinal) && ee.ExportName is null)
                {
                    starExportEntries.Add(ee);
                }
                else
                {
                    indirectExportEntries.Add(ee);
                }
            }
        }
    }

    private sealed class ScriptWalker
    {
        internal List<FunctionDeclaration>? _functions;

        private readonly bool _collectVarNames;
        internal List<VariableDeclaration>? _variableDeclarations;
        internal List<Key>? _varNames;

        private readonly bool _collectLexicalNames;
        internal List<Declaration>? _lexicalDeclarations;
        internal List<string>? _lexicalNames;

        public ScriptWalker(bool collectVarNames, bool collectLexicalNames)
        {
            _collectVarNames = collectVarNames;
            _collectLexicalNames = collectLexicalNames;
        }

        public void Visit(Node node, Node? parent)
        {
            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;
                if (childType == NodeType.VariableDeclaration)
                {
                    var variableDeclaration = (VariableDeclaration)childNode;
                    if (variableDeclaration.Kind == VariableDeclarationKind.Var)
                    {
                        _variableDeclarations ??= new List<VariableDeclaration>();
                        _variableDeclarations.Add(variableDeclaration);
                        if (_collectVarNames)
                        {
                            _varNames ??= new List<Key>();
                            ref readonly var nodeList = ref variableDeclaration.Declarations;
                            foreach (var declaration in nodeList)
                            {
                                if (declaration.Id is Identifier identifier)
                                {
                                    _varNames.Add(identifier.Name);
                                }
                            }
                        }
                    }

                    if (parent is null or AstModule && variableDeclaration.Kind != VariableDeclarationKind.Var)
                    {
                        _lexicalDeclarations ??= new List<Declaration>();
                        _lexicalDeclarations.Add(variableDeclaration);
                        if (_collectLexicalNames)
                        {
                            _lexicalNames ??= new List<string>();
                            ref readonly var nodeList = ref variableDeclaration.Declarations;
                            foreach (var declaration in nodeList)
                            {
                                if (declaration.Id is Identifier identifier)
                                {
                                    _lexicalNames.Add(identifier.Name);
                                }
                            }
                        }
                    }
                }
                else if (childType == NodeType.FunctionDeclaration)
                {
                    // function declarations are not hoisted if they are under block or case clauses
                    if (parent is null || (node.Type != NodeType.BlockStatement && node.Type != NodeType.SwitchCase))
                    {
                        _functions ??= new List<FunctionDeclaration>();
                        _functions.Add((FunctionDeclaration)childNode);
                    }
                }
                else if (childType == NodeType.ClassDeclaration && parent is null or AstModule)
                {
                    _lexicalDeclarations ??= new List<Declaration>();
                    _lexicalDeclarations.Add((Declaration) childNode);
                }

                if (childType != NodeType.FunctionDeclaration
                    && childType != NodeType.ArrowFunctionExpression
                    && childType != NodeType.FunctionExpression
                    && !childNode.ChildNodes.IsEmpty())
                {
                    Visit(childNode, node);
                }
            }
        }
    }

    private sealed class ModuleRequestRecordComparer : IComparer<ModuleRequest>
    {
        public int Compare(ModuleRequest x, ModuleRequest y)
        {
            return string.Compare(x.Specifier, y.Specifier, StringComparison.Ordinal);
        }
    }

    private sealed class ModuleWalker
    {
        internal List<ImportEntry>? _importEntries;
        internal List<ExportEntry>? _exportEntries;
        internal HashSet<ModuleRequest>? _requestedModules;

        internal void Visit(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.Type == NodeType.ImportDeclaration)
                {
                    _importEntries ??= [];
                    _requestedModules ??= [];
                    var import = (ImportDeclaration) childNode;
                    import.GetImportEntries(_importEntries, _requestedModules);
                }
                else if (childNode.Type is NodeType.ExportAllDeclaration or NodeType.ExportDefaultDeclaration or NodeType.ExportNamedDeclaration)
                {
                    _exportEntries ??= [];
                    _requestedModules ??= [];
                    var export = (ExportDeclaration) childNode;
                    export.GetExportEntries(_exportEntries, _requestedModules);
                }

                if (!childNode.ChildNodes.IsEmpty())
                {
                    Visit(childNode);
                }
            }
        }
    }
}
