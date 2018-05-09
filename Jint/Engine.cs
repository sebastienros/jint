using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
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
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.References;

namespace Jint
{
    public class Engine
    {
        private static readonly ParserOptions DefaultParserOptions = new ParserOptions
        {
            AdaptRegexp = true,
            Tolerant = false,
            Loc = true
        };

        private readonly ExpressionInterpreter _expressions;
        private readonly StatementInterpreter _statements;
        private readonly Stack<ExecutionContext> _executionContexts;
        private JsValue _completionValue = JsValue.Undefined;
        private int _statementsCount;
        private long _initialMemoryUsage;
        private long _timeoutTicks;
        private INode _lastSyntaxNode;

        // cached access
        private readonly bool _isDebugMode;
        private readonly bool _isStrict;
        private readonly int _maxStatements;
        private readonly IReferenceResolver _referenceResolver;
        
        public ITypeConverter ClrTypeConverter;

        // cache of types used when resolving CLR type names
        internal Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        internal static Dictionary<Type, Func<Engine, object, JsValue>> TypeMappers = new Dictionary<Type, Func<Engine, object, JsValue>>
        {
            { typeof(bool), (Engine engine, object v) => (bool) v ? JsBoolean.True : JsBoolean.False },
            { typeof(byte), (Engine engine, object v) => JsNumber.Create((byte)v) },
            { typeof(char), (Engine engine, object v) => JsString.Create((char)v) },
            { typeof(DateTime), (Engine engine, object v) => engine.Date.Construct((DateTime)v) },
            { typeof(DateTimeOffset), (Engine engine, object v) => engine.Date.Construct((DateTimeOffset)v) },
            { typeof(decimal), (Engine engine, object v) => (JsValue) (double)(decimal)v },
            { typeof(double), (Engine engine, object v) => (JsValue)(double)v },
            { typeof(Int16), (Engine engine, object v) => JsNumber.Create((Int16)v) },
            { typeof(Int32), (Engine engine, object v) => JsNumber.Create((Int32)v) },
            { typeof(Int64), (Engine engine, object v) => (JsValue)(Int64)v },
            { typeof(SByte), (Engine engine, object v) => JsNumber.Create((SByte)v) },
            { typeof(Single), (Engine engine, object v) => (JsValue)(Single)v },
            { typeof(string), (Engine engine, object v) => (JsValue) (string)v },
            { typeof(UInt16), (Engine engine, object v) => JsNumber.Create((UInt16)v) },
            { typeof(UInt32), (Engine engine, object v) => JsNumber.Create((UInt32)v) },
            { typeof(UInt64), (Engine engine, object v) => JsNumber.Create((UInt64)v) },
            { typeof(System.Text.RegularExpressions.Regex), (Engine engine, object v) => engine.RegExp.Construct((System.Text.RegularExpressions.Regex)v, "") }
        };

        internal JintCallStack CallStack = new JintCallStack();

        static Engine()
        {
            var methodInfo = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");

            if (methodInfo != null)
            {
                GetAllocatedBytesForCurrentThread =  (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), null, methodInfo);
            }
            else
            {
                GetAllocatedBytesForCurrentThread = () => long.MaxValue;
            }
        }

        public Engine() : this(null)
        {
        }

        public Engine(Action<Options> options)
        {
            _executionContexts = new Stack<ExecutionContext>();

            Global = GlobalObject.CreateGlobalObject(this);

            Object = ObjectConstructor.CreateObjectConstructor(this);
            Function = FunctionConstructor.CreateFunctionConstructor(this);

            Symbol = SymbolConstructor.CreateSymbolConstructor(this);
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

            GlobalSymbolRegistry = new GlobalSymbolRegistry();

            // Because the properties might need some of the built-in object
            // their configuration is delayed to a later step

            Global.Configure();

            Object.Configure();
            Object.PrototypeObject.Configure();

            Symbol.Configure();
            Symbol.PrototypeObject.Configure();

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

            options?.Invoke(Options);
            
            // gather some options as fields for faster checks
            _isDebugMode = Options.IsDebugMode;
            _isStrict = Options.IsStrict;
            _maxStatements = Options.MaxStatementCount;
            _referenceResolver = Options.ReferenceResolver;

            ReferencePool = new ReferencePool();
            ArgumentsInstancePool = new ArgumentsInstancePool(this);
            JsValueArrayPool = new JsValueArrayPool();

            Eval = new EvalFunctionInstance(this, System.Array.Empty<string>(), LexicalEnvironment.NewDeclarativeEnvironment(this, ExecutionContext.LexicalEnvironment), StrictModeScope.IsStrictModeCode);
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

        public LexicalEnvironment GlobalEnvironment { get; }
        public GlobalObject Global { get; }
        public ObjectConstructor Object { get; }
        public FunctionConstructor Function { get; }
        public ArrayConstructor Array { get; }
        public StringConstructor String { get; }
        public RegExpConstructor RegExp { get; }
        public BooleanConstructor Boolean { get; }
        public NumberConstructor Number { get; }
        public DateConstructor Date { get; }
        public MathInstance Math { get; }
        public JsonInstance Json { get; }
        public SymbolConstructor Symbol { get; }
        public EvalFunctionInstance Eval { get; }

        public ErrorConstructor Error { get; }
        public ErrorConstructor EvalError { get; }
        public ErrorConstructor SyntaxError { get; }
        public ErrorConstructor TypeError { get; }
        public ErrorConstructor RangeError { get; }
        public ErrorConstructor ReferenceError { get; }
        public ErrorConstructor UriError { get; }

        public ExecutionContext ExecutionContext => _executionContexts.Peek();

        public GlobalSymbolRegistry GlobalSymbolRegistry { get; }

        internal Options Options { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        internal ReferencePool ReferencePool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        internal ArgumentsInstancePool ArgumentsInstancePool { get; }
        internal JsValueArrayPool JsValueArrayPool { get; }

        #region Debugger
        public delegate StepMode DebugStepDelegate(object sender, DebugInformation e);
        public delegate StepMode BreakDelegate(object sender, DebugInformation e);
        public event DebugStepDelegate Step;
        public event BreakDelegate Break;
        internal DebugHandler DebugHandler { get; private set; }
        public List<BreakPoint> BreakPoints { get; private set; }

        internal StepMode? InvokeStepEvent(DebugInformation info)
        {
            return Step?.Invoke(this, info);
        }

        internal StepMode? InvokeBreakEvent(DebugInformation info)
        {
            return Break?.Invoke(this, info);
        }
        #endregion

        private static Func<long> GetAllocatedBytesForCurrentThread;

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
            return SetValue(name, (JsValue) value);
        }

        public Engine SetValue(string name, double value)
        {
            return SetValue(name, JsNumber.Create(value));
        }

        public Engine SetValue(string name, int value)
        {
            return SetValue(name, JsNumber.Create(value));
        }

        public Engine SetValue(string name, bool value)
        {
            return SetValue(name, value ? JsBoolean.True : JsBoolean.False);
        }

        public Engine SetValue(string name, JsValue value)
        {
            Global.Put(name, value, false);
            return this;
        }

        public Engine SetValue(string name, object obj)
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

        public void ResetMemoryUsage()
        {
            _initialMemoryUsage = GetAllocatedBytesForCurrentThread();
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
            return Execute(source, DefaultParserOptions);
        }

        public Engine Execute(string source, ParserOptions parserOptions)
        {
            var parser = new JavaScriptParser(source, parserOptions);
            return Execute(parser.ParseProgram());
        }

        public Engine Execute(Program program)
        {
            ResetStatementsCount();
            
            if (Options._MemoryLimit > 0)
            {
                ResetMemoryUsage();
            }
            
            ResetTimeoutTicks();
            ResetLastStatement();
            ResetCallStack();

            using (new StrictModeScope(_isStrict || program.Strict))
            {
                DeclarationBindingInstantiation(DeclarationBindingType.GlobalCode, program.HoistingScope.FunctionDeclarations, program.HoistingScope.VariableDeclarations, null, null);

                var result = _statements.ExecuteProgram(program);
                if (result.Type == CompletionType.Throw)
                {
                    var ex = new JavaScriptException(result.GetValueOrDefault()).SetCallstack(this, result.Location);
                    throw ex;
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
            if (_maxStatements > 0 && _statementsCount++ > _maxStatements)
            {
                throw new StatementsCountOverflowException();
            }

            if (_timeoutTicks > 0 && _timeoutTicks < DateTime.UtcNow.Ticks)
            {
                throw new TimeoutException();
            }

            if (Options._MemoryLimit > 0)
            {
                var memoryUsage = GetAllocatedBytesForCurrentThread() - _initialMemoryUsage;
                if (memoryUsage > Options._MemoryLimit)
                {
                    throw new MemoryLimitExceededException($"Script has allocated {memoryUsage} but is limited to {Options._MemoryLimit}");
                }
            }

            _lastSyntaxNode = statement;

            if (_isDebugMode)
            {
                DebugHandler.OnStep(statement);
            }

            switch (statement.Type)
            {
                case Nodes.BlockStatement:
                    return _statements.ExecuteBlockStatement((BlockStatement) statement);

                case Nodes.ReturnStatement:
                    return _statements.ExecuteReturnStatement((ReturnStatement) statement);

                case Nodes.VariableDeclaration:
                    return _statements.ExecuteVariableDeclaration((VariableDeclaration) statement);

                case Nodes.BreakStatement:
                    return _statements.ExecuteBreakStatement((BreakStatement) statement);

                case Nodes.ContinueStatement:
                    return _statements.ExecuteContinueStatement((ContinueStatement) statement);

                case Nodes.DoWhileStatement:
                    return _statements.ExecuteDoWhileStatement((DoWhileStatement) statement);

                case Nodes.EmptyStatement:
                    return _statements.ExecuteEmptyStatement((EmptyStatement) statement);

                case Nodes.ExpressionStatement:
                    return _statements.ExecuteExpressionStatement((ExpressionStatement) statement);

                case Nodes.ForStatement:
                    return _statements.ExecuteForStatement((ForStatement) statement);

                case Nodes.ForInStatement:
                    return _statements.ExecuteForInStatement((ForInStatement) statement);

                case Nodes.IfStatement:
                    return _statements.ExecuteIfStatement((IfStatement) statement);

                case Nodes.LabeledStatement:
                    return _statements.ExecuteLabeledStatement((LabeledStatement) statement);

                case Nodes.SwitchStatement:
                    return _statements.ExecuteSwitchStatement((SwitchStatement) statement);

                case Nodes.FunctionDeclaration:
                    return Completion.Empty;

                case Nodes.ThrowStatement:
                    return _statements.ExecuteThrowStatement((ThrowStatement) statement);

                case Nodes.TryStatement:
                    return _statements.ExecuteTryStatement((TryStatement) statement);

                case Nodes.WhileStatement:
                    return _statements.ExecuteWhileStatement((WhileStatement) statement);

                case Nodes.WithStatement:
                    return _statements.ExecuteWithStatement((WithStatement) statement);

                case Nodes.DebuggerStatement:
                    return _statements.ExecuteDebuggerStatement((DebuggerStatement) statement);

                case Nodes.Program:
                    return _statements.ExecuteProgram((Program) statement);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object EvaluateExpression(INode expression)
        {
            _lastSyntaxNode = expression;

            switch (expression.Type)
            {
                case Nodes.AssignmentExpression:
                    return _expressions.EvaluateAssignmentExpression((AssignmentExpression) expression);

                case Nodes.ArrayExpression:
                    return _expressions.EvaluateArrayExpression((ArrayExpression) expression);

                case Nodes.BinaryExpression:
                    return _expressions.EvaluateBinaryExpression((BinaryExpression) expression);

                case Nodes.CallExpression:
                    return _expressions.EvaluateCallExpression((CallExpression) expression);

                case Nodes.ConditionalExpression:
                    return _expressions.EvaluateConditionalExpression((ConditionalExpression) expression);

                case Nodes.FunctionExpression:
                    return _expressions.EvaluateFunctionExpression((IFunction) expression);

                case Nodes.Identifier:
                    return _expressions.EvaluateIdentifier((Identifier) expression);

                case Nodes.Literal:
                    return _expressions.EvaluateLiteral((Literal) expression);

                case Nodes.LogicalExpression:
                    return _expressions.EvaluateLogicalExpression((BinaryExpression) expression);

                case Nodes.MemberExpression:
                    return _expressions.EvaluateMemberExpression((MemberExpression) expression);

                case Nodes.NewExpression:
                    return _expressions.EvaluateNewExpression((NewExpression) expression);

                case Nodes.ObjectExpression:
                    return _expressions.EvaluateObjectExpression((ObjectExpression) expression);

                case Nodes.SequenceExpression:
                    return _expressions.EvaluateSequenceExpression((SequenceExpression) expression);

                case Nodes.ThisExpression:
                    return _expressions.EvaluateThisExpression((ThisExpression) expression);

                case Nodes.UpdateExpression:
                    return _expressions.EvaluateUpdateExpression((UpdateExpression) expression);

                case Nodes.UnaryExpression:
                    return _expressions.EvaluateUnaryExpression((UnaryExpression) expression);

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
            return GetValue(value, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal JsValue GetValue(object value, bool returnReferenceToPool)
        {
            var jsValue = value as JsValue;
            if (!ReferenceEquals(jsValue, null))
            {
                return jsValue;
            }

            var reference = value as Reference;
            if (reference == null)
            {
                if (value is Completion completion)
                {
                    return GetValue(completion.Value, returnReferenceToPool);
                }
            }

            if (reference.IsUnresolvableReference())
            {
                if (_referenceResolver != null &&
                    _referenceResolver.TryUnresolvableReference(this, reference, out JsValue val))
                {
                    return val;
                }
                throw new JavaScriptException(ReferenceError, reference.GetReferencedName() + " is not defined");
            }

            var baseValue = reference.GetBase();

            if (reference.IsPropertyReference())
            {
                if (_referenceResolver != null &&
                    _referenceResolver.TryPropertyReference(this, reference, ref baseValue))
                {
                    return baseValue;
                }

                var referencedName = reference.GetReferencedName();
                if (returnReferenceToPool)
                {
                    ReferencePool.Return(reference);
                }
                if (reference.HasPrimitiveBase() == false)
                {
                    var o = TypeConverter.ToObject(this, baseValue);
                    var v = o.Get(referencedName);
                    return v;
                }
                else
                {
                    var o = TypeConverter.ToObject(this, baseValue);
                    var desc = o.GetProperty(referencedName);
                    if (desc == PropertyDescriptor.Undefined)
                    {
                        return JsValue.Undefined;
                    }

                    if (desc.IsDataDescriptor())
                    {
                        return desc.Value;
                    }

                    var getter = desc.Get;
                    if (ReferenceEquals(getter, Undefined.Instance))
                    {
                        return Undefined.Instance;
                    }

                    var callable = (ICallable)getter.AsObject();
                    return callable.Call(baseValue, Arguments.Empty);
                }
            }

            var record = (EnvironmentRecord) baseValue;
            if (ReferenceEquals(record, null))
            {
                throw new ArgumentException();
            }

            var bindingValue = record.GetBindingValue(reference.GetReferencedName(), reference.IsStrict());

            if (returnReferenceToPool)
            {
                ReferencePool.Return(reference);
            }

            return bindingValue;
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
                var record = baseValue as EnvironmentRecord;

                if (ReferenceEquals(record, null))
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
                var setter = (ICallable)desc.Set.AsObject();
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
        /// <param name="propertyName">The name of the function to call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
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

            return Invoke(value, thisObj, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="value">The function to call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public JsValue Invoke(JsValue value, params object[] arguments)
        {
            return Invoke(value, null, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="value">The function to call.</param>
        /// <param name="thisObj">The this value inside the function call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public JsValue Invoke(JsValue value, object thisObj, object[] arguments)
        {
            var callable = value.TryCast<ICallable>();

            if (callable == null)
            {
                throw new ArgumentException("Can only invoke functions");
            }

            var items = JsValueArrayPool.RentArray(arguments.Length);
            for (int i = 0; i < arguments.Length; ++i)
            {
                items[i] = JsValue.FromObject(this, arguments[i]);
            }

            var result = callable.Call(JsValue.FromObject(this, thisObj), items);
            JsValueArrayPool.ReturnArray(items);

            return result;
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
        /// Gets the last evaluated <see cref="INode"/>.
        /// </summary>
        public INode GetLastSyntaxNode()
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
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("propertyName");
            }

            var reference = ReferencePool.Rent(scope, propertyName, _isStrict);
            var jsValue = GetValue(reference);
            ReferencePool.Return(reference);

            return jsValue;
        }

        //  http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
        internal bool DeclarationBindingInstantiation(
            DeclarationBindingType declarationBindingType,
            List<FunctionDeclaration> functionDeclarations,
            List<VariableDeclaration> variableDeclarations,
            FunctionInstance functionInstance,
            JsValue[] arguments)
        {
            var env = ExecutionContext.VariableEnvironment.Record;
            bool configurableBindings = declarationBindingType == DeclarationBindingType.EvalCode;
            var strict = StrictModeScope.IsStrictModeCode;

            if (declarationBindingType == DeclarationBindingType.FunctionCode)
            {
                var parameters = functionInstance.FormalParameters;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var argName = parameters[i];
                    var v = i + 1 > arguments.Length ? Undefined.Instance : arguments[i];
                    var argAlreadyDeclared = env.HasBinding(argName);
                    if (!argAlreadyDeclared)
                    {
                        env.CreateMutableBinding(argName, v);
                    }

                    env.SetMutableBinding(argName, v, strict);
                }
            }

            var functionDeclarationsCount = functionDeclarations.Count;
            for (var i = 0; i < functionDeclarationsCount; i++)
            {
                var f = functionDeclarations[i];
                var fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f);
                var funcAlreadyDeclared = env.HasBinding(fn);
                if (!funcAlreadyDeclared)
                {
                    env.CreateMutableBinding(fn, configurableBindings);
                }
                else
                {
                    if (ReferenceEquals(env, GlobalEnvironment.Record))
                    {
                        var go = Global;
                        var existingProp = go.GetProperty(fn);
                        if (existingProp.Configurable)
                        {
                            var flags = PropertyFlag.Writable | PropertyFlag.Enumerable;
                            if (configurableBindings)
                            {
                                flags |= PropertyFlag.Configurable;
                            }

                            var descriptor = new PropertyDescriptor(Undefined.Instance, flags);
                            go.DefineOwnProperty(fn, descriptor, true);
                        }
                        else
                        {
                            if (existingProp.IsAccessorDescriptor() || !existingProp.Enumerable)
                            {
                                throw new JavaScriptException(TypeError);
                            }
                        }
                    }
                }

                env.SetMutableBinding(fn, fo, strict);
            }

            bool canReleaseArgumentsInstance = false;
            if (declarationBindingType == DeclarationBindingType.FunctionCode && !env.HasBinding("arguments"))
            {
                var argsObj = ArgumentsInstancePool.Rent(functionInstance, functionInstance.FormalParameters, arguments, env, strict);
                canReleaseArgumentsInstance = true;

                if (strict)
                {
                    var declEnv = env as DeclarativeEnvironmentRecord;

                    if (ReferenceEquals(declEnv, null))
                    {
                        throw new ArgumentException();
                    }

                    declEnv.CreateImmutableBinding("arguments", argsObj);
                }
                else
                {
                    env.CreateMutableBinding("arguments", argsObj);
                }
            }

            // process all variable declarations in the current parser scope
            var variableDeclarationsCount = variableDeclarations.Count;
            for (var i = 0; i < variableDeclarationsCount; i++)
            {
                var variableDeclaration = variableDeclarations[i];
                var declarationsCount = variableDeclaration.Declarations.Count;
                for (var j = 0; j < declarationsCount; j++)
                {
                    var d = variableDeclaration.Declarations[j];
                    var dn = ((Identifier) d.Id).Name;
                    var varAlreadyDeclared = env.HasBinding(dn);
                    if (!varAlreadyDeclared)
                    {
                        env.CreateMutableBinding(dn, Undefined.Instance);
                    }
                }
            }

            return canReleaseArgumentsInstance;
        }
    }
}