using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Global;
using Jint.Native.Iterator;
using Jint.Native.Json;
using Jint.Native.Map;
using Jint.Native.Math;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Proxy;
using Jint.Native.Reflect;
using Jint.Native.Promise;
using Jint.Native.RegExp;
using Jint.Native.Set;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;
using Jint.Runtime.References;
using ExecutionContext = Jint.Runtime.Environments.ExecutionContext;

namespace Jint
{
    public class Engine
    {
        private static readonly ParserOptions DefaultParserOptions = new ParserOptions
        {
            AdaptRegexp = true,
            Tolerant = true,
            Loc = true
        };


        private static readonly JsString _errorFunctionName = new JsString("Error");
        private static readonly JsString _evalErrorFunctionName = new JsString("EvalError");
        private static readonly JsString _rangeErrorFunctionName = new JsString("RangeError");
        private static readonly JsString _referenceErrorFunctionName = new JsString("ReferenceError");
        private static readonly JsString _syntaxErrorFunctionName = new JsString("SyntaxError");
        private static readonly JsString _typeErrorFunctionName = new JsString("TypeError");
        private static readonly JsString _uriErrorFunctionName = new JsString("URIError");

        private readonly ExecutionContextStack _executionContexts;
        private JsValue _completionValue = JsValue.Undefined;
        internal INode _lastSyntaxNode;

        private readonly object _promiseContinuationsPadlock = new object();
        private readonly SemaphoreSlim _threadLock = new SemaphoreSlim(1, 1);
        private readonly Queue<Action> _promiseContinuations = new Queue<Action>();
        private bool _continuationsTaskActive = false;

        // lazy properties
        private ErrorConstructor _error;
        private ErrorConstructor _evalError;
        private ErrorConstructor _rangeError;
        private ErrorConstructor _referenceError;
        private ErrorConstructor _syntaxError;
        private ErrorConstructor _typeError;
        private ErrorConstructor _uriError;
        private DebugHandler _debugHandler;
        private List<BreakPoint> _breakPoints;

        // cached access
        private readonly List<IConstraint> _constraints;
        private readonly bool _isDebugMode;
        internal readonly bool _isStrict;
        internal readonly IReferenceResolver _referenceResolver;
        internal readonly ReferencePool _referencePool;
        internal readonly ArgumentsInstancePool _argumentsInstancePool;
        internal readonly JsValueArrayPool _jsValueArrayPool;

        public ITypeConverter ClrTypeConverter { get; set; }

        // cache of types used when resolving CLR type names
        internal readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        internal static Dictionary<Type, Func<Engine, object, JsValue>> TypeMappers = new Dictionary<Type, Func<Engine, object, JsValue>>
        {
            { typeof(bool), (engine, v) => (bool) v ? JsBoolean.True : JsBoolean.False },
            { typeof(byte), (engine, v) => JsNumber.Create((byte)v) },
            { typeof(char), (engine, v) => JsString.Create((char)v) },
            { typeof(DateTime), (engine, v) => engine.Date.Construct((DateTime)v) },
            { typeof(DateTimeOffset), (engine, v) => engine.Date.Construct((DateTimeOffset)v) },
            { typeof(decimal), (engine, v) => (JsValue) (double)(decimal)v },
            { typeof(double), (engine, v) => (JsValue)(double)v },
            { typeof(Int16), (engine, v) => JsNumber.Create((Int16)v) },
            { typeof(Int32), (engine, v) => JsNumber.Create((Int32)v) },
            { typeof(Int64), (engine, v) => (JsValue)(Int64)v },
            { typeof(SByte), (engine, v) => JsNumber.Create((SByte)v) },
            { typeof(Single), (engine, v) => (JsValue)(Single)v },
            { typeof(string), (engine, v) => JsString.Create((string) v) },
            { typeof(UInt16), (engine, v) => JsNumber.Create((UInt16)v) },
            { typeof(UInt32), (engine, v) => JsNumber.Create((UInt32)v) },
            { typeof(UInt64), (engine, v) => JsNumber.Create((UInt64)v) },
            { typeof(System.Text.RegularExpressions.Regex), (engine, v) => engine.RegExp.Construct((System.Text.RegularExpressions.Regex)v, "", engine) }
        };

        // shared frozen version
        internal readonly PropertyDescriptor _getSetThrower;

        internal readonly struct ClrPropertyDescriptorFactoriesKey : IEquatable<ClrPropertyDescriptorFactoriesKey>
        {
            public ClrPropertyDescriptorFactoriesKey(Type type, in Key propertyName)
            {
                Type = type;
                PropertyName = propertyName;
            }

            private readonly Type Type;
            private readonly Key PropertyName;

            public bool Equals(ClrPropertyDescriptorFactoriesKey other)
            {
                return Type == other.Type && PropertyName == other.PropertyName;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }
                return obj is ClrPropertyDescriptorFactoriesKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Type.GetHashCode() * 397) ^ PropertyName.GetHashCode();
                }
            }
        }

        internal readonly Dictionary<ClrPropertyDescriptorFactoriesKey, Func<Engine, object, PropertyDescriptor>> ClrPropertyDescriptorFactories =
            new Dictionary<ClrPropertyDescriptorFactoriesKey, Func<Engine, object, PropertyDescriptor>>();

        internal readonly JintCallStack CallStack = new JintCallStack();

        public Engine() : this(null)
        {
        }

        public Engine(Action<Options> options)
        {
            _executionContexts = new ExecutionContextStack(2);

            Global = GlobalObject.CreateGlobalObject(this);

            Object = ObjectConstructor.CreateObjectConstructor(this);
            Function = FunctionConstructor.CreateFunctionConstructor(this);
            _getSetThrower = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(Function.ThrowTypeError);

            Symbol = SymbolConstructor.CreateSymbolConstructor(this);
            Array = ArrayConstructor.CreateArrayConstructor(this);
            Map = MapConstructor.CreateMapConstructor(this);
            Set = SetConstructor.CreateSetConstructor(this);
            Promise = PromiseConstructor.CreatePromiseConstructor(this);
            Iterator = IteratorConstructor.CreateIteratorConstructor(this);
            String = StringConstructor.CreateStringConstructor(this);
            RegExp = RegExpConstructor.CreateRegExpConstructor(this);
            Number = NumberConstructor.CreateNumberConstructor(this);
            Boolean = BooleanConstructor.CreateBooleanConstructor(this);
            Date = DateConstructor.CreateDateConstructor(this);
            Math = MathInstance.CreateMathObject(this);
            Json = JsonInstance.CreateJsonObject(this);
            Proxy = ProxyConstructor.CreateProxyConstructor(this);
            Reflect = ReflectInstance.CreateReflectObject(this);

            GlobalSymbolRegistry = new GlobalSymbolRegistry();

            // Because the properties might need some of the built-in object
            // their configuration is delayed to a later step

            // trigger initialization
            Global.GetProperty(JsString.Empty);

            // this is implementation dependent, and only to pass some unit tests
            Global._prototype = Object.PrototypeObject;
            Object._prototype = Function.PrototypeObject;

            // create the global environment http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.3
            GlobalEnvironment = LexicalEnvironment.NewGlobalEnvironment(this, Global);

            // create the global execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.1.1
            EnterExecutionContext(GlobalEnvironment, GlobalEnvironment, Global);

            Options = new Options();

            options?.Invoke(Options);

            // gather some options as fields for faster checks
            _isDebugMode = Options.IsDebugMode;
            _isStrict = Options.IsStrict;
            _constraints = Options._Constraints;
            _referenceResolver = Options.ReferenceResolver;

            _referencePool = new ReferencePool();
            _argumentsInstancePool = new ArgumentsInstancePool(this);
            _jsValueArrayPool = new JsValueArrayPool();

            Eval = new EvalFunctionInstance(this, System.Array.Empty<string>(), LexicalEnvironment.NewDeclarativeEnvironment(this, ExecutionContext.LexicalEnvironment), StrictModeScope.IsStrictModeCode);
            Global.SetProperty(CommonProperties.Eval, new PropertyDescriptor(Eval, PropertyFlag.Configurable | PropertyFlag.Writable));

            if (Options._IsClrAllowed)
            {
                Global.SetProperty("System", new PropertyDescriptor(new NamespaceReference(this, "System"), PropertyFlag.AllForbidden));
                Global.SetProperty("importNamespace", new PropertyDescriptor(new ClrFunctionInstance(
                    this,
                    "importNamespace",
                    (thisObj, arguments) => new NamespaceReference(this, TypeConverter.ToString(arguments.At(0)))), PropertyFlag.AllForbidden));
            }

            ClrTypeConverter = new DefaultTypeConverter(this);
        }

        internal LexicalEnvironment GlobalEnvironment { get; }
        public GlobalObject Global { get; }
        public ObjectConstructor Object { get; }
        public FunctionConstructor Function { get; }
        public ArrayConstructor Array { get; }
        public MapConstructor Map { get; }
        public SetConstructor Set { get; }
        public PromiseConstructor Promise { get; }
        public IteratorConstructor Iterator { get; }
        public StringConstructor String { get; }
        public RegExpConstructor RegExp { get; }
        public BooleanConstructor Boolean { get; }
        public NumberConstructor Number { get; }
        public DateConstructor Date { get; }
        public MathInstance Math { get; }
        public JsonInstance Json { get; }
        public ProxyConstructor Proxy { get; }
        public ReflectInstance Reflect { get; }
        public SymbolConstructor Symbol { get; }
        public EvalFunctionInstance Eval { get; }

        public ErrorConstructor Error => _error ??= ErrorConstructor.CreateErrorConstructor(this, _errorFunctionName);
        public ErrorConstructor EvalError => _evalError ??= ErrorConstructor.CreateErrorConstructor(this, _evalErrorFunctionName);
        public ErrorConstructor SyntaxError => _syntaxError ??= ErrorConstructor.CreateErrorConstructor(this, _syntaxErrorFunctionName);
        public ErrorConstructor TypeError => _typeError ??= ErrorConstructor.CreateErrorConstructor(this, _typeErrorFunctionName);
        public ErrorConstructor RangeError => _rangeError ??= ErrorConstructor.CreateErrorConstructor(this, _rangeErrorFunctionName);
        public ErrorConstructor ReferenceError => _referenceError ??= ErrorConstructor.CreateErrorConstructor(this, _referenceErrorFunctionName);
        public ErrorConstructor UriError => _uriError ??= ErrorConstructor.CreateErrorConstructor(this, _uriErrorFunctionName);

        public ref readonly ExecutionContext ExecutionContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _executionContexts.Peek();
        }

        public GlobalSymbolRegistry GlobalSymbolRegistry { get; }

        internal long CurrentMemoryUsage { get; private set; }

        internal Options Options { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        #region Debugger
        public delegate StepMode DebugStepDelegate(object sender, DebugInformation e);
        public delegate StepMode BreakDelegate(object sender, DebugInformation e);
        public event DebugStepDelegate Step;
        public event BreakDelegate Break;

        internal DebugHandler DebugHandler => _debugHandler ?? (_debugHandler = new DebugHandler(this));

        public List<BreakPoint> BreakPoints => _breakPoints ?? (_breakPoints = new List<BreakPoint>());

        internal StepMode? InvokeStepEvent(DebugInformation info)
        {
            return Step?.Invoke(this, info);
        }

        internal StepMode? InvokeBreakEvent(DebugInformation info)
        {
            return Break?.Invoke(this, info);
        }
        #endregion

        public void EnterExecutionContext(
            LexicalEnvironment lexicalEnvironment,
            LexicalEnvironment variableEnvironment,
            JsValue thisBinding)
        {
            var context = new ExecutionContext(
                lexicalEnvironment,
                variableEnvironment,
                thisBinding);

            _executionContexts.Push(context);
        }

        public Engine SetValue(JsValue name, Delegate value)
        {
            Global.FastAddProperty(name, new DelegateWrapper(this, value), true, false, true);
            return this;
        }

        public Engine SetValue(JsValue name, string value)
        {
            return SetValue(name, new JsString(value));
        }

        public Engine SetValue(JsValue name, double value)
        {
            return SetValue(name, JsNumber.Create(value));
        }

        public Engine SetValue(JsValue name, int value)
        {
            return SetValue(name, JsNumber.Create(value));
        }

        public Engine SetValue(JsValue name, bool value)
        {
            return SetValue(name, value ? JsBoolean.True : JsBoolean.False);
        }

        public Engine SetValue(JsValue name, JsValue value)
        {
            Global.Set(name, value, Global);
            return this;
        }

        public Engine SetValue(JsValue name, object obj)
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
        public void ResetConstraints()
        {
            for (var i = 0; i < _constraints.Count; i++)
            {
                _constraints[i].Reset();
            }
        }

        /// <summary>
        /// Initializes list of references of called functions
        /// </summary>
        public void ResetCallStack()
        {
            CallStack.Clear();
        }

        public Engine Execute(string source) => Execute(source, true);
        public Engine Execute(string source, ParserOptions parserOptions) => Execute(source, parserOptions, true);
        public Engine Execute(Script program) => Execute(program, true);

        internal Engine Execute(string source, bool threadLock)
        {
            return Execute(source, DefaultParserOptions, threadLock);
        }

        internal Engine Execute(string source, ParserOptions parserOptions, bool threadLock)
        {
            var parser = new JavaScriptParser(source, parserOptions);
            return Execute(parser.ParseScript(), threadLock);
        }

        internal Engine Execute(Script program, bool threadLock)
        {
            if (threadLock)
            {
                _threadLock.Wait();
            }

            try
            {
                ResetConstraints();
                ResetLastStatement();
                ResetCallStack();

                using (new StrictModeScope(_isStrict || program.Strict))
                {
                    DeclarationBindingInstantiation(
                        DeclarationBindingType.GlobalCode,
                        program.HoistingScope,
                        functionInstance: null,
                        arguments: null);

                    var list = new JintStatementList(this, null, program.Body);
                    var result = list.Execute();
                    if (result.Type == CompletionType.Throw)
                    {
                        var ex = new JavaScriptException(result.GetValueOrDefault()).SetCallstack(this, result.Location);
                        throw ex;
                    }

                    _completionValue = result.GetValueOrDefault();
                }
            }
            finally
            {
                if (threadLock)
                {
                    _threadLock.Release();
                }
            }

            return this;
        }
        
        internal void QueuePromiseContinuation(Action continuation)
        {
            var startContinuationsTask = false;

            lock (_promiseContinuationsPadlock)
            {
                _promiseContinuations.Enqueue(continuation);

                if (_continuationsTaskActive == false)
                {
                    startContinuationsTask = true;
                    _continuationsTaskActive = true;
                }
            }

            if (startContinuationsTask)
            {
                Task.Factory.StartNew(async () =>
                {
                    while (true)
                    {
                        Action nextContinuation;

                        lock (_promiseContinuationsPadlock)
                        {
                            //  End task if no more continuations to process
                            if (_promiseContinuations.Count == 0)
                            {
                                _continuationsTaskActive = false;
                                return;
                            }

                            nextContinuation = _promiseContinuations.Dequeue();
                        }

                        //  Prevent a continuation using this engine concurrently with the user
                        await _threadLock.WaitAsync();

                        try
                        {
                            nextContinuation();
                        }
                        catch
                        {
                            continue;
                        }
                        finally
                        {
                            _threadLock.Release();
                        }
                    }
                });
            }
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

        /// <summary>
        /// Gets the last evaluated statement completion value.  If the completion value is a promise then the method asynchronously waits for the promise to resolve and returns the resolve result
        /// </summary>
        /// <returns></returns>
        public async Task<JsValue> GetCompletionValueAsync()
        {
            if (_completionValue is PromiseInstance promise)
                _completionValue = await promise.Task;

            return _completionValue;
        }

        /// <summary>
        /// Asynchronously waits for a completion promise to resolve.  Returns immediately if the completion value is not a promise.
        /// </summary>
        /// <returns></returns>
        public async Task WaitForCompletionAsync()
        {
            if (_completionValue is PromiseInstance promise)
                await promise.Task;
        }

        internal void RunBeforeExecuteStatementChecks(Statement statement)
        {
            // Avoid allocating the enumerator because we run this loop very often.
            for (var i = 0; i < _constraints.Count; i++)
            {
                _constraints[i].Check();
            }

            if (_isDebugMode)
            {
                DebugHandler.OnStep(statement);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.1
        /// </summary>
        public JsValue GetValue(object value)
        {
            return GetValue(value, false);
        }

        internal JsValue GetValue(object value, bool returnReferenceToPool)
        {
            if (value is JsValue jsValue)
            {
                return jsValue;
            }

            if (!(value is Reference reference))
            {
                return ((Completion) value).Value;
            }

            return GetValue(reference, returnReferenceToPool);
        }

        internal JsValue GetValue(Reference reference, bool returnReferenceToPool)
        {
            var baseValue = reference.GetBase();

            if (baseValue._type == InternalTypes.Undefined)
            {
                if (_referenceResolver != null &&
                    _referenceResolver.TryUnresolvableReference(this, reference, out JsValue val))
                {
                    return val;
                }

                ExceptionHelper.ThrowReferenceError(this, reference);
            }

            if (_referenceResolver != null
                && (baseValue._type & InternalTypes.ObjectEnvironmentRecord) == 0
                && _referenceResolver.TryPropertyReference(this, reference, ref baseValue))
            {
                    return baseValue;
                }
            
            if (reference.IsPropertyReference())
            {
                var property = reference.GetReferencedName();
                if (returnReferenceToPool)
                {
                    _referencePool.Return(reference);
                }

                if (baseValue.IsObject())
                {
                    var o = TypeConverter.ToObject(this, baseValue);
                    var v = o.Get(property);
                    return v;
                }
                else
                {
                    // check if we are accessing a string, boxing operation can be costly to do index access
                    // we have good chance to have fast path with integer or string indexer
                    ObjectInstance o = null;
                    if ((property._type & (InternalTypes.String | InternalTypes.Integer)) != 0
                        && baseValue is JsString s
                        && TryHandleStringValue(property, s, ref o, out var jsValue))
                    {
                        return jsValue;
                        }

                    if (o is null)
                        {
                        o = TypeConverter.ToObject(this, baseValue);
                    }

                    var desc = o.GetProperty(property);
                    if (desc == PropertyDescriptor.Undefined)
                    {
                        return JsValue.Undefined;
                    }

                    if (desc.IsDataDescriptor())
                    {
                        return desc.Value;
                    }

                    var getter = desc.Get;
                    if (getter.IsUndefined())
                    {
                        return Undefined.Instance;
                    }

                    var callable = (ICallable) getter.AsObject();
                    return callable.Call(baseValue, Arguments.Empty);
                }
            }

            if (!(baseValue is EnvironmentRecord record))
            {
                return ExceptionHelper.ThrowArgumentException<JsValue>();
            }

            var bindingValue = record.GetBindingValue(reference.GetReferencedName().ToString(), reference.IsStrictReference());

            if (returnReferenceToPool)
            {
                _referencePool.Return(reference);
            }

            return bindingValue;
        }

        private bool TryHandleStringValue(JsValue property, JsString s, ref ObjectInstance o, out JsValue jsValue)
        {
            if (property == CommonProperties.Length)
            {
                jsValue = JsNumber.Create((uint) s.Length);
                return true;
            }

            if (property is JsNumber number && number.IsInteger())
            {
                var index = number.AsInteger();
                var str = s._value;
                if (index < 0 || index >= str.Length)
                {
                    jsValue = JsValue.Undefined;
                    return true;
                }

                jsValue = JsString.Create(str[index]);
                return true;
            }

            if (property is JsString propertyString
                && propertyString._value.Length > 0
                && char.IsLower(propertyString._value[0]))
            {
                // trying to find property that's always in prototype
                o = String.PrototypeObject;
            }

            jsValue = JsValue.Undefined;
            return false;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/#sec-putvalue
        /// </summary>
        internal void PutValue(Reference reference, JsValue value)
        {
            var property = reference.GetReferencedName();
            var baseValue = reference.GetBase();
            if (reference.IsUnresolvableReference())
            {
                if (reference.IsStrictReference())
                {
                    ExceptionHelper.ThrowReferenceError(this, reference);
                }

                Global.Set(property, value);
            }
            else if (reference.IsPropertyReference())
            {
                if (reference.HasPrimitiveBase())
                {
                    baseValue = TypeConverter.ToObject(this, baseValue);
                }

                var thisValue = GetThisValue(reference);
                var succeeded = baseValue.Set(property, value, thisValue);
                if (!succeeded && reference.IsStrictReference())
                {
                    ExceptionHelper.ThrowTypeError(this);
                }
            }
            else
            {
                ((EnvironmentRecord) baseValue).SetMutableBinding(property.ToString(), value, reference.IsStrictReference());
            }
        }

        private static JsValue GetThisValue(Reference reference)
        {
            if (reference.IsSuperReference())
            {
                return ExceptionHelper.ThrowNotImplementedException<JsValue>();
                }

            return reference.GetBase();
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
            var callable = value as ICallable ?? ExceptionHelper.ThrowArgumentException<ICallable>("Can only invoke functions");

            var items = _jsValueArrayPool.RentArray(arguments.Length);
            for (int i = 0; i < arguments.Length; ++i)
            {
                items[i] = JsValue.FromObject(this, arguments[i]);
            }

            var result = callable.Call(JsValue.FromObject(this, thisObj), items);
            _jsValueArrayPool.ReturnArray(items);

            return result;
        }

        /// <summary>
        /// Gets a named value from the Global scope.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        public JsValue GetValue(string propertyName)
        {
            return GetValue(Global, new JsString(propertyName));
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
        /// <param name="property">The name of the property to return.</param>
        public JsValue GetValue(JsValue scope, JsValue property)
        {
            var reference = _referencePool.Rent(scope, property, _isStrict);
            var jsValue = GetValue(reference, false);
            _referencePool.Return(reference);
            return jsValue;
        }

        //  http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
        internal ArgumentsInstance DeclarationBindingInstantiation(
            DeclarationBindingType declarationBindingType,
            HoistingScope hoistingScope,
            FunctionInstance functionInstance,
            JsValue[] arguments)
        {
            var env = ExecutionContext.VariableEnvironment._record;
            bool configurableBindings = declarationBindingType == DeclarationBindingType.EvalCode;
            var strict = StrictModeScope.IsStrictModeCode;
            ArgumentsInstance argsObj = null;

            var der = env as DeclarativeEnvironmentRecord;
            if (declarationBindingType == DeclarationBindingType.FunctionCode)
            {
                // arrow functions don't needs arguments
                var arrowFunctionInstance = functionInstance as ArrowFunctionInstance;
                argsObj = arrowFunctionInstance is null
                    ? _argumentsInstancePool.Rent(functionInstance, functionInstance._formalParameters, arguments, env, strict)
                    : null;

                var functionDeclaration = (functionInstance as ScriptFunctionInstance)?.FunctionDeclaration ??
                                          arrowFunctionInstance?.FunctionDeclaration;

                if (!ReferenceEquals(der, null))
                {
                    der.AddFunctionParameters(arguments, argsObj, functionDeclaration);
                }
                else
                {
                    // TODO: match functionality with DeclarationEnvironmentRecord.AddFunctionParameters here
                    // slow path
                    var parameters = functionInstance._formalParameters;
                    for (uint i = 0; i < (uint) parameters.Length; i++)
                    {
                        var argName = parameters[i];
                        var v = i + 1 > arguments.Length ? Undefined.Instance : arguments[i];
                        v = DeclarativeEnvironmentRecord.HandleAssignmentPatternIfNeeded(functionDeclaration, v, i);

                        var argAlreadyDeclared = env.HasBinding(argName);
                        if (!argAlreadyDeclared)
                        {
                            env.CreateMutableBinding(argName, v);
                        }

                        env.SetMutableBinding(argName, v, strict);
                    }
                    env.CreateMutableBinding("arguments", argsObj);
                }
            }

            var functionDeclarations = hoistingScope.FunctionDeclarations;
            if (functionDeclarations.Count > 0)
            {
                AddFunctionDeclarations(ref functionDeclarations, env, configurableBindings, strict);
            }

            var variableDeclarations = hoistingScope.VariableDeclarations;
            if (variableDeclarations.Count == 0)
            {
                return argsObj;
            }

            // process all variable declarations in the current parser scope
            if (!ReferenceEquals(der, null))
            {
                der.AddVariableDeclarations(ref variableDeclarations);
            }
            else
            {
                // slow path
                var variableDeclarationsCount = variableDeclarations.Count;
                for (var i = 0; i < variableDeclarationsCount; i++)
                {
                    var variableDeclaration = variableDeclarations[i];
                    var declarations = variableDeclaration.Declarations;
                    var declarationsCount = declarations.Count;
                    for (var j = 0; j < declarationsCount; j++)
                    {
                        var d = declarations[j];
                        if (d.Id is Identifier id1)
                        {
                            var name = id1.Name;
                            var varAlreadyDeclared = env.HasBinding(name);
                            if (!varAlreadyDeclared)
                            {
                                env.CreateMutableBinding(name, Undefined.Instance);
                            }
                        }
                    }
                }
            }

            return argsObj;
        }

        private void AddFunctionDeclarations(
            ref NodeList<IFunctionDeclaration> functionDeclarations,
            EnvironmentRecord env,
            bool configurableBindings,
            bool strict)
        {
            var functionDeclarationsCount = functionDeclarations.Count;
            for (var i = 0; i < functionDeclarationsCount; i++)
            {
                var f = functionDeclarations[i];
                var fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f);
                var funcAlreadyDeclared = env.HasBinding(f.Id.Name);
                if (!funcAlreadyDeclared)
                {
                    env.CreateMutableBinding(fn, configurableBindings);
                }
                else
                {
                    if (ReferenceEquals(env, GlobalEnvironment._record))
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
                            go.DefinePropertyOrThrow(fn, descriptor);
                        }
                        else
                        {
                            if (existingProp.IsAccessorDescriptor() || !existingProp.Enumerable)
                            {
                                ExceptionHelper.ThrowTypeError(this);
                            }
                        }
                    }
                }

                env.SetMutableBinding(fn, fo, strict);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateLexicalEnvironment(LexicalEnvironment newEnv)
        {
            _executionContexts.ReplaceTopLexicalEnvironment(newEnv);
        }

        private static void AssertNotNullOrEmpty(string propertyName, string propertyValue)
        {
            if (string.IsNullOrEmpty(propertyValue))
            {
                ExceptionHelper.ThrowArgumentException(propertyName);
            }
        }
    }
}
