using System.Runtime.InteropServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
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
        var astAnalyzer = new AstAnalyzer();
        var options = ParserOptions.Default with
        {
            AllowReturnOutsideFunction = true,
            OnNodeCreated = astAnalyzer.NodeVisitor
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
        var astAnalyzer = new AstAnalyzer();
        var options = ParserOptions.Default with { OnNodeCreated = astAnalyzer.NodeVisitor };

        return new JavaScriptParser(options).ParseModule(script, source);
    }

    private sealed class AstAnalyzer
    {
        private readonly Dictionary<string, Environment.BindingName> _bindingNames = new(StringComparer.Ordinal);

        public void NodeVisitor(Node node)
        {
            switch (node.Type)
            {
                case Nodes.Identifier:
                    {
                        var name = ((Identifier) node).Name;

                        if (!_bindingNames.TryGetValue(name, out var bindingName))
                        {
                            _bindingNames[name] = bindingName = new Environment.BindingName(JsString.CachedCreate(name));
                        }

                        node.AssociatedData = bindingName;
                        break;
                    }
                case Nodes.Literal:
                    node.AssociatedData = JintLiteralExpression.ConvertToJsValue((Literal) node);
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
