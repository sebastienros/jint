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
using Jint.Native.Iterator;
using Jint.Native.Json;
using Jint.Native.Map;
using Jint.Native.Math;
using Jint.Native.Number;
using Jint.Native.Object;
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
        private int _statementsCount;
        private long _initialMemoryUsage;
        private long _timeoutTicks;
        internal INode _lastSyntaxNode;

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
        private readonly bool _isDebugMode;
        internal readonly bool _isStrict;
        private readonly int _maxStatements;
        private readonly long _memoryLimit;
        internal readonly bool _runBeforeStatementChecks;
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
            { typeof(string), (engine, v) => (JsValue) (string)v },
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

        static Engine()
        {
            var methodInfo = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");

            if (methodInfo != null)
            {
                GetAllocatedBytesForCurrentThread =  (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), null, methodInfo);
            }
        }

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
            Iterator = IteratorConstructor.CreateIteratorConstructor(this);
            String = StringConstructor.CreateStringConstructor(this);
            RegExp = RegExpConstructor.CreateRegExpConstructor(this);
            Number = NumberConstructor.CreateNumberConstructor(this);
            Boolean = BooleanConstructor.CreateBooleanConstructor(this);
            Date = DateConstructor.CreateDateConstructor(this);
            Math = MathInstance.CreateMathObject(this);
            Json = JsonInstance.CreateJsonObject(this);

            GlobalSymbolRegistry = new GlobalSymbolRegistry();

            // Because the properties might need some of the built-in object
            // their configuration is delayed to a later step

            // this is implementation dependent, and only to pass some unit tests
            Global.Prototype = Object.PrototypeObject;
            Object.Prototype = Function.PrototypeObject;

            // create the global environment http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.3
            GlobalEnvironment = LexicalEnvironment.NewObjectEnvironment(this, Global, null, false);

            // create the global execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.1.1
            EnterExecutionContext(GlobalEnvironment, GlobalEnvironment, Global);

            Options = new Options();

            options?.Invoke(Options);

            // gather some options as fields for faster checks
            _isDebugMode = Options.IsDebugMode;
            _isStrict = Options.IsStrict;
            _maxStatements = Options._MaxStatements;
            _referenceResolver = Options.ReferenceResolver;
            _memoryLimit = Options._MemoryLimit;
            _runBeforeStatementChecks = (_maxStatements > 0 &&_maxStatements < int.MaxValue)
                                        || Options._TimeoutInterval.Ticks > 0
                                        || _memoryLimit > 0
                                        || _isDebugMode;

            _referencePool = new ReferencePool();
            _argumentsInstancePool = new ArgumentsInstancePool(this);
            _jsValueArrayPool = new JsValueArrayPool();

            Eval = new EvalFunctionInstance(this, System.ArrayExt.Empty<string>(), LexicalEnvironment.NewDeclarativeEnvironment(this, ExecutionContext.LexicalEnvironment), StrictModeScope.IsStrictModeCode);
            Global._properties[KnownKeys.Eval] = new PropertyDescriptor(Eval, true, false, true);

            if (Options._IsClrAllowed)
            {
                Global._properties["System"] = new PropertyDescriptor(new NamespaceReference(this, "System"), false, false, false);
                Global._properties["importNamespace"] = new PropertyDescriptor(new ClrFunctionInstance(
                    this,
                    "importNamespace",
                    (thisObj, arguments) => new NamespaceReference(this, TypeConverter.ToString(arguments.At(0)))), false, false, false);
            }

            ClrTypeConverter = new DefaultTypeConverter(this);
        }

        public LexicalEnvironment GlobalEnvironment { get; }
        public GlobalObject Global { get; }
        public ObjectConstructor Object { get; }
        public FunctionConstructor Function { get; }
        public ArrayConstructor Array { get; }
        public MapConstructor Map { get; }
        public SetConstructor Set { get; }
        public IteratorConstructor Iterator { get; }
        public StringConstructor String { get; }
        public RegExpConstructor RegExp { get; }
        public BooleanConstructor Boolean { get; }
        public NumberConstructor Number { get; }
        public DateConstructor Date { get; }
        public MathInstance Math { get; }
        public JsonInstance Json { get; }
        public SymbolConstructor Symbol { get; }
        public EvalFunctionInstance Eval { get; }

        public ErrorConstructor Error => _error ?? (_error = ErrorConstructor.CreateErrorConstructor(this, _errorFunctionName));
        public ErrorConstructor EvalError => _evalError ?? (_evalError = ErrorConstructor.CreateErrorConstructor(this, _evalErrorFunctionName));
        public ErrorConstructor SyntaxError => _syntaxError ?? (_syntaxError = ErrorConstructor.CreateErrorConstructor(this, _syntaxErrorFunctionName));
        public ErrorConstructor TypeError => _typeError ?? (_typeError = ErrorConstructor.CreateErrorConstructor(this, _typeErrorFunctionName));
        public ErrorConstructor RangeError => _rangeError ?? (_rangeError = ErrorConstructor.CreateErrorConstructor(this, _rangeErrorFunctionName));
        public ErrorConstructor ReferenceError => _referenceError ?? (_referenceError = ErrorConstructor.CreateErrorConstructor(this, _referenceErrorFunctionName));
        public ErrorConstructor UriError => _uriError ?? (_uriError = ErrorConstructor.CreateErrorConstructor(this, _uriErrorFunctionName));

        public ref readonly ExecutionContext ExecutionContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _executionContexts.Peek();
        }

        public GlobalSymbolRegistry GlobalSymbolRegistry { get; }

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

        private static readonly Func<long> GetAllocatedBytesForCurrentThread;

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

        public Engine SetValue(in Key name, Delegate value)
        {
            Global.FastAddProperty(name, new DelegateWrapper(this, value), true, false, true);
            return this;
        }

        public Engine SetValue(in Key name, string value)
        {
            return SetValue(name, (JsValue) value);
        }

        public Engine SetValue(in Key name, double value)
        {
            return SetValue(name, JsNumber.Create(value));
        }

        public Engine SetValue(in Key name, int value)
        {
            return SetValue(name, JsNumber.Create(value));
        }

        public Engine SetValue(in Key name, bool value)
        {
            return SetValue(name, value ? JsBoolean.True : JsBoolean.False);
        }

        public Engine SetValue(in Key name, JsValue value)
        {
            Global.Put(name, value, false);
            return this;
        }

        public Engine SetValue(in Key name, object obj)
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
            if (GetAllocatedBytesForCurrentThread != null)
            {
                _initialMemoryUsage = GetAllocatedBytesForCurrentThread();
            }
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

            if (_memoryLimit > 0)
            {
                ResetMemoryUsage();
            }

            ResetTimeoutTicks();
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

        internal void RunBeforeExecuteStatementChecks(Statement statement)
        {
            if (_maxStatements > 0 && _statementsCount++ > _maxStatements)
            {
                ExceptionHelper.ThrowStatementsCountOverflowException();
            }

            if (_timeoutTicks > 0 && _timeoutTicks < DateTime.UtcNow.Ticks)
            {
                ExceptionHelper.ThrowTimeoutException();
            }

            if (_memoryLimit > 0)
            {
                if (GetAllocatedBytesForCurrentThread != null)
                {
                    var memoryUsage = GetAllocatedBytesForCurrentThread() - _initialMemoryUsage;
                    if (memoryUsage > _memoryLimit)
                    {
                        ExceptionHelper.ThrowMemoryLimitExceededException($"Script has allocated {memoryUsage} but is limited to {_memoryLimit}");
                    }
                }
                else
                {
                    ExceptionHelper.ThrowPlatformNotSupportedException("The current platform doesn't support MemoryLimit.");
                }
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
            if (reference._baseValue._type == InternalTypes.Undefined)
            {
                if (_referenceResolver != null &&
                    _referenceResolver.TryUnresolvableReference(this, reference, out JsValue val))
                {
                    return val;
                }

                ExceptionHelper.ThrowReferenceError(this, reference);
            }

            var baseValue = reference._baseValue;

            if (reference.IsPropertyReference())
            {
                if (_referenceResolver != null &&
                    _referenceResolver.TryPropertyReference(this, reference, ref baseValue))
                {
                    return baseValue;
                }

                ref readonly var referencedName = ref reference.GetReferencedName();
                if (returnReferenceToPool)
                {
                    _referencePool.Return(reference);
                }

                if (reference._baseValue._type == InternalTypes.Object)
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

            var bindingValue = record.GetBindingValue(reference.GetReferencedName(), reference._strict);

            if (returnReferenceToPool)
            {
                _referencePool.Return(reference);
            }

            return bindingValue;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7.2
        /// </summary>
        public void PutValue(Reference reference, JsValue value)
        {
            ref readonly var referencedName = ref reference.GetReferencedName();
            if (reference._baseValue._type == InternalTypes.Undefined)
            {
                if (reference._strict)
                {
                    ExceptionHelper.ThrowReferenceError(this, reference);
                }

                Global.Put(referencedName, value, false);
            }
            else if (reference.IsPropertyReference())
            {
                var baseValue = reference._baseValue;
                if (reference._baseValue._type == InternalTypes.Object || reference._baseValue._type == InternalTypes.None)
                {
                    ((ObjectInstance) baseValue).Put(referencedName, value, reference._strict);
                }
                else
                {
                    PutPrimitiveBase(baseValue, referencedName, value, reference._strict);
                }
            }
            else
            {
                var baseValue = reference._baseValue;
                ((EnvironmentRecord) baseValue).SetMutableBinding(referencedName, value, reference._strict);
            }
        }

        /// <summary>
        /// Used by PutValue when the reference has a primitive base value
        /// </summary>
        public void PutPrimitiveBase(JsValue b, in Key name, JsValue value, bool throwOnError)
        {
            var o = TypeConverter.ToObject(this, b);
            if (!o.CanPut(name))
            {
                if (throwOnError)
                {
                    ExceptionHelper.ThrowTypeError(this);
                }
                return;
            }

            var ownDesc = o.GetOwnProperty(name);

            if (ownDesc.IsDataDescriptor())
            {
                if (throwOnError)
                {
                    ExceptionHelper.ThrowTypeError(this);
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
                    ExceptionHelper.ThrowTypeError(this);
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
        public JsValue GetValue(JsValue scope, in Key propertyName)
        {
            AssertNotNullOrEmpty(nameof(propertyName), propertyName);

            var reference = _referencePool.Rent(scope, propertyName, _isStrict);
            var jsValue = GetValue(reference, false);
            _referencePool.Return(reference);
            return jsValue;
        }

        //  http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
        internal bool DeclarationBindingInstantiation(
            DeclarationBindingType declarationBindingType,
            HoistingScope hoistingScope,
            FunctionInstance functionInstance,
            JsValue[] arguments)
        {
            var env = ExecutionContext.VariableEnvironment._record;
            bool configurableBindings = declarationBindingType == DeclarationBindingType.EvalCode;
            var strict = StrictModeScope.IsStrictModeCode;

            var der = env as DeclarativeEnvironmentRecord;
            bool canReleaseArgumentsInstance = false;
            if (declarationBindingType == DeclarationBindingType.FunctionCode)
            {
                var argsObj = _argumentsInstancePool.Rent(functionInstance, functionInstance._formalParameters, arguments, env, strict);
                canReleaseArgumentsInstance = true;

                var functionDeclaration = (functionInstance as ScriptFunctionInstance)?.FunctionDeclaration ??
                    (functionInstance as ArrowFunctionInstance)?.FunctionDeclaration;

                if (!ReferenceEquals(der, null))
                {
                    der.AddFunctionParameters(functionInstance, arguments, argsObj, functionDeclaration);
                }
                else
                {
                    // TODO: match functionality with DeclarationEnvironmentRecord.AddFunctionParameters here
                    // slow path
                    var parameters = functionInstance._formalParameters;
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        Key argName = parameters[i];
                        var v = i + 1 > arguments.Length ? Undefined.Instance : arguments[i];
                        v = DeclarativeEnvironmentRecord.HandleAssignmentPatternIfNeeded(functionDeclaration, v, i);

                        var argAlreadyDeclared = env.HasBinding(argName);
                        if (!argAlreadyDeclared)
                        {
                            env.CreateMutableBinding(argName, v);
                        }

                        env.SetMutableBinding(argName, v, strict);
                    }
                    env.CreateMutableBinding(KnownKeys.Arguments, argsObj);
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
                return canReleaseArgumentsInstance;
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
                        if (d.Id is Esprima.Ast.Identifier id1)
                        {
                            Key name = id1.Name;
                            var varAlreadyDeclared = env.HasBinding(name);
                            if (!varAlreadyDeclared)
                            {
                                env.CreateMutableBinding(name, Undefined.Instance);
                            }
                        }
                    }
                }
            }

            return canReleaseArgumentsInstance;
        }

        private void AddFunctionDeclarations(
            ref NodeList<FunctionDeclaration> functionDeclarations,
            EnvironmentRecord env,
            bool configurableBindings,
            bool strict)
        {
            var functionDeclarationsCount = functionDeclarations.Count;
            for (var i = 0; i < functionDeclarationsCount; i++)
            {
                var f = functionDeclarations[i];
                Key fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f);
                var funcAlreadyDeclared = env.HasBinding(fn);
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
                            go.DefineOwnProperty(fn, descriptor, true);
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
