using System.Collections.Generic;
using Esprima.Ast;

namespace Jint
{
    internal sealed class HoistingScope
    {
        internal readonly List<FunctionDeclaration> _functionDeclarations;
        internal readonly List<VariableDeclaration> _variablesDeclarations;

        private HoistingScope(List<FunctionDeclaration> functionDeclarations, List<VariableDeclaration> variables)
        {
            _variablesDeclarations = variables;
            _functionDeclarations = functionDeclarations;
        }

        public static HoistingScope HoistFunctionScope(INode node)
        {
            var treeWalker = new FunctionScopeAstWalker();
            treeWalker.Visit(node);
            return new HoistingScope(treeWalker._functions, treeWalker._variables);
        }

        public static HoistingScope HoistBlockScope(BlockStatement statement)
        {
            var treeWalker = new BlockWalker();
            treeWalker.Visit(statement);
            return new HoistingScope(null, treeWalker._variables);
        }

        private sealed class FunctionScopeAstWalker
        {
            internal List<VariableDeclaration> _variables;
            internal List<FunctionDeclaration> _functions;

            public void Visit(INode node)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    if (childNode is null)
                    {
                        // array expression can push null nodes in Esprima
                        continue;
                    }
                    
                    if (childNode is VariableDeclaration variableDeclaration
                        && variableDeclaration.Kind == VariableDeclarationKind.Var)
                    {
                        _variables ??= new List<VariableDeclaration>();
                        _variables.Add(variableDeclaration);
                    }     
                    else if (childNode is FunctionDeclaration functionDeclaration)
                    {
                        _functions ??= new List<FunctionDeclaration>();
                        _functions.Add(functionDeclaration);
                    }

                    if (childNode.Type != Nodes.FunctionDeclaration
                        && childNode.Type != Nodes.ArrowFunctionExpression
                        && childNode.Type != Nodes.ArrowParameterPlaceHolder
                        && childNode.Type != Nodes.FunctionExpression)
                    {
                        Visit(childNode);
                    }
                }
            }
        }

        private sealed class BlockWalker
        {
            internal List<VariableDeclaration> _variables;

            public void Visit(INode node)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    if (childNode is null)
                    {
                        // array expression can push null nodes in Esprima
                        continue;
                    }
                    
                    if (childNode is VariableDeclaration variableDeclaration
                        && variableDeclaration.Kind != VariableDeclarationKind.Var)
                    {
                        _variables ??= new List<VariableDeclaration>();
                        _variables.Add(variableDeclaration);
                    }     
                }
            }
        }
    }
}