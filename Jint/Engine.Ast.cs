using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Interpreter.Statements;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// Prepares a script for the engine that includes static analysis data to speed up execution during run-time.
    /// </summary>
    /// <remarks>
    /// Returned instance is reusable and thread-safe. You should prepare scripts only once and then reuse them.
    /// <para>
    /// Setting <see cref="ScriptPreparationOptions.StaticAnalysis"/> to <see langword="false"/> skips the analysis
    /// and leaves preparation at roughly the cost of a parse; the result stays reusable and thread-safe, and every
    /// engine running it rebuilds lazily what the analysis would have precomputed.
    /// </para>
    /// </remarks>
    public static Prepared<Script> PrepareScript(string code, string? source = null, bool strict = false, ScriptPreparationOptions? options = null)
    {
        source ??= "<anonymous>";
        options ??= ScriptPreparationOptions.Default;

        var sourceOffset = options.ParsingOptions.SourceOffset;
        var padding = AcornimaExtensions.CreateSourceOffsetPadding(sourceOffset);
        var paddedCode = padding.Length > 0 ? padding + code : code;

        var referencedGlobals = options.CollectReferencedGlobals ? new ReferencedGlobalsCollector() : null;
        var parserOptions = options.GetParserOptions();
        var parser = CreatePreparationParser(options, options.StaticAnalysis, referencedGlobals, parserOptions);

        try
        {
            var preparedScript = parser.ParseScript(paddedCode, padding.Length, code.Length, source, strict);

            return new Prepared<Script>(preparedScript, parserOptions, referencedGlobals?.Build(preparedScript));
        }
        catch (Exception e)
        {
            throw new ScriptPreparationException("Could not prepare script: " + e.Message, e);
        }
    }

    /// <summary>
    /// Prepares a module for the engine that includes static analysis data to speed up execution during run-time.
    /// </summary>
    /// <remarks>
    /// Returned instance is reusable and thread-safe. You should prepare modules only once and then reuse them.
    /// <para>
    /// Setting <see cref="ModulePreparationOptions.StaticAnalysis"/> to <see langword="false"/> skips the analysis
    /// and leaves preparation at roughly the cost of a parse; the result stays reusable and thread-safe, and every
    /// engine running it rebuilds lazily what the analysis would have precomputed.
    /// </para>
    /// </remarks>
    public static Prepared<Module> PrepareModule(string code, string? source = null, ModulePreparationOptions? options = null)
    {
        source ??= "<anonymous>";
        options ??= ModulePreparationOptions.Default;

        var referencedGlobals = options.CollectReferencedGlobals ? new ReferencedGlobalsCollector() : null;
        var parserOptions = options.GetParserOptions();
        var parser = CreatePreparationParser(options, options.StaticAnalysis, referencedGlobals, parserOptions);

        try
        {
            var preparedModule = parser.ParseModule(code, source);

            return new Prepared<Module>(preparedModule, parserOptions, referencedGlobals?.Build(preparedModule));
        }
        catch (Exception e)
        {
            throw new ScriptPreparationException("Could not prepare script: " + e.Message, e);
        }
    }

    /// <summary>
    /// Builds the parser a preparation runs under, which is entirely a question of which node visitor — if any —
    /// gets installed on top of <paramref name="parserOptions"/>.
    /// <para>
    /// When the analysis runs it owns the visitor slot outright: it folds the referenced-globals collector into its
    /// own visitor and reads retained source text from the parse context itself, so nothing it displaces is lost.
    /// When it does not run, the two remaining reasons to visit a node have to be composed by hand, and the case
    /// that matters is the one where there is no reason at all: the parser options are then handed to the parser
    /// untouched, which is both one <see cref="ParserOptions"/> clone saved and what keeps the source-text
    /// retention handler that <c>ParsingOptions.ApplyTo</c> installed in effect.
    /// </para>
    /// </summary>
    private static Parser CreatePreparationParser(
        IPreparationOptions<IParsingOptions> options,
        bool staticAnalysis,
        ReferencedGlobalsCollector? referencedGlobals,
        ParserOptions parserOptions)
    {
        if (staticAnalysis)
        {
            return new Parser(parserOptions with { OnNode = new AstAnalyzer(options, referencedGlobals).GetNodeVisitor() });
        }

        if (referencedGlobals is null)
        {
            return new Parser(parserOptions);
        }

        // Collecting without analyzing. The collector never writes UserData, so ordering against the retention
        // handler is free, but displacing it is not: retained source text is the retention handler's only chance
        // to be recorded, and Function.prototype.toString() reads what it left behind.
        var retainSourceText = parserOptions.OnNode;
        OnNodeHandler onNode = retainSourceText is null
            ? referencedGlobals.OnNode
            : new CompositeNodeVisitor(retainSourceText, referencedGlobals.OnNode).Visit;

        return new Parser(parserOptions with { OnNode = onNode });
    }

    /// <summary>
    /// Runs two node handlers in turn. Only the parse-only path ever needs one: with the analysis on, the single
    /// visitor it installs already does everything that would otherwise be composed here.
    /// </summary>
    private sealed class CompositeNodeVisitor(OnNodeHandler first, OnNodeHandler second)
    {
        public void Visit(Node node, in OnNodeContext ctx)
        {
            first(node, in ctx);
            second(node, in ctx);
        }
    }

    private sealed class AstAnalyzer
    {
        private readonly IPreparationOptions<IParsingOptions> _preparationOptions;
        private readonly bool _retainSourceText;
        private readonly ReferencedGlobalsCollector? _referencedGlobals;
        private readonly Dictionary<string, Environment.BindingName> _bindingNames = new(StringComparer.Ordinal);

        public AstAnalyzer(IPreparationOptions<IParsingOptions> preparationOptions, ReferencedGlobalsCollector? referencedGlobals)
        {
            _preparationOptions = preparationOptions;
            _retainSourceText = preparationOptions.ParsingOptions.RetainFunctionSourceText;
            _referencedGlobals = referencedGlobals;
        }

        /// <summary>
        /// Picks the callback to install. Referenced-globals collection gets its own visitor rather than a flag
        /// tested inside the default one, so that not asking for it costs literally nothing per node.
        /// </summary>
        public OnNodeHandler GetNodeVisitor() => _referencedGlobals is null ? NodeVisitor : CollectingNodeVisitor;

        private void CollectingNodeVisitor(Node node, in OnNodeContext ctx)
        {
            _referencedGlobals!.OnNode(node, in ctx);
            NodeVisitor(node, in ctx);
        }

        private void NodeVisitor(Node node, in OnNodeContext ctx)
        {
            switch (node.Type)
            {
                case NodeType.Identifier:
                    var identifier = (Identifier) node;
                    var name = identifier.Name;

                    if (!_bindingNames.TryGetValue(name, out var bindingName))
                    {
                        // Build the binding name from the parser's own (deduplicated) string
                        // instance: environment binding storage is keyed by Keys created from
                        // the same parse, so Key comparisons stay on the reference-equality
                        // fast path of string.Equals. Routing through a process-wide JsString
                        // cache here would substitute string instances from unrelated parses
                        // and force every lookup onto the memcmp path.
                        _bindingNames[name] = bindingName = new Environment.BindingName(name);
                    }

                    node.UserData = bindingName;
                    break;

                case NodeType.Literal:
                    var literal = (Literal) node;

                    var constantValue = JintLiteralExpression.ConvertToJsValue(literal);
                    node.UserData = constantValue is not null ? new JintConstantExpression(literal, constantValue) : null;
                    break;

                case NodeType.MemberExpression:
                    node.UserData = JintMemberExpression.InitializeDeterminedProperty((MemberExpression) node, cache: true);
                    break;

                case NodeType.ArrowFunctionExpression:
                case NodeType.FunctionDeclaration:
                case NodeType.FunctionExpression:
                    node.UserData = JintFunctionDefinition.BuildState((IFunction) node, _retainSourceText ? ctx.Input : null);
                    break;

                case NodeType.ClassDeclaration:
                case NodeType.ClassExpression:
                    // See Engine.DefaultNodeHandler: a class constructor's source text is the whole class
                    // production, and a class with no explicit constructor has no function node carrying it.
                    if (_retainSourceText)
                    {
                        node.UserData = ctx.Input;
                    }
                    break;

                case NodeType.Program:
                    // A module root is a Program by node type, but nothing reads a module's cached scope —
                    // every reader of it is Script-typed and SourceTextModule walks its own declarations —
                    // so caching one for a module was a second full walk of the AST that never got consumed.
                    if (node is Script script)
                    {
                        node.UserData = new CachedHoistingScope(script);
                    }
                    break;

                case NodeType.UnaryExpression:
                    node.UserData = JintUnaryExpression.BuildConstantExpression((NonUpdateUnaryExpression) node);
                    break;

                case NodeType.BinaryExpression:
                    var binaryExpression = (NonLogicalBinaryExpression) node;
                    if (_preparationOptions.FoldConstants
                        && binaryExpression.Operator != Operator.InstanceOf
                        && binaryExpression.Operator != Operator.In
                        && binaryExpression is { Left: Literal leftLiteral, Right: Literal rightLiteral })
                    {
                        var left = JintLiteralExpression.ConvertToJsValue(leftLiteral);
                        var right = JintLiteralExpression.ConvertToJsValue(rightLiteral);

                        if (left is not null && right is not null)
                        {
                            // we have fixed result
                            try
                            {
                                var result = JintBinaryExpression.Build(binaryExpression);
                                var context = new EvaluationContext();
                                node.UserData = new JintConstantExpression(binaryExpression, (JsValue) result.EvaluateWithoutNodeTracking(context));
                            }
                            catch
                            {
                                // probably caused an error and error reporting doesn't work without engine
                            }
                        }
                    }

                    break;

                case NodeType.ReturnStatement:
                    var returnStatement = (ReturnStatement) node;
                    if (returnStatement.Argument is Literal returnedLiteral)
                    {
                        var returnValue = JintLiteralExpression.ConvertToJsValue(returnedLiteral);
                        if (returnValue is not null)
                        {
                            node.UserData = new ConstantStatement(returnStatement, CompletionType.Return, returnValue);
                        }
                    }
                    break;

                case NodeType.BlockStatement:
                    // A function body is a BlockStatement by node type, but nothing reads a function body's
                    // BlockState — the statement list wraps it without consulting UserData, and the only
                    // reader, JintBlockStatement, takes a NestedBlockStatement — so building one per function
                    // bought nothing. A class static block reports NodeType.StaticBlock and never lands here.
                    if (node is NestedBlockStatement nestedBlockStatement)
                    {
                        node.UserData = JintBlockStatement.BuildState(nestedBlockStatement);
                    }
                    break;
            }
        }
    }
}

internal sealed class CachedHoistingScope
{
    // Script, not Program: a module's cached scope has no reader, so the analyzer only builds
    // one for a script root, and the parameter type is what keeps it that way.
    public CachedHoistingScope(Script script)
    {
        // Collecting during the walk makes the walker produce the bound-name list the
        // separate GatherVarNames re-walk used to build; null means the program has no vars.
        Scope = HoistingScope.GetProgramLevelDeclarations(script, collectVarNames: true);
        VarNames = Scope._varNames ?? [];

        LexNames = DeclarationCacheBuilder.Build(Scope._lexicalDeclarations);
    }

    internal static void GatherVarNames(HoistingScope scope, List<Key> boundNames)
    {
        var varDeclarations = scope._variablesDeclarations;
        if (varDeclarations != null)
        {
            for (var i = 0; i < varDeclarations.Count; i++)
            {
                var d = varDeclarations[i];
                d.GetBoundNames(boundNames);
            }
        }
    }

    public HoistingScope Scope { get; }
    public List<Key> VarNames { get; }
    public DeclarationCache LexNames { get; }
}

internal static class AstPreparationExtensions
{
    internal static HoistingScope GetHoistingScope(this Program program)
    {
        return program.UserData is CachedHoistingScope cached ? cached.Scope : HoistingScope.GetProgramLevelDeclarations(program);
    }

    internal static List<Key> GetVarNames(this Program program, HoistingScope hoistingScope)
    {
        // A scope whose walk already collected the names answers without another pass —
        // the eval cache reuses one HoistingScope across calls, so this also removes a
        // per-call list allocation there. Callers must treat the result as read-only.
        if (hoistingScope._varNames is not null)
        {
            return hoistingScope._varNames;
        }

        List<Key> boundNames;
        if (program.UserData is CachedHoistingScope cached)
        {
            boundNames = cached.VarNames;
        }
        else
        {
            boundNames = [];
            CachedHoistingScope.GatherVarNames(hoistingScope, boundNames);
        }

        return boundNames;
    }

    internal static List<ScopedDeclaration> GetLexNames(this Program program, HoistingScope hoistingScope)
    {
        DeclarationCache cache;
        if (program.UserData is CachedHoistingScope cached)
        {
            cache = cached.LexNames;
        }
        else
        {
            cache = DeclarationCacheBuilder.Build(hoistingScope._lexicalDeclarations);
        }

        return cache.Declarations;
    }
}
