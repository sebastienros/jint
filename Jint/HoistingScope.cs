using Jint.Runtime.Modules;

namespace Jint;

internal sealed class HoistingScope
{
    internal readonly List<FunctionDeclaration>? _functionDeclarations;

    internal readonly List<VariableDeclaration>? _variablesDeclarations;
    internal readonly List<Key>? _varNames;

    internal readonly List<Declaration>? _lexicalDeclarations;
    internal readonly List<string>? _lexicalNames;

    /// <summary>
    /// B.3.2/B.3.3: Block-level function declarations that need AnnexB var hoisting in sloppy mode.
    /// </summary>
    internal readonly List<FunctionDeclaration>? _annexBFunctionDeclarations;

    private HoistingScope(
        List<FunctionDeclaration>? functionDeclarations,
        List<Key>? varNames,
        List<VariableDeclaration>? variableDeclarations,
        List<Declaration>? lexicalDeclarations,
        List<string>? lexicalNames,
        List<FunctionDeclaration>? annexBFunctionDeclarations = null)
    {
        _functionDeclarations = functionDeclarations;
        _varNames = varNames;
        _variablesDeclarations = variableDeclarations;
        _lexicalDeclarations = lexicalDeclarations;
        _lexicalNames = lexicalNames;
        _annexBFunctionDeclarations = annexBFunctionDeclarations;
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
            treeWalker._lexicalNames,
            treeWalker._annexBFunctions);
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
            treeWalker._lexicalNames,
            strict ? null : treeWalker._annexBFunctions);
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
        localExportEntries = [];
        indirectExportEntries = [];
        starExportEntries = [];

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
                                    // Per ECMAScript 16.2.1.7.1 step 10.b.ii:
                                    // This is a re-export of an imported module namespace object.
                                    // Create an indirect export entry with ImportName: all ("*")
                                    indirectExportEntries.Add(new(ee.ExportName, ie.ModuleRequest, "*", null));
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

        /// <summary>
        /// B.3.2/B.3.3: Function declarations inside blocks/switch cases in sloppy mode.
        /// </summary>
        internal List<FunctionDeclaration>? _annexBFunctions;

        private int _depth;
        private const int MaxDepth = 256;

        public ScriptWalker(bool collectVarNames, bool collectLexicalNames)
        {
            _collectVarNames = collectVarNames;
            _collectLexicalNames = collectLexicalNames;
        }

        public void Visit(Node node, Node? parent, HashSet<string>? enclosingLexicalNames = null)
        {
            if (++_depth > MaxDepth)
            {
                _depth--;
                throw new ScriptPreparationException($"Script nesting exceeds maximum depth of {MaxDepth} levels.", null);
            }
            try
            {
                VisitCore(node, parent, enclosingLexicalNames);
            }
            finally
            {
                _depth--;
            }
        }

        private void VisitCore(Node node, Node? parent, HashSet<string>? enclosingLexicalNames)
        {
            // Collect lexical names from this scope level for AnnexB conflict checking.
            // These are let/const/class declarations in non-root scopes (blocks, for headers, etc.)
            // that would prevent a var hoisting from the function declaration's position.
            HashSet<string>? currentScopeLexicalNames = null;
            if (parent is not null)
            {
                foreach (var child in node.ChildNodes)
                {
                    if (child is VariableDeclaration { Kind: not VariableDeclarationKind.Var } lexDecl)
                    {
                        CollectBoundNames(lexDecl, ref currentScopeLexicalNames, enclosingLexicalNames);
                    }
                    else if (child is ClassDeclaration classDecl && classDecl.Id is not null)
                    {
                        EnsureSet(ref currentScopeLexicalNames, enclosingLexicalNames);
                        currentScopeLexicalNames!.Add(classDecl.Id.Name);
                    }
                }

                // For for/for-in/for-of statements, also collect lexical names from the header
                if (node is ForStatement { Init: VariableDeclaration { Kind: not VariableDeclarationKind.Var } forInit })
                {
                    CollectBoundNames(forInit, ref currentScopeLexicalNames, enclosingLexicalNames);
                }
                else if (node is ForInStatement { Left: VariableDeclaration { Kind: not VariableDeclarationKind.Var } forInLeft })
                {
                    CollectBoundNames(forInLeft, ref currentScopeLexicalNames, enclosingLexicalNames);
                }
                else if (node is ForOfStatement { Left: VariableDeclaration { Kind: not VariableDeclarationKind.Var } forOfLeft })
                {
                    CollectBoundNames(forOfLeft, ref currentScopeLexicalNames, enclosingLexicalNames);
                }
                else if (node is CatchClause catchClause && catchClause.Param is not null and not Identifier)
                {
                    // Per B.3.5: Only destructured catch parameters (not simple BindingIdentifier)
                    // prevent AnnexB var hoisting of the same name.
                    // Simple catch(e) allows AnnexB hoisting; catch({e}) or catch([e]) blocks it.
                    var boundNames = new List<Key>();
                    catchClause.Param.GetBoundNames(boundNames);
                    foreach (var bn in boundNames)
                    {
                        currentScopeLexicalNames ??= enclosingLexicalNames != null ? new HashSet<string>(enclosingLexicalNames, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
                        currentScopeLexicalNames.Add(bn.Name);
                    }
                }
            }

            var effectiveLexicalNames = currentScopeLexicalNames ?? enclosingLexicalNames;

            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;
                if (childType == NodeType.VariableDeclaration)
                {
                    var variableDeclaration = (VariableDeclaration) childNode;
                    if (variableDeclaration.Kind == VariableDeclarationKind.Var)
                    {
                        _variableDeclarations ??= [];
                        _variableDeclarations.Add(variableDeclaration);
                        if (_collectVarNames)
                        {
                            _varNames ??= [];
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
                        _lexicalDeclarations ??= [];
                        _lexicalDeclarations.Add(variableDeclaration);
                        if (_collectLexicalNames)
                        {
                            _lexicalNames ??= [];
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
                    // Function declarations are regular hoisted functions unless they appear in
                    // block-level positions (blocks, switch cases, if statement clauses).
                    // Per B.3.2/B.3.3, block-level function declarations in sloppy mode get
                    // special AnnexB hoisting behavior.
                    if (parent is null || node.Type is not (NodeType.BlockStatement or NodeType.SwitchCase or NodeType.IfStatement))
                    {
                        _functions ??= [];
                        _functions.Add((FunctionDeclaration) childNode);
                    }
                    else
                    {
                        // B.3.2/B.3.3: Collect block-level function declarations for AnnexB hoisting
                        // Only regular function declarations get AnnexB treatment (not generators, async, or async generators)
                        // But skip if there's a conflicting lexical name in any enclosing scope
                        var funcDecl = (FunctionDeclaration) childNode;
                        if (!funcDecl.Generator && !funcDecl.Async)
                        {
                            var fnName = funcDecl.Id!.Name;
                            if (effectiveLexicalNames?.Contains(fnName) != true)
                            {
                                _annexBFunctions ??= [];
                                _annexBFunctions.Add(funcDecl);

                                // Track this function name as a lexical binding so inner scopes
                                // can't AnnexB-hoist a same-named function (replacing with var
                                // would conflict with this block-level lexical binding).
                                if (effectiveLexicalNames is null)
                                {
                                    effectiveLexicalNames = new HashSet<string>(StringComparer.Ordinal);
                                }
                                else if (ReferenceEquals(effectiveLexicalNames, enclosingLexicalNames))
                                {
                                    effectiveLexicalNames = new HashSet<string>(enclosingLexicalNames, StringComparer.Ordinal);
                                }
                                effectiveLexicalNames.Add(fnName);
                            }
                        }
                    }
                }
                else if (childType == NodeType.ClassDeclaration && parent is null or AstModule)
                {
                    _lexicalDeclarations ??= [];
                    _lexicalDeclarations.Add((Declaration) childNode);
                }

                if (childType != NodeType.FunctionDeclaration
                    && childType != NodeType.ArrowFunctionExpression
                    && childType != NodeType.FunctionExpression
                    && !childNode.ChildNodes.IsEmpty())
                {
                    Visit(childNode, node, effectiveLexicalNames);
                }
            }
        }

        private static void EnsureSet(ref HashSet<string>? set, HashSet<string>? source)
        {
            set ??= source != null ? new HashSet<string>(source, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        }

        private static void CollectBoundNames(VariableDeclaration decl, ref HashSet<string>? set, HashSet<string>? source)
        {
            ref readonly var decls = ref decl.Declarations;
            foreach (var d in decls)
            {
                if (d.Id is Identifier id)
                {
                    EnsureSet(ref set, source);
                    set!.Add(id.Name);
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

    /// <summary>
    /// Checks if the module has top-level await expressions.
    /// Only checks at the module level, not inside function bodies.
    /// </summary>
    public static bool HasTopLevelAwait(AstModule module)
    {
        return HasTopLevelAwaitVisitor.Check(module);
    }

    private sealed class HasTopLevelAwaitVisitor
    {
        private bool _hasTopLevelAwait;

        public static bool Check(AstModule module)
        {
            var visitor = new HasTopLevelAwaitVisitor();
            visitor.Visit(module);
            return visitor._hasTopLevelAwait;
        }

        private void Visit(Node node)
        {
            if (_hasTopLevelAwait)
            {
                return;
            }

            // Found a top-level await expression
            if (node.Type == NodeType.AwaitExpression)
            {
                _hasTopLevelAwait = true;
                return;
            }

            // Found a top-level for-await-of statement
            if (node is ForOfStatement { Await: true })
            {
                _hasTopLevelAwait = true;
                return;
            }

            // Don't descend into function bodies - those have their own async context
            if (node.Type is NodeType.FunctionDeclaration or NodeType.FunctionExpression
                or NodeType.ArrowFunctionExpression or NodeType.ClassDeclaration
                or NodeType.ClassExpression)
            {
                return;
            }

            foreach (var childNode in node.ChildNodes)
            {
                Visit(childNode);
                if (_hasTopLevelAwait)
                {
                    return;
                }
            }
        }
    }
}
