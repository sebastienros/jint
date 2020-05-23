using System;
using System.Collections.Generic;
using System.Linq;
using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter
{
    /// <summary>
    /// Works as memento for function execution. Optimization to cache things that don't change.
    /// </summary>
    internal sealed class JintFunctionDefinition
    {
        private readonly Engine _engine;
        
        private JintStatement _body;
        
        public readonly string Name;
        public readonly bool Strict;
        public readonly IFunction Function;

        private State _state;

        public JintFunctionDefinition(
            Engine engine,
            IFunction function)
        {
            _engine = engine;
            Function = function;
            Name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id.Name : null;
            Strict = function.Strict;

            if (!Strict && !function.Expression)
            {
                // Esprima doesn't detect strict at the moment for
                // language/expressions/object/method-definition/name-invoke-fn-strict.js
                var blockStatement = (BlockStatement) function.Body;
                ref readonly var statements = ref blockStatement.Body;
                for (int i = 0; i < statements.Count; ++i)
                {
                    if (statements[i] is Directive d && d.Directiv == "use strict")
                    {
                        Strict = true;
                    }
                }
            }
        }

        public JintStatement Body
        {
            get
            {
                if (_body != null)
                {
                    return _body;
                }

                _body = Function.Expression
                    ? (JintStatement) new JintReturnStatement(_engine, new ReturnStatement((Expression) Function.Body))
                    : new JintBlockStatement(_engine, (BlockStatement) Function.Body);

                return _body;
            }
        }

        internal State Initialize(Engine engine, FunctionInstance functionInstance)
        {
            return _state ??= DoInitialize(functionInstance);
        }

        internal class State
        {
            public bool HasRestParameter;
            public int Length;
            public Key[] ParameterNames;
            public bool HasDuplicates;
            public bool IsSimpleParameterList;
            public bool HasParameterExpressions;
            public bool ArgumentsObjectNeeded;
            public List<Key> VarNames;
            public FunctionDeclaration[] FunctionsToInitialize;
            public readonly HashSet<Key> FunctionNames = new HashSet<Key>();
            public List<VariableDeclaration> _lexicalDeclarations;
            public HashSet<Key> ParameterBindings;
        }

        private State DoInitialize(FunctionInstance functionInstance)
        {
            var state = new State();
            
            ProcessParameters(Function, state, out var hasArguments);

            var hoistingScope = HoistingScope.GetFunctionLevelDeclarations(Function, collectVarNames: true, collectLexicalNames: true);
            var functionDeclarations = hoistingScope._functionDeclarations;
            var lexicalNames = hoistingScope._lexicalNames;
            state.VarNames = hoistingScope._varNames;
            state._lexicalDeclarations = hoistingScope._lexicalDeclarations;

            LinkedList<FunctionDeclaration> functionsToInitialize = null;

            if (functionDeclarations != null)
            {
                functionsToInitialize = new LinkedList<FunctionDeclaration>();
                for (var i = functionDeclarations.Count - 1; i >= 0; i--)
                {
                    var d = functionDeclarations[i];
                    var fn = d.Id.Name;
                    if (state.FunctionNames.Add(fn))
                    {
                        functionsToInitialize.AddFirst(d);
                    }
                }
            }

            state.FunctionsToInitialize = functionsToInitialize != null
                ? functionsToInitialize.ToArray()
                : Array.Empty<FunctionDeclaration>();

            const string ParameterNameArguments = "arguments";

            state.ArgumentsObjectNeeded = true;
            if (functionInstance._thisMode == FunctionInstance.FunctionThisMode.Lexical)
            {
                state.ArgumentsObjectNeeded = false;
            }
            else if (hasArguments)
            {
                state.ArgumentsObjectNeeded = false;
            }
            else if (!state.HasParameterExpressions)
            {
                if (state.FunctionNames.Contains(ParameterNameArguments)
                    || lexicalNames?.Contains(ParameterNameArguments) == true)
                {
                    state.ArgumentsObjectNeeded = false;
                }
            }
            
            var parameterBindings = new HashSet<Key>(state.ParameterNames);
            if (state.ArgumentsObjectNeeded)
            {
                parameterBindings.Add(KnownKeys.Arguments);
            }

            state.ParameterBindings = parameterBindings;

            return state;
        }

        private static void GetBoundNames(
            Expression parameter,
            List<Key> target, 
            bool checkDuplicates, 
            ref bool _hasRestParameter, 
            ref bool _hasParameterExpressions, 
            ref bool _hasDuplicates,
            ref bool hasArguments)
        {
            if (parameter is Identifier identifier)
            {
                _hasDuplicates |= checkDuplicates && target.Contains(identifier.Name);
                target.Add(identifier.Name);
                hasArguments |= identifier.Name == "arguments";
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

                if (parameter is ArrayPattern arrayPattern)
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
                            ref _hasDuplicates,
                            ref hasArguments);
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
                                ref _hasDuplicates,
                                ref hasArguments);
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

        private static void ProcessParameters(
            IFunction function,
            State state,
            out bool hasArguments)
        {
            hasArguments = false;
            state.IsSimpleParameterList  = true;

            ref readonly var functionDeclarationParams = ref function.Params;
            var count = functionDeclarationParams.Count;
            var parameterNames = new List<Key>(count);
            for (var i = 0; i < count; i++)
            {
                var parameter = functionDeclarationParams[i];
                if (parameter is Identifier id)
                {
                    state.HasDuplicates |= parameterNames.Contains(id.Name);
                    hasArguments = id.Name == "arguments";
                    parameterNames.Add(id.Name);
                    if (state.IsSimpleParameterList)
                    {
                        state.Length++;
                    }
                }
                else if (parameter.Type != Nodes.Literal)
                {
                    state.IsSimpleParameterList = false;
                    GetBoundNames(
                        parameter, 
                        parameterNames,
                        checkDuplicates: true,
                        ref state.HasRestParameter,
                        ref state.HasParameterExpressions, 
                        ref state.HasDuplicates,
                        ref hasArguments);
                }
            }

            state.ParameterNames = parameterNames.ToArray();
        }
    }
}