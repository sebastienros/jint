using System.Collections.Generic;
using Esprima.Ast;

namespace Jint
{
    internal readonly struct HoistingScope
    {
        internal readonly List<FunctionDeclaration> _functionDeclarations;
        internal readonly List<VariableDeclaration> _variablesDeclarations;
        internal readonly List<VariableDeclaration> _lexicalDeclarations;

        private HoistingScope(
            List<FunctionDeclaration> functionDeclarations, 
            List<VariableDeclaration> variables,
            List<VariableDeclaration> lexicalDeclarations)
        {
            _functionDeclarations = functionDeclarations;
            _variablesDeclarations = variables;
            _lexicalDeclarations = lexicalDeclarations;
        }

        internal enum HoistingMode
        {
            Script = 1,
            Block = 2,
        }

        public static HoistingScope Hoist(INode node, HoistingMode mode)
        {
            var treeWalker = new AstWalker(mode);
            treeWalker.Visit(node);
            return new HoistingScope(treeWalker._functions, treeWalker._variableDeclarations, treeWalker._lexicalDeclarations);
        }

        private sealed class AstWalker
        {
            private readonly HoistingMode _mode;
            internal List<VariableDeclaration> _variableDeclarations;
            internal List<VariableDeclaration> _lexicalDeclarations;
            internal List<FunctionDeclaration> _functions;

            public AstWalker(HoistingMode mode)
            {
                _mode = mode;
            }

            public void Visit(INode node)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    if (childNode is null)
                    {
                        // array expression can push null nodes in Esprima
                        continue;
                    }
                    
                    if (childNode is VariableDeclaration variableDeclaration)
                    {
                        if (_mode == HoistingMode.Script && variableDeclaration.Kind == VariableDeclarationKind.Var)
                        {
                            _variableDeclarations ??= new List<VariableDeclaration>();
                            _variableDeclarations.Add(variableDeclaration);
                        }
                        if (variableDeclaration.Kind != VariableDeclarationKind.Var)
                        {
                            _lexicalDeclarations ??= new List<VariableDeclaration>();
                            _lexicalDeclarations.Add(variableDeclaration);
                        }
                    }     
                    else if (_mode == HoistingMode.Script && childNode is FunctionDeclaration functionDeclaration)
                    {
                        _functions ??= new List<FunctionDeclaration>();
                        _functions.Add(functionDeclaration);
                    }

                    if ((_mode & HoistingMode.Script) != 0 
                        && childNode.Type != Nodes.FunctionDeclaration
                        && childNode.Type != Nodes.ArrowFunctionExpression
                        && childNode.Type != Nodes.ArrowParameterPlaceHolder
                        && childNode.Type != Nodes.FunctionExpression)
                    {
                        Visit(childNode);
                    }
                }
            }
        }
    }
}