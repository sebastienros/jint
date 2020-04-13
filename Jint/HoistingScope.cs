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
            List<VariableDeclaration> variableDeclarations,
            List<VariableDeclaration> lexicalDeclarations)
        {
            _functionDeclarations = functionDeclarations;
            _variablesDeclarations = variableDeclarations;
            _lexicalDeclarations = lexicalDeclarations;
        }

        public static HoistingScope GetFunctionLevelDeclarations(INode node)
        {
            var treeWalker = new ScriptWalker();
            treeWalker.Visit(node, true);
            return new HoistingScope(
                treeWalker._functions,
                treeWalker._variableDeclarations,
                treeWalker._lexicalDeclarations);
        }

        public static HoistingScope GetLexicalDeclarations(INode node)
        {
            var treeWalker = new LexicalWalker();
            treeWalker.Visit(node);
            return new HoistingScope(functionDeclarations: null, variableDeclarations: null, treeWalker._lexicalDeclarations);
        }

        public static HoistingScope GetLexicalDeclarations(BlockStatement statement)
        {
            List<VariableDeclaration> lexicalDeclarations = null ;
            foreach (var node in statement.Body)
            {
                if (node is VariableDeclaration rootVariable)
                {
                    if (rootVariable.Kind != VariableDeclarationKind.Var)
                    {
                        lexicalDeclarations = new List<VariableDeclaration>();
                        lexicalDeclarations.Add(rootVariable);
                    }
                }
            }
            
            return new HoistingScope(functionDeclarations: null, variableDeclarations: null, lexicalDeclarations);
        }

        private sealed class ScriptWalker
        {
            internal List<FunctionDeclaration> _functions;
            internal List<VariableDeclaration> _lexicalDeclarations;
            internal List<VariableDeclaration> _variableDeclarations;

            public void Visit(INode node, bool root)
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
                        if (variableDeclaration.Kind == VariableDeclarationKind.Var)
                        {
                            _variableDeclarations ??= new List<VariableDeclaration>();
                            _variableDeclarations.Add(variableDeclaration);
                        }

                        if (root && variableDeclaration.Kind != VariableDeclarationKind.Var)
                        {
                            _lexicalDeclarations ??= new List<VariableDeclaration>();
                            _lexicalDeclarations.Add(variableDeclaration);
                        }
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
                        Visit(childNode, false);
                    }
                }
            }
        }

        private sealed class LexicalWalker
        {
            internal List<VariableDeclaration> _lexicalDeclarations;

            public void Visit(INode node)
            {
                if (node is VariableDeclaration rootVariable)
                {
                    if (rootVariable.Kind != VariableDeclarationKind.Var)
                    {
                        _lexicalDeclarations ??= new List<VariableDeclaration>();
                        _lexicalDeclarations.Add(rootVariable);
                    }
                    return;
                }
                
                foreach (var childNode in node.ChildNodes)
                {
                    if (childNode is VariableDeclaration variableDeclaration)
                    {
                        if (variableDeclaration.Kind != VariableDeclarationKind.Var)
                        {
                            _lexicalDeclarations ??= new List<VariableDeclaration>();
                            _lexicalDeclarations.Add(variableDeclaration);
                        }
                    }
                }
            }
        }
    }
}