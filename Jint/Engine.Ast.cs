using Esprima;
using Esprima.Ast;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

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
        private readonly Dictionary<string, EnvironmentRecord.BindingName> _bindingNames = new();

        public void NodeVisitor(Node node)
        {
            switch (node.Type)
            {
                case Nodes.Identifier:
                    {
                        var name = ((Identifier) node).Name;

                        if (!_bindingNames.TryGetValue(name, out var bindingName))
                        {
                            _bindingNames[name] = bindingName = new EnvironmentRecord.BindingName(name);
                        }

                        node.AssociatedData = bindingName;
                        break;
                    }
                case Nodes.Literal:
                    node.AssociatedData = JintLiteralExpression.ConvertToJsValue((Literal) node);
                    break;
                case Nodes.ArrowFunctionExpression:
                case Nodes.FunctionDeclaration:
                case Nodes.FunctionExpression:
                    var function = (IFunction) node;
                    node.AssociatedData = JintFunctionDefinition.BuildState(function);
                    break;
            }
        }
    }
}
