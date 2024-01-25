using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Esprima;
using Esprima.Ast;
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
    public static Script PrepareScript(string script, string? source = null, bool strict = false)
    {
        var astAnalyzer = new AstAnalyzer(new ScriptPreparationOptions());
        var options = ParserOptions.Default with
        {
            AllowReturnOutsideFunction = true, OnNodeCreated = astAnalyzer.NodeVisitor
        };

        return new JavaScriptParser(options).ParseScript(script, source, strict);
    }

    /// <summary>
    /// Prepares a module for the engine that includes static analysis data to speed up execution during run-time.
    /// </summary>
    /// <remarks>
    /// Returned instance is reusable and thread-safe. You should prepare modules only once and then reuse them.
    /// </remarks>
    public static Module PrepareModule(string script, string? source = null)
    {
        var astAnalyzer = new AstAnalyzer(new ScriptPreparationOptions());
        var options = ParserOptions.Default with
        {
            OnNodeCreated = astAnalyzer.NodeVisitor
        };

        return new JavaScriptParser(options).ParseModule(script, source);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ScriptPreparationOptions(bool CompileRegex, bool FoldConstants)
    {
        public ScriptPreparationOptions() : this(CompileRegex: true, FoldConstants: true)
        {
        }
    }

    private sealed class AstAnalyzer
    {
        private readonly ScriptPreparationOptions _options;
        private readonly Dictionary<string, Environment.BindingName> _bindingNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Regex> _regexes = new(StringComparer.Ordinal);

        public AstAnalyzer(ScriptPreparationOptions options)
        {
            _options = options;
        }

        public void NodeVisitor(Node node)
        {
            switch (node.Type)
            {
                case Nodes.Identifier:
                    var identifier = (Identifier) node;
                    var name = identifier.Name;

                    if (!_bindingNames.TryGetValue(name, out var bindingName))
                    {
                        _bindingNames[name] = bindingName = new Environment.BindingName(JsString.CachedCreate(name));
                    }

                    node.AssociatedData = new JintIdentifierExpression(identifier, bindingName);
                    break;

                case Nodes.Literal:
                    var literal = (Literal) node;

                    var constantValue = JintLiteralExpression.ConvertToJsValue(literal);
                    node.AssociatedData = constantValue is not null ? new JintConstantExpression(literal, constantValue) : null;

                    if (node.AssociatedData is null && literal.TokenType == TokenType.RegularExpression && _options.CompileRegex)
                    {
                        var regExpLiteral = (RegExpLiteral) literal;
                        var regExpParseResult = regExpLiteral.ParseResult;

                        // only compile if there's no negative lookahead, it works incorrectly under NET 7 and NET 8
                        // https://github.com/dotnet/runtime/issues/97455
                        if (regExpParseResult.Success && !regExpLiteral.Raw.Contains("(?!"))
                        {
                            if (!_regexes.TryGetValue(regExpLiteral.Raw, out var regex))
                            {
                                regex = regExpParseResult.Regex!;
                                if ((regex.Options & RegexOptions.Compiled) == RegexOptions.None)
                                {
                                    regex = new Regex(regex.ToString(), regex.Options | RegexOptions.Compiled, regex.MatchTimeout);
                                }

                                _regexes[regExpLiteral.Raw] = regex;
                            }

                            regExpLiteral.AssociatedData = regex;
                        }
                    }

                    break;

                case Nodes.MemberExpression:
                    node.AssociatedData = JintMemberExpression.InitializeDeterminedProperty((MemberExpression) node, cache: true);
                    break;

                case Nodes.ArrowFunctionExpression:
                case Nodes.FunctionDeclaration:
                case Nodes.FunctionExpression:
                    var function = (IFunction) node;
                    node.AssociatedData = JintFunctionDefinition.BuildState(function);
                    break;

                case Nodes.Program:
                    node.AssociatedData = new CachedHoistingScope((Program) node);
                    break;

                case Nodes.UnaryExpression:
                    node.AssociatedData = JintUnaryExpression.BuildConstantExpression((UnaryExpression) node);
                    break;

                case Nodes.BinaryExpression:
                    var binaryExpression = (BinaryExpression) node;
                    if (_options.FoldConstants
                        && binaryExpression.Operator != BinaryOperator.InstanceOf
                        && binaryExpression.Operator != BinaryOperator.In
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
                                node.AssociatedData = new JintConstantExpression(binaryExpression, (JsValue) result.EvaluateWithoutNodeTracking(context));
                            }
                            catch
                            {
                                // probably caused an error and error reporting doesn't work without engine
                            }
                        }
                    }

                    break;

                case Nodes.ReturnStatement:
                    var returnStatement = (ReturnStatement) node;
                    if (returnStatement.Argument is Literal returnedLiteral)
                    {
                        var returnValue = JintLiteralExpression.ConvertToJsValue(returnedLiteral);
                        if (returnValue is not null)
                        {
                            node.AssociatedData = new ConstantStatement(returnStatement, CompletionType.Return, returnValue);
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
        return program.AssociatedData is CachedHoistingScope cached ? cached.Scope : HoistingScope.GetProgramLevelDeclarations(program);
    }

    internal static List<Key> GetVarNames(this Program program, HoistingScope hoistingScope)
    {
        List<Key> boundNames;
        if (program.AssociatedData is CachedHoistingScope cached)
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
        if (program.AssociatedData is CachedHoistingScope cached)
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
