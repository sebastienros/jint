using System.Collections.Generic;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    internal sealed class JintFunctionDefinition
    {
        internal readonly IFunction _function;
        internal readonly string _name;
        internal readonly bool _strict;
        internal readonly string[] _parameterNames;
        internal readonly JintStatement _body;
        internal bool _hasRestParameter;

        public readonly HoistingScope _hoistingScope;

        public JintFunctionDefinition(Engine engine, IFunction function)
        {
            _function = function;
            _hoistingScope = function.HoistingScope;
            _name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id.Name : null;
            _strict = function.Strict;
            _parameterNames = GetParameterNames(function);

            Statement bodyStatement;
            if (function.Expression)
            {
                bodyStatement = new ReturnStatement((Expression) function.Body);
            }
            else
            {
                bodyStatement = (BlockStatement) function.Body;
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
                return GetParameterIdentifiers(restElement.Argument);
            }
            if (parameter is ArrayPattern arrayPattern)
            {
                return arrayPattern.Elements.SelectMany(GetParameterIdentifiers);
            }
            if (parameter is ObjectPattern objectPattern)
            {
                return objectPattern.Properties.SelectMany(property => GetParameterIdentifiers(property.Value));
            }
            if (parameter is AssignmentPattern assignmentPattern)
            {
                return GetParameterIdentifiers(assignmentPattern.Left);
            }

            return Enumerable.Empty<Identifier>();
        }

        private string[] GetParameterNames(IFunction functionDeclaration)
        {
            var parameterNames = new List<string>();
            var functionDeclarationParams = functionDeclaration.Params;
            int count = functionDeclarationParams.Count;
            for (var i = 0; i < count; i++)
            {
                var parameter = functionDeclarationParams[i];
                if (parameter is Identifier id)
                {
                    parameterNames.Add(id.Name);
                }
                else
                {
                    foreach (var identifier in GetParameterIdentifiers(parameter))
                    {
                        parameterNames.Add(identifier.Name);
                    }
                }
            }

            return parameterNames.ToArray();
        }
    }
}