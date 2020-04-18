using System.Collections.Generic;
using System.Linq;
using Esprima.Ast;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    internal sealed class JintFunctionDefinition
    {
        internal readonly IFunction _function;
        internal readonly string _name;
        internal readonly bool _strict;
        internal string[] _parameterNames;
        internal readonly JintStatement _body;
        internal bool _hasRestParameter;
        internal int _length;

        public readonly HoistingScope _hoistingScope;
        internal bool _hasDuplicates;
        internal bool _isSimpleParameterList;
        internal bool _hasParameterExpressions;
        internal bool _hasArguments;

        public JintFunctionDefinition(Engine engine, IFunction function)
        {
            _function = function;
            _hoistingScope = HoistingScope.GetFunctionLevelDeclarations(function, collectVarNames: true, collectLexicalNames: true);
            _name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id.Name : null;
            _strict = function.Strict;
             ProcessParameters(function);

            Statement bodyStatement;
            if (function.Expression)
            {
                bodyStatement = new ReturnStatement((Expression) function.Body);
            }
            else
            {
                // Esprima doesn't detect strict at the moment for
                // language/expressions/object/method-definition/name-invoke-fn-strict.js
                var blockStatement = (BlockStatement) function.Body;
                for (int i = 0; i < blockStatement.Body.Count; ++i)
                {
                    if (blockStatement.Body[i] is Directive d && d.Directiv == "use strict")
                    {
                        _strict = true;
                    }
                }
                bodyStatement = blockStatement;
            }

            _body = JintStatement.Build(engine, bodyStatement);
        }

        private IEnumerable<Identifier> GetParameterIdentifiers(INode parameter)
        {
            if (parameter is Identifier identifier)
            {
                return new [] { identifier };
            }
            if (parameter is RestElement restElement)
            {
                _hasRestParameter = true;
                _hasParameterExpressions = true; 
                return GetParameterIdentifiers(restElement.Argument);
            }
            if (parameter is ArrayPattern arrayPattern)
            {
                _hasParameterExpressions = true; 
                return arrayPattern.Elements.SelectMany(GetParameterIdentifiers);
            }
            if (parameter is ObjectPattern objectPattern)
            {
                _hasParameterExpressions = true; 
                return objectPattern.Properties.SelectMany(property =>
                    property is Property p
                        ? GetParameterIdentifiers(p.Value)
                        : GetParameterIdentifiers((RestElement) property)
                );
            }
            if (parameter is AssignmentPattern assignmentPattern)
            {
                _hasParameterExpressions = true; 
                return GetParameterIdentifiers(assignmentPattern.Left);
            }

            return Enumerable.Empty<Identifier>();
        }

        private void ProcessParameters(IFunction functionDeclaration)
        {
            var parameterNames = new List<string>();
            var functionDeclarationParams = functionDeclaration.Params;
            int count = functionDeclarationParams.Count;
            _isSimpleParameterList  = true;
            for (var i = 0; i < count; i++)
            {
                var parameter = functionDeclarationParams[i];
                if (parameter is Identifier id)
                {
                    _hasDuplicates |= parameterNames.Contains(id.Name);
                    _hasArguments = id.Name == "arguments";
                    parameterNames.Add(id.Name);
                    if (_isSimpleParameterList )
                    {
                        _length++;
                    }
                }
                else
                {
                    _isSimpleParameterList  = false;
                    foreach (var identifier in GetParameterIdentifiers(parameter))
                    {
                        _hasDuplicates |= parameterNames.Contains(identifier.Name);
                        _hasArguments = identifier.Name == "arguments";
                        parameterNames.Add(identifier.Name);
                    }
                }
            }

            _parameterNames = parameterNames.ToArray();
        }
    }
}