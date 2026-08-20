using Jint.Runtime.Modules;

namespace Jint;

internal sealed class HoistingScope
{
    internal readonly List<FunctionDeclaration>? _functionDeclarations;

    internal readonly List<VariableDeclaration>? _variablesDeclarations;
    internal readonly List<Key>? _varNames;

    internal readonly List<Declaration>? _lexicalDeclarations;
    internal readonly List<Key>? _lexicalNames;

    /// <summary>
    /// B.3.2/B.3.3: Block-level function declarations that need AnnexB var hoisting in sloppy mode.
    /// </summary>
    internal readonly List<FunctionDeclaration>? _annexBFunctionDeclarations;

    private HoistingScope(
        List<FunctionDeclaration>? functionDeclarations,
        List<Key>? varNames,
        List<VariableDeclaration>? variableDeclarations,
        List<Declaration>? lexicalDeclarations,
        List<Key>? lexicalNames,
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
        // A module root is a Program too, so the flag has to be derived from the node rather than defaulted -
        // otherwise the same AST answers differently depending on which factory was called, and the module's
        // own exported lexical declarations go missing from the scope this returns.
        var treeWalker = new ScriptWalker(collectVarNames, collectLexicalNames, module: script is AstModule);
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
        // A function body is never module scope, whatever encloses it: an export declaration cannot appear inside one.
        var treeWalker = new ScriptWalker(collectVarNames: true, collectLexicalNames: true, module: false);
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
        var treeWalker = new ScriptWalker(collectVarNames, collectLexicalNames, module: true);
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
                                if (ie.Phase == ModuleImportPhase.Source)
                                {
                                    // Re-export of a source-phase import (import source x from "..."; export { x };).
                                    // ParseModule reclassifies this as an indirect export whose ImportName is ~source~.
                                    indirectExportEntries.Add(new(ee.ExportName, ie.ModuleRequest, "*source*", null));
                                }
                                else if (ie.Phase == ModuleImportPhase.Defer)
                                {
                                    // Deferred namespace imports re-exported should be treated as local exports
                                    // so the deferred namespace object is returned from the environment binding
                                    localExportEntries.Add(ee);
                                }
                                else if (string.Equals(ie.ImportName, "*", StringComparison.Ordinal))
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
        internal List<Key>? _lexicalNames;

        /// <summary>
        /// Whether the walk started at an <see cref="AstModule"/>. Only there can an export declaration wrap a
        /// module-level lexical declaration, so a script or function body walk stops at the root test. It has no
        /// default: every entry point must state it, because getting it wrong silently drops declarations.
        /// </summary>
        private readonly bool _module;

        /// <summary>
        /// B.3.2/B.3.3: Function declarations inside blocks/switch cases in sloppy mode.
        /// </summary>
        internal List<FunctionDeclaration>? _annexBFunctions;

        private int _depth;
        private const int MaxDepth = 256;

        public ScriptWalker(bool collectVarNames, bool collectLexicalNames, bool module)
        {
            _collectVarNames = collectVarNames;
            _collectLexicalNames = collectLexicalNames;
            _module = module;
        }

        // labelHost / labelHostParent: when `node` is a LabelledStatement, the statement-list container
        // the label chain sits in and that container's own parent. A LabelledStatement is transparent
        // for the FunctionDeclaration classification below - TopLevelVarScopedDeclarations sees through
        // the label (so `l: function f(){}` at the top level of a script or function body hoists like
        // any other function declaration) while VarScopedDeclarations does not (so inside a Block it is
        // scoped to that Block).
        public void Visit(Node node, Node? parent, HashSet<string>? enclosingLexicalNames = null, Node? labelHost = null, Node? labelHostParent = null)
        {
            if (++_depth > MaxDepth)
            {
                _depth--;
                throw new ScriptPreparationException($"Script nesting exceeds maximum depth of {MaxDepth} levels.", null);
            }
            try
            {
                VisitCore(node, parent, enclosingLexicalNames, labelHost, labelHostParent);
            }
            finally
            {
                _depth--;
            }
        }

        private void VisitCore(Node node, Node? parent, HashSet<string>? enclosingLexicalNames, Node? labelHost = null, Node? labelHostParent = null)
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

            // https://tc39.es/ecma262/#sec-module-semantics-static-semantics-lexicallyscopeddeclarations
            // Only a direct child of the module, or an export wrapping one, is a module item; a top-level block or `for` head is not.
            // Outside a module the module-item question cannot arise, so the root test is the whole answer.
            var atTopLevel = parent is null
                || (_module && node.Type is NodeType.ExportNamedDeclaration or NodeType.ExportDefaultDeclaration && parent is AstModule);

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
                                declaration.Id.GetBoundNames(_varNames);
                            }
                        }
                    }

                    if (atTopLevel && variableDeclaration.Kind != VariableDeclarationKind.Var)
                    {
                        _lexicalDeclarations ??= [];
                        _lexicalDeclarations.Add(variableDeclaration);
                        if (_collectLexicalNames)
                        {
                            _lexicalNames ??= [];
                            ref readonly var nodeList = ref variableDeclaration.Declarations;
                            foreach (var declaration in nodeList)
                            {
                                declaration.Id.GetBoundNames(_lexicalNames);
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
                    // A LabelledStatement is transparent here (Annex B.3.1 legalizes the form and
                    // every web implementation gives it the same treatment as an unlabelled one):
                    // `l: function f(){}` hoists like any other declaration at the top level of a
                    // script or function body, and is a block-level declaration when it appears in a
                    // Block or CaseClause - see the labelHost parameter.
                    var host = labelHost ?? node;
                    var hostParent = labelHost is not null ? labelHostParent : parent;
                    if (hostParent is null || host.Type is not (NodeType.BlockStatement or NodeType.SwitchCase or NodeType.IfStatement))
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
                                // Skip for IfStatement branches - sibling branches (consequent/alternate)
                                // are not nested scopes and should not block each other.
                                if (node.Type is not NodeType.IfStatement)
                                {
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
                }
                else if (childType == NodeType.ClassDeclaration && atTopLevel)
                {
                    var classDeclaration = (ClassDeclaration) childNode;
                    _lexicalDeclarations ??= [];
                    _lexicalDeclarations.Add(classDeclaration);

                    // A class name is a lexically declared name of this scope just as a let/const one is, and
                    // B.3.2.1 skips the var-hoisting of a block-level function whose name is lexically declared.
                    // The Id is null for `export default class {}`, which binds *default* rather than a name.
                    if (_collectLexicalNames && classDeclaration.Id is not null)
                    {
                        _lexicalNames ??= [];
                        _lexicalNames.Add(classDeclaration.Id.Name);
                    }
                }

                // A class static block is its own var scope — ClassStaticBlockDefinitionEvaluation hoists it as a
                // function body of its own — so its `var` and `function` declarations must not reach this one.
                if (childType != NodeType.FunctionDeclaration
                    && childType != NodeType.ArrowFunctionExpression
                    && childType != NodeType.FunctionExpression
                    && childType != NodeType.StaticBlock
                    && !childNode.ChildNodes.IsEmpty())
                {
                    // Carry this statement list's container into a label chain so the classification
                    // above can see through it (labels nest, so an already-set host wins).
                    if (childType == NodeType.LabeledStatement)
                    {
                        Visit(childNode, node, effectiveLexicalNames, labelHost ?? node, labelHost is not null ? labelHostParent : parent);
                    }
                    else
                    {
                        Visit(childNode, node, effectiveLexicalNames);
                    }
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
