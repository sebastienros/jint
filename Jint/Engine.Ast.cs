using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
    /// </remarks>
    public static Prepared<Script> PrepareScript(string code, string? source = null, bool strict = false, ScriptPreparationOptions? options = null)
    {
        options ??= ScriptPreparationOptions.Default;
        var astAnalyzer = new AstAnalyzer(options);
        var parserOptions = options.GetParserOptions();
        var preparedScript = new Parser(parserOptions with { OnNode = astAnalyzer.NodeVisitor }).ParseScript(code, source, strict);
        return new Prepared<Script>(preparedScript, parserOptions);
    }

    /// <summary>
    /// Prepares a module for the engine that includes static analysis data to speed up execution during run-time.
    /// </summary>
    /// <remarks>
    /// Returned instance is reusable and thread-safe. You should prepare modules only once and then reuse them.
    /// </remarks>
    public static Prepared<Module> PrepareModule(string code, string? source = null, ModulePreparationOptions? options = null)
    {
        options ??= ModulePreparationOptions.Default;
        var astAnalyzer = new AstAnalyzer(options);
        var parserOptions = options.GetParserOptions();
        var preparedModule = new Parser(parserOptions with { OnNode = astAnalyzer.NodeVisitor }).ParseModule(code, source);
        return new Prepared<Module>(preparedModule, parserOptions);
    }

    private sealed class AstAnalyzer
    {
        private static readonly bool _canCompileNegativeLookaroundAssertions = typeof(Regex).Assembly.GetName().Version?.Major is not (null or 7 or 8);

        private readonly IPreparationOptions<IParsingOptions> _preparationOptions;
        private readonly Dictionary<string, Environment.BindingName> _bindingNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Regex> _regexes = new(StringComparer.Ordinal);

        public AstAnalyzer(IPreparationOptions<IParsingOptions> preparationOptions)
        {
            _preparationOptions = preparationOptions;
        }

        public void NodeVisitor(Node node, OnNodeContext _)
        {
            switch (node.Type)
            {
                case NodeType.Identifier:
                    var identifier = (Identifier) node;
                    var name = identifier.Name;

                    if (!_bindingNames.TryGetValue(name, out var bindingName))
                    {
                        _bindingNames[name] = bindingName = new Environment.BindingName(JsString.CachedCreate(name));
                    }

                    node.UserData = new JintIdentifierExpression(identifier, bindingName);
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
                    var function = (IFunction) node;
                    node.UserData = JintFunctionDefinition.BuildState(function);
                    break;

                case NodeType.Program:
                    node.UserData = new CachedHoistingScope((Program) node);
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
            }
        }
    }
}

internal sealed class CachedHoistingScope
{
    public CachedHoistingScope(Program program)
    {
        Scope = HoistingScope.GetProgramLevelDeclarations(program);

        VarNames = new List<Key>();
        GatherVarNames(Scope, VarNames);

        LexNames = new List<CachedLexicalName>();
        GatherLexNames(Scope, LexNames);
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

    internal static void GatherLexNames(HoistingScope scope, List<CachedLexicalName> boundNames)
    {
        var lexDeclarations = scope._lexicalDeclarations;
        if (lexDeclarations != null)
        {
            var temp = new List<Key>();
            for (var i = 0; i < lexDeclarations.Count; i++)
            {
                var d = lexDeclarations[i];
                temp.Clear();
                d.GetBoundNames(temp);
                for (var j = 0; j < temp.Count; j++)
                {
                    boundNames.Add(new CachedLexicalName(temp[j], d.IsConstantDeclaration()));
                }
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct CachedLexicalName(Key Name, bool Constant);

    public HoistingScope Scope { get; }
    public List<Key> VarNames { get; }
    public List<CachedLexicalName> LexNames { get; }
}

internal static class AstPreparationExtensions
{
    internal static HoistingScope GetHoistingScope(this Program program)
    {
        return program.UserData is CachedHoistingScope cached ? cached.Scope : HoistingScope.GetProgramLevelDeclarations(program);
    }

    internal static List<Key> GetVarNames(this Program program, HoistingScope hoistingScope)
    {
        List<Key> boundNames;
        if (program.UserData is CachedHoistingScope cached)
        {
            boundNames = cached.VarNames;
        }
        else
        {
            boundNames = new List<Key>();
            CachedHoistingScope.GatherVarNames(hoistingScope, boundNames);
        }

        return boundNames;
    }

    internal static List<CachedHoistingScope.CachedLexicalName> GetLexNames(this Program program, HoistingScope hoistingScope)
    {
        List<CachedHoistingScope.CachedLexicalName> boundNames;
        if (program.UserData is CachedHoistingScope cached)
        {
            boundNames = cached.LexNames;
        }
        else
        {
            boundNames = new List<CachedHoistingScope.CachedLexicalName>();
            CachedHoistingScope.GatherLexNames(hoistingScope, boundNames);
        }

        return boundNames;
    }
}
