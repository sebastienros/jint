using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter
{
    /// <summary>
    /// Works as memento for function execution. Optimization to cache things that don't change.
    /// </summary>
    internal sealed class JintFunctionDefinition
    {
        private readonly Engine _engine;

        private JintExpression? _bodyExpression;
        private JintStatementList? _bodyStatementList;

        public readonly string? Name;
        public readonly bool Strict;
        public readonly IFunction Function;

        private State? _state;

        public JintFunctionDefinition(
            Engine engine,
            IFunction function)
        {
            _engine = engine;
            Function = function;
            Name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id!.Name : null;
            Strict = function.Strict;
        }

        public FunctionThisMode ThisMode => Strict ? FunctionThisMode.Strict : FunctionThisMode.Global;

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluatebody
        /// </summary>
        internal Completion EvaluateBody(EvaluationContext context, FunctionInstance functionObject, JsValue[] argumentsList)
        {
            Completion result;
            if (Function.Expression)
            {
                result = EvaluateConciseBody(context, functionObject, argumentsList);
            }
            else if (Function.Generator)
            {
                result = EvaluateFunctionBody(context, functionObject, argumentsList);
                // TODO generators
                // result = EvaluateGeneratorBody(functionObject, argumentsList);
            }
            else
            {
                result = EvaluateFunctionBody(context, functionObject, argumentsList);
            }

            return new Completion(result.Type, result.GetValueOrDefault().Clone(), result.Target, result.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluategeneratorbody
        /// </summary>
        private Completion EvaluateGeneratorBody(FunctionInstance functionObject, JsValue[] argumentsList)
        {
            ExceptionHelper.ThrowNotImplementedException("generators not implemented");
            return default;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluateconcisebody
        /// </summary>
        private Completion EvaluateConciseBody(EvaluationContext context, FunctionInstance functionObject, JsValue[] argumentsList)
        {
            var argumentsInstance = _engine.FunctionDeclarationInstantiation(functionObject, argumentsList);
            _bodyExpression ??= JintExpression.Build(_engine, (Expression) Function.Body);
            var jsValue = _bodyExpression?.GetValue(context).Value ?? Undefined.Instance;
            argumentsInstance?.FunctionWasCalled();
            return new Completion(CompletionType.Return, jsValue, null, Function.Body.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluatefunctionbody
        /// </summary>
        private Completion EvaluateFunctionBody(EvaluationContext context, FunctionInstance functionObject, JsValue[] argumentsList)
        {
            var argumentsInstance = _engine.FunctionDeclarationInstantiation(functionObject, argumentsList);
            _bodyStatementList ??= new JintStatementList(Function);
            var completion = _bodyStatementList.Execute(context);
            argumentsInstance?.FunctionWasCalled();

            return completion;
        }

        internal State Initialize(FunctionInstance functionInstance)
        {
            return _state ??= DoInitialize(functionInstance);
        }

        internal sealed class State
        {
            public bool HasRestParameter;
            public int Length;
            public Key[] ParameterNames = null!;
            public bool HasDuplicates;
            public bool IsSimpleParameterList;
            public bool HasParameterExpressions;
            public bool ArgumentsObjectNeeded;
            public List<Key>? VarNames;
            public LinkedList<JintFunctionDefinition>? FunctionsToInitialize;
            public readonly HashSet<Key> FunctionNames = new();
            public LexicalVariableDeclaration[] LexicalDeclarations = Array.Empty<LexicalVariableDeclaration>();
            public HashSet<Key>? ParameterBindings;
            public List<VariableValuePair>? VarsToInitialize;

            internal struct VariableValuePair
            {
                public Key Name;
                public JsValue? InitialValue;
            }

            internal struct LexicalVariableDeclaration
            {
                public bool IsConstantDeclaration;
                public List<string> BoundNames;
            }
        }

        private State DoInitialize(FunctionInstance functionInstance)
        {
            var state = new State();

            ProcessParameters(Function, state, out var hasArguments);

            var hoistingScope = HoistingScope.GetFunctionLevelDeclarations(Function, collectVarNames: true, collectLexicalNames: true);
            var functionDeclarations = hoistingScope._functionDeclarations;
            var lexicalNames = hoistingScope._lexicalNames;
            state.VarNames = hoistingScope._varNames;

            LinkedList<JintFunctionDefinition>? functionsToInitialize = null;

            if (functionDeclarations != null)
            {
                functionsToInitialize = new LinkedList<JintFunctionDefinition>();
                for (var i = functionDeclarations.Count - 1; i >= 0; i--)
                {
                    var d = functionDeclarations[i];
                    var fn = d.Id!.Name;
                    if (state.FunctionNames.Add(fn))
                    {
                        functionsToInitialize.AddFirst(new JintFunctionDefinition(_engine, d));
                    }
                }
            }

            state.FunctionsToInitialize = functionsToInitialize;

            const string ParameterNameArguments = "arguments";

            state.ArgumentsObjectNeeded = true;
            if (functionInstance._thisMode == FunctionThisMode.Lexical)
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


            var varsToInitialize = new List<State.VariableValuePair>();
            if (!state.HasParameterExpressions)
            {
                var instantiatedVarNames = state.VarNames != null
                    ? new HashSet<Key>(state.ParameterBindings)
                    : new HashSet<Key>();

                for (var i = 0; i < state.VarNames?.Count; i++)
                {
                    var n = state.VarNames[i];
                    if (instantiatedVarNames.Add(n))
                    {
                        varsToInitialize.Add(new State.VariableValuePair
                        {
                            Name = n
                        });
                    }
                }
            }
            else
            {
                var instantiatedVarNames = state.VarNames != null
                    ? new HashSet<Key>(state.ParameterBindings)
                    : null;

                for (var i = 0; i < state.VarNames?.Count; i++)
                {
                    var n = state.VarNames[i];
                    if (instantiatedVarNames!.Add(n))
                    {
                        JsValue? initialValue = null;
                        if (!state.ParameterBindings.Contains(n) || state.FunctionNames.Contains(n))
                        {
                            initialValue = JsValue.Undefined;
                        }

                        varsToInitialize.Add(new State.VariableValuePair
                        {
                            Name = n,
                            InitialValue = initialValue
                        });
                    }
                }
            }

            state.VarsToInitialize = varsToInitialize;

            if (hoistingScope._lexicalDeclarations != null)
            {
                var _lexicalDeclarations = hoistingScope._lexicalDeclarations;
                var lexicalDeclarationsCount = _lexicalDeclarations.Count;
                var declarations = new State.LexicalVariableDeclaration[lexicalDeclarationsCount];
                for (var i = 0; i < lexicalDeclarationsCount; i++)
                {
                    var d = _lexicalDeclarations[i];
                    var boundNames = new List<string>();
                    d.GetBoundNames(boundNames);
                    declarations[i] = new State.LexicalVariableDeclaration
                    {
                        IsConstantDeclaration = d.IsConstantDeclaration(),
                        BoundNames = boundNames
                    };
                }
                state.LexicalDeclarations = declarations;
            }

            return state;
        }

        private static void GetBoundNames(
            Node? parameter,
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
