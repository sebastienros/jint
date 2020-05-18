using System.Collections.Generic;
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

        private static void GetBoundNames(
            Expression parameter,
            List<string> target, 
            bool checkDuplicates, 
            ref bool _hasRestParameter, 
            ref bool _hasParameterExpressions, 
            ref bool _hasDuplicates)
        {
            if (parameter is Identifier identifier)
            {
                _hasDuplicates |= checkDuplicates && target.Contains(identifier.Name);
                target.Add(identifier.Name);
                return;
            }

            while (true)
            {
                if (parameter is RestElement restElement)
                {
                    _hasRestParameter = true;
                    _hasParameterExpressions = true;
                    parameter = restElement.Argument;
                    continue;
                }
                else if (parameter is ArrayPattern arrayPattern)
                {
                    _hasParameterExpressions = true;
                    ref readonly var arrayPatternElements = ref arrayPattern.Elements;
                    for (var i = 0; i < arrayPatternElements.Count; i++)
                    {
                        var expression = arrayPatternElements[i];
                        GetBoundNames(
                            expression, 
                            target,
                            checkDuplicates,
                            ref _hasRestParameter,
                            ref _hasParameterExpressions,
                            ref _hasDuplicates);
                    }
                }
                else if (parameter is ObjectPattern objectPattern)
                {
                    _hasParameterExpressions = true;
                    ref readonly var objectPatternProperties = ref objectPattern.Properties;
                    for (var i = 0; i < objectPatternProperties.Count; i++)
                    {
                        var property = objectPatternProperties[i];
                        if (property is Property p)
                        {
                            GetBoundNames(
                                p.Value, 
                                target,
                                checkDuplicates,
                                ref _hasRestParameter,
                                ref _hasParameterExpressions,
                                ref _hasDuplicates);
                        }
                        else
                        {
                            _hasRestParameter = true;
                            _hasParameterExpressions = true;
                            parameter = ((RestElement) property).Argument;
                            continue;
                        }
                    }
                }
                else if (parameter is AssignmentPattern assignmentPattern)
                {
                    _hasParameterExpressions = true;
                    parameter = assignmentPattern.Left;
                    continue;
                }

                break;
            }
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
                    if (_isSimpleParameterList)
                    {
                        _length++;
                    }
                }
                else if (parameter.Type != Nodes.Literal)
                {
                    _isSimpleParameterList  = false;
                    int start = parameterNames.Count;
                    GetBoundNames(parameter, parameterNames, checkDuplicates: true, ref _hasRestParameter, ref _hasParameterExpressions, ref _hasDuplicates);
                    for (var j = start; j < parameterNames.Count; j++)
                    {
                        var identifier = parameterNames[j];
                        _hasArguments = identifier == "arguments";
                    }
                }
            }

            _parameterNames = parameterNames.ToArray();
        }
    }
}