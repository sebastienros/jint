using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Global;
using Jint.Native.Json;
using Jint.Native.Math;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Parser;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.References;

namespace Jint
{
    using Jint.Runtime.CallStack;

    public class Engine
    {
        private readonly ExpressionInterpreter _expressions;
        private readonly StatementInterpreter _statements;
        private readonly Stack<ExecutionContext> _executionContexts;
        private JsValue _completionValue = JsValue.Undefined;
        private int _statementsCount;
        private long _timeoutTicks;
        private SyntaxNode _lastSyntaxNode = null;
        
        public ITypeConverter ClrTypeConverter;

        // cache of types used when resolving CLR type names
        internal Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        internal JintCallStack CallStack = new JintCallStack();

        public Engine() : this(null)
        {
        }

        public Engine(Action<Options> options)
        {
            _executionContexts = new Stack<ExecutionContext>();

            Global = GlobalObject.CreateGlobalObject(this);

            Object = ObjectConstructor.CreateObjectConstructor(this);
            Function = FunctionConstructor.CreateFunctionConstructor(this);

            Array = ArrayConstructor.CreateArrayConstructor(this);
            String = StringConstructor.CreateStringConstructor(this);
            RegExp = RegExpConstructor.CreateRegExpConstructor(this);
            Number = NumberConstructor.CreateNumberConstructor(this);
            Boolean = BooleanConstructor.CreateBooleanConstructor(this);
            Date = DateConstructor.CreateDateConstructor(this);
            Math = MathInstance.CreateMathObject(this);
            Json = JsonInstance.CreateJsonObject(this);

            Error = ErrorConstructor.CreateErrorConstructor(this, "Error");
            EvalError = ErrorConstructor.CreateErrorConstructor(this, "EvalError");
            RangeError = ErrorConstructor.CreateErrorConstructor(this, "RangeError");
            ReferenceError = ErrorConstructor.CreateErrorConstructor(this, "ReferenceError");
            SyntaxError = ErrorConstructor.CreateErrorConstructor(this, "SyntaxError");
            TypeError = ErrorConstructor.CreateErrorConstructor(this, "TypeError");
            UriError = ErrorConstructor.CreateErrorConstructor(this, "URIError");

            // Because the properties might need some of the built-in object
            // their configuration is delayed to a later step

            Global.Configure();

            Object.Configure();
            Object.PrototypeObject.Configure();

            Function.Configure();
            Function.PrototypeObject.Configure();

            Array.Configure();
            Array.PrototypeObject.Configure();

            String.Configure();
            String.PrototypeObject.Configure();

            RegExp.Configure();
            RegExp.PrototypeObject.Configure();

            Number.Configure();
            Number.PrototypeObject.Configure();

            Boolean.Configure();
            Boolean.PrototypeObject.Configure();

            Date.Configure();
            Date.PrototypeObject.Configure();

            Math.Configure();
            Json.Configure();

            Error.Configure();
            Error.PrototypeObject.Configure();

            // create the global environment http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.3
            GlobalEnvironment = LexicalEnvironment.NewObjectEnvironment(this, Global, null, false);
            
            // create the global execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.1.1
            EnterExecutionContext(GlobalEnvironment, GlobalEnvironment, Global);

            Options = new Options();

            if (options != null)
            {
                options(Options);
            }

            Eval = new EvalFunctionInstance(this, new string[0], LexicalEnvironment.NewDeclarativeEnvironment(this, ExecutionContext.LexicalEnvironment), StrictModeScope.IsStrictModeCode);
            Global.FastAddProperty("eval", Eval, true, false, true);

            _statements = new StatementInterpreter(this);
            _expressions = new ExpressionInterpreter(this);

            if (Options._IsClrAllowed)
            {
                Global.FastAddProperty("System", new NamespaceReference(this, "System"), false, false, false);
                Global.FastAddProperty("importNamespace", new ClrFunctionInstance(this, (thisObj, arguments) =>
                {
                    return new NamespaceReference(this, TypeConverter.ToString(arguments.At(0)));
                }), false, false, false);
            }

            ClrTypeConverter = new DefaultTypeConverter(this);
            BreakPoints = new List<BreakPoint>();
            DebugHandler = new DebugHandler(this);
        }

        public LexicalEnvironment GlobalEnvironment;

        public GlobalObject Global { get; private set; }
        public ObjectConstructor Object { get; private set; }
        public FunctionConstructor Function { get; private set; }
        public ArrayConstructor Array { get; private set; }
        public StringConstructor String { get; private set; }
        public RegExpConstructor RegExp { get; private set; }
        public BooleanConstructor Boolean { get; private set; }
        public NumberConstructor Number { get; private set; }
        public DateConstructor Date { get; private set; }
        public MathInstance Math { get; private set; }
        public JsonInstance Json { get; private set; }
        public EvalFunctionInstance Eval { get; private set; }

        public ErrorConstructor Error { get; private set; }
        public ErrorConstructor EvalError { get; private set; }
        public ErrorConstructor SyntaxError { get; private set; }
        public ErrorConstructor TypeError { get; private set; }
        public ErrorConstructor RangeError { get; private set; }
        public ErrorConstructor ReferenceError { get; private set; }
        public ErrorConstructor UriError { get; private set; }

        public ExecutionContext ExecutionContext { get { return _executionContexts.Peek(); } }

        internal Options Options { get; private set; }
        
        #region Debugger
        public delegate StepMode DebugStepDelegate(object sender, DebugInformation e);
        public delegate StepMode BreakDelegate(object sender, DebugInformation e);
        public event DebugStepDelegate Step;
        public event BreakDelegate Break;
        internal DebugHandler DebugHandler { get; private set; }
        public List<BreakPoint> BreakPoints { get; private set; }

        internal StepMode? InvokeStepEvent(DebugInformation info)
        {
            if (Step != null)
            {
                return Step(this, info);
            }
            return null;
        }

        internal StepMode? InvokeBreakEvent(DebugInformation info)
        {
            if (Break != null)
            {
                return Break(this, info);
            }
            return null;
        }
        #endregion

        public ExecutionContext EnterExecutionContext(LexicalEnvironment lexicalEnvironment, LexicalEnvironment variableEnvironment, JsValue thisBinding)
        {
            var executionContext = new ExecutionContext
                {
                    LexicalEnvironment = lexicalEnvironment,
                    VariableEnvironment = variableEnvironment,
                    ThisBinding = thisBinding
                };
            _executionContexts.Push(executionContext);

            return executionContext;
        }

        public Engine SetValue(string name, Delegate value)
        {
            Global.FastAddProperty(name, new DelegateWrapper(this, value), true, false, true);
            return this;
        }

        public Engine SetValue(string name, string value)
        {
            return SetValue(name, new JsValue(value));
        }

        public Engine SetValue(string name, double value)
        {
            return SetValue(name, new JsValue(value));
        }

        public Engine SetValue(string name, bool value)
        {
            return SetValue(name, new JsValue(value));
        }

        public Engine SetValue(string name, JsValue value)
        {
            Global.Put(name, value, false);
            return this;
        }

        public Engine SetValue(string name, Object obj)
        {
            return SetValue(name, JsValue.FromObject(this, obj));
        }

        public void LeaveExecutionContext()
        {
            _executionContexts.Pop();
        }

        /// <summary>
        /// Initializes the statements count
        /// </summary>
        public void ResetStatementsCount()
        {
            _statementsCount = 0;
        }
        
        public void ResetTimeoutTicks()
        {
            var timeoutIntervalTicks = Options._TimeoutInterval.Ticks;
            _timeoutTicks = timeoutIntervalTicks > 0 ? DateTime.UtcNow.Ticks + timeoutIntervalTicks : 0;
        }

        /// <summary>
        /// Initializes list of references of called functions
        /// </summary>
        public void ResetCallStack()
        {
            CallStack.Clear();
        }

        public Engine Execute(string source)
        {
            var parser = new JavaScriptParser();
            return Execute(parser.Parse(source));
        }

        public Engine Execute(string source, ParserOptions parserOptions)
        {
            var parser = new JavaScriptParser();
            return Execute(parser.Parse(source, parserOptions));
        }

        public Engine Execute(Program program)
        {
            ResetStatementsCount();
            ResetTimeoutTicks();
            ResetLastStatement();
            ResetCallStack();

            using (new StrictModeScope(Options._IsStrict || program.Strict))
            {
                DeclarationBindingInstantiation(DeclarationBindingType.GlobalCode, program.FunctionDeclarations, program.VariableDeclarations, null, null);

                var result = _statements.ExecuteProgram(program);
                if (result.Type == Completion.Throw)
                {
                    throw new JavaScriptException(result.GetValueOrDefault())
                    {
                        Location = result.Location
                    };
                }

                _completionValue = result.GetValueOrDefault();
            }

            return this;
        }

        private void ResetLastStatement()
        {
            _lastSyntaxNode = null;
        }

        /// <summary>
        /// Gets the last evaluated statement completion value
        /// </summary>
        public JsValue GetCompletionValue()
        {
            return _completionValue;
        }

        public Completion ExecuteStatement(Statement statement)
        {
            var maxStatements = Options._MaxStatements;
            if (maxStatements > 0 && _statementsCount++ > maxStatements)
            {
                throw new StatementsCountOverflowException();
            }

            if (_timeoutTicks > 0 && _timeoutTicks < DateTime.UtcNow.Ticks)
            {
                throw new TimeoutException();
            }

            _lastSyntaxNode = statement;
            
            if (Options._IsDebugMode)
            {
                DebugHandler.OnStep(statement);
            }

            switch (statement.Type)
            {
                case SyntaxNodes.BlockStatement:
                    return _statements.ExecuteBlockStatement(statement.As<BlockStatement>());
                    
                case SyntaxNodes.BreakStatement:
                    return _statements.ExecuteBreakStatement(statement.As<BreakStatement>());
                    
                case SyntaxNodes.ContinueStatement:
                    return _statements.ExecuteContinueStatement(statement.As<ContinueStatement>());
                    
                case SyntaxNodes.DoWhileStatement:
                    return _statements.ExecuteDoWhileStatement(statement.As<DoWhileStatement>());
                    
                case SyntaxNodes.DebuggerStatement:
                    return _statements.ExecuteDebuggerStatement(statement.As<DebuggerStatement>());
                    
                case SyntaxNodes.EmptyStatement:
                    return _statements.ExecuteEmptyStatement(statement.As<EmptyStatement>());
                    
                case SyntaxNodes.ExpressionStatement:
                    return _statements.ExecuteExpressionStatement(statement.As<ExpressionStatement>());

                case SyntaxNodes.ForStatement:
                    return _statements.ExecuteForStatement(statement.As<ForStatement>());
                    
                case SyntaxNodes.ForInStatement:
                    return _statements.ExecuteForInStatement(statement.As<ForInStatement>());

                case SyntaxNodes.FunctionDeclaration:
                    return new Completion(Completion.Normal, null, null);
                    
                case SyntaxNodes.IfStatement:
                    return _statements.ExecuteIfStatement(statement.As<IfStatement>());
                    
                case SyntaxNodes.LabeledStatement:
                    return _statements.ExecuteLabelledStatement(statement.As<LabelledStatement>());

                case SyntaxNodes.ReturnStatement:
                    return _statements.ExecuteReturnStatement(statement.As<ReturnStatement>());
                    
                case SyntaxNodes.SwitchStatement:
                    return _statements.ExecuteSwitchStatement(statement.As<SwitchStatement>());
                    
                case SyntaxNodes.ThrowStatement:
                    return _statements.ExecuteThrowStatement(statement.As<ThrowStatement>());

                case SyntaxNodes.TryStatement:
                    return _statements.ExecuteTryStatement(statement.As<TryStatement>());
                    
                case SyntaxNodes.VariableDeclaration:
                    return _statements.ExecuteVariableDeclaration(statement.As<VariableDeclaration>());
                    
                case SyntaxNodes.WhileStatement:
                    return _statements.ExecuteWhileStatement(statement.As<WhileStatement>());
                    
                case SyntaxNodes.WithStatement:
                    return _statements.ExecuteWithStatement(statement.As<WithStatement>());

                case SyntaxNodes.Program:
                    return _statements.ExecuteProgram(statement.As<Program>());
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object EvaluateExpression(Expression expression)
        {
            _lastSyntaxNode = expression;

            switch (expression.Type)
            {
                case SyntaxNodes.AssignmentExpression:
                    return _expressions.EvaluateAssignmentExpression(expression.As<AssignmentExpression>());

                case SyntaxNodes.ArrayExpression:
                    return _expressions.EvaluateArrayExpression(expression.As<ArrayExpression>());

                case SyntaxNodes.BinaryExpression:
                    return _expressions.EvaluateBinaryExpression(expression.As<BinaryExpression>());

                case SyntaxNodes.CallExpression:
                    return _expressions.EvaluateCallExpression(expression.As<CallExpression>());

                case SyntaxNodes.ConditionalExpression:
                    return _expressions.EvaluateConditionalExpression(expression.As<ConditionalExpression>());

                case SyntaxNodes.FunctionExpression:
                    return _expressions.EvaluateFunctionExpression(expression.As<FunctionExpression>());

                case SyntaxNodes.Identifier:
                    return _expressions.EvaluateIdentifier(expression.As<Identifier>());

                case SyntaxNodes.Literal:
                    return _expressions.EvaluateLiteral(expression.As<Literal>());

                case SyntaxNodes.RegularExpressionLiteral:
                    return _expressions.EvaluateLiteral(expression.As<Literal>());

                case SyntaxNodes.LogicalExpression:
                    return _expressions.EvaluateLogicalExpression(expression.As<LogicalExpression>());

                case SyntaxNodes.MemberExpression:
                    return _expressions.EvaluateMemberExpression(expression.As<MemberExpression>());

                case SyntaxNodes.NewExpression:
                    return _expressions.EvaluateNewExpression(expression.As<NewExpression>());

                case SyntaxNodes.ObjectExpression:
                    return _expressions.EvaluateObjectExpression(expression.As<ObjectExpression>());

                case SyntaxNodes.SequenceExpression:
                    return _expressions.EvaluateSequenceExpression(expression.As<SequenceExpression>());

                case SyntaxNodes.ThisExpression:
                    return _expressions.EvaluateThisExpression(expression.As<ThisExpression>());

                case SyntaxNodes.UpdateExpression:
                    return _expressions.EvaluateUpdateExpression(expression.As<UpdateExpression>());

                case SyntaxNodes.UnaryExpression:
                    return _expressions.EvaluateUnaryExpression(expression.As<UnaryExpression>());

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.1
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public JsValue GetValue(object value)
        {
            var reference = value as Reference;

            if (reference == null)
            {
                var completion = value as Completion;

                if (completion != null)
                {
                    return GetValue(completion.Value);
                }

                return (JsValue)value;
            }

            if (reference.IsUnresolvableReference())
            {
                throw new JavaScriptException(ReferenceError, reference.GetReferencedName() + " is not defined");
            }

            var baseValue = reference.GetBase();

            if (reference.IsPropertyReference())
            {
                if (reference.HasPrimitiveBase() == false)
                {
                    var o = TypeConverter.ToObject(this, baseValue);
                    return o.Get(reference.GetReferencedName());
                }
                else
                {
                    var o = TypeConverter.ToObject(this, baseValue);
                    var desc = o.GetProperty(reference.GetReferencedName());
                    if (desc == PropertyDescriptor.Undefined)
                    {
                        return JsValue.Undefined;
                    }
                    
                    if (desc.IsDataDescriptor())
                    {
                        return desc.Value.Value;
                    }

                    var getter = desc.Get.Value;
                    if (getter == Undefined.Instance)
                    {
                        return Undefined.Instance;
                    }

                    var callable = (ICallable)getter.AsObject();
                    return callable.Call(baseValue, Arguments.Empty);
                }
            }
            else
            {
                var record = baseValue.As<EnvironmentRecord>();

                if (record == null)
                {
                    throw new ArgumentException();
                }

                return record.GetBindingValue(reference.GetReferencedName(), reference.IsStrict());    
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.2
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="value"></param>
        public void PutValue(Reference reference, JsValue value)
        {
            if (reference.IsUnresolvableReference())
            {
                if (reference.IsStrict())
                {
                    throw new JavaScriptException(ReferenceError);
                }

                Global.Put(reference.GetReferencedName(), value, false);
            }
            else if (reference.IsPropertyReference())
            {
                var baseValue = reference.GetBase();
                if (!reference.HasPrimitiveBase())
                {
                    baseValue.AsObject().Put(reference.GetReferencedName(), value, reference.IsStrict());
                }
                else
                {
                    PutPrimitiveBase(baseValue, reference.GetReferencedName(), value, reference.IsStrict());
                }
            }
            else
            {
                var baseValue = reference.GetBase();
                var record = baseValue.As<EnvironmentRecord>();

                if (record == null)
                {
                    throw new ArgumentNullException();
                }

                record.SetMutableBinding(reference.GetReferencedName(), value, reference.IsStrict());
            }
        }

        /// <summary>
        /// Used by PutValue when the reference has a primitive base value
        /// </summary>
        /// <param name="b"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="throwOnError"></param>
        public void PutPrimitiveBase(JsValue b, string name, JsValue value, bool throwOnError)
        {
            var o = TypeConverter.ToObject(this, b);
            if (!o.CanPut(name))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(TypeError);
                }

                return;
            }

            var ownDesc = o.GetOwnProperty(name);

            if (ownDesc.IsDataDescriptor())
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(TypeError);
                }

                return;
            }

            var desc = o.GetProperty(name);

            if (desc.IsAccessorDescriptor())
            {
                var setter = (ICallable)desc.Set.Value.AsObject();
                setter.Call(b, new[] { value });
            }
            else
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(TypeError);
                }
            }
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="propertyName">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public JsValue Invoke(string propertyName, params object[] arguments)
        {
            return Invoke(propertyName, null, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="propertyName">The name of the function to call.</param>
        /// <param name="thisObj">The this value inside the function call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public JsValue Invoke(string propertyName, object thisObj, object[] arguments)
        {
            var value = GetValue(propertyName);
            var callable = value.TryCast<ICallable>();

            if (callable == null)
            {
                throw new ArgumentException("Can only invoke functions");
            }

            return callable.Call(JsValue.FromObject(this, thisObj), arguments.Select(x => JsValue.FromObject(this, x)).ToArray());
        }

        /// <summary>
        /// Gets a named value from the Global scope.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        public JsValue GetValue(string propertyName)
        {
            return GetValue(Global, propertyName);
        }

        /// <summary>
        /// Gets the last evaluated <see cref="SyntaxNode"/>.
        /// </summary>
        public SyntaxNode GetLastSyntaxNode()
        {
            return _lastSyntaxNode;
        }

        /// <summary>
        /// Gets a named value from the specified scope.
        /// </summary>
        /// <param name="scope">The scope to get the property from.</param>
        /// <param name="propertyName">The name of the property to return.</param>
        public JsValue GetValue(JsValue scope, string propertyName)
        {
            if (System.String.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("propertyName");
            }

            var reference = new Reference(scope, propertyName, Options._IsStrict);

            return GetValue(reference);
        }

        //  http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
        public void DeclarationBindingInstantiation(DeclarationBindingType declarationBindingType, IList<FunctionDeclaration> functionDeclarations, IList<VariableDeclaration> variableDeclarations, FunctionInstance functionInstance, JsValue[] arguments)
        {
            var env = ExecutionContext.VariableEnvironment.Record;
            bool configurableBindings = declarationBindingType == DeclarationBindingType.EvalCode;
            var strict = StrictModeScope.IsStrictModeCode;

            if (declarationBindingType == DeclarationBindingType.FunctionCode)
            {
                var argCount = arguments.Length;
                var n = 0;
                foreach (var argName in functionInstance.FormalParameters)
                {
                    n++;
                    var v = n > argCount ? Undefined.Instance : arguments[n - 1];
                    var argAlreadyDeclared = env.HasBinding(argName);
                    if (!argAlreadyDeclared)
                    {
                        env.CreateMutableBinding(argName);
                    }

                    env.SetMutableBinding(argName, v, strict);
                }
            }

            foreach (var f in functionDeclarations)
            {
                var fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f);
                var funcAlreadyDeclared = env.HasBinding(fn);
                if (!funcAlreadyDeclared)
                {
                    env.CreateMutableBinding(fn, configurableBindings);
                }
                else
                {
                    if (env == GlobalEnvironment.Record)
                    {
                        var go = Global;
                        var existingProp = go.GetProperty(fn);
                        if (existingProp.Configurable.Value)
                        {
                            go.DefineOwnProperty(fn,
                                                 new PropertyDescriptor(
                                                     value: Undefined.Instance,
                                                     writable: true,
                                                     enumerable: true,
                                                     configurable: configurableBindings
                                                     ), true);
                        }
                        else
                        {
                            if (existingProp.IsAccessorDescriptor() || (!existingProp.Enumerable.Value))
                            {
                                throw new JavaScriptException(TypeError);
                            }
                        }
                    }
                }

                env.SetMutableBinding(fn, fo, strict);
            }

            var argumentsAlreadyDeclared = env.HasBinding("arguments");

            if (declarationBindingType == DeclarationBindingType.FunctionCode && !argumentsAlreadyDeclared)
            {
                var argsObj = ArgumentsInstance.CreateArgumentsObject(this, functionInstance, functionInstance.FormalParameters, arguments, env, strict);

                if (strict)
                {
                    var declEnv = env as DeclarativeEnvironmentRecord;

                    if (declEnv == null)
                    {
                        throw new ArgumentException();
                    }

                    declEnv.CreateImmutableBinding("arguments");
                    declEnv.InitializeImmutableBinding("arguments", argsObj);
                }
                else
                {
                    env.CreateMutableBinding("arguments");
                    env.SetMutableBinding("arguments", argsObj, false);
                }
            }

            // process all variable declarations in the current parser scope
            foreach (var d in variableDeclarations.SelectMany(x => x.Declarations))
            {
                var dn = d.Id.Name;
                var varAlreadyDeclared = env.HasBinding(dn);
                if (!varAlreadyDeclared)
                {
                    env.CreateMutableBinding(dn, configurableBindings);
                    env.SetMutableBinding(dn, Undefined.Instance, strict);
                }
            }
        }
    }
}