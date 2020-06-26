using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        internal Node _lastSyntaxNode;

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
        internal readonly PropertyDescriptor _callerCalleeArgumentsThrowerConfigurable;
        internal readonly PropertyDescriptor _callerCalleeArgumentsThrowerNonConfigurable;

        internal readonly struct ClrPropertyDescriptorFactoriesKey : IEquatable<ClrPropertyDescriptorFactoriesKey>
        {
            public ClrPropertyDescriptorFactoriesKey(Type type, Key propertyName)
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

        /// <summary>
        /// Constructs a new engine instance.
        /// </summary>
        public Engine() : this((Action<Options>) null)
        {
        }

        /// <summary>
        /// Constructs a new engine instance and allows customizing options.
        /// </summary>
        public Engine(Action<Options> options)
            : this((engine, opts) => options?.Invoke(opts))
        {
        }

        /// <summary>
        /// Constructs a new engine instance and allows customizing options.
        /// </summary>
        /// <remarks>The provided engine instance in callback is not guaranteed to be fully configured</remarks>
        public Engine(Action<Engine, Options> options)
        {
            _executionContexts = new ExecutionContextStack(2);

            Global = GlobalObject.CreateGlobalObject(this);

            Object = ObjectConstructor.CreateObjectConstructor(this);
            Function = FunctionConstructor.CreateFunctionConstructor(this);
            _callerCalleeArgumentsThrowerConfigurable = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(this,  PropertyFlag.Configurable | PropertyFlag.CustomJsValue, "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions or the arguments objects for calls to them");
            _callerCalleeArgumentsThrowerNonConfigurable = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(this, PropertyFlag.CustomJsValue, "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions or the arguments objects for calls to them");

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
            EnterExecutionContext(GlobalEnvironment, GlobalEnvironment);

            Eval = new EvalFunctionInstance(this);
            Global.SetProperty(CommonProperties.Eval, new PropertyDescriptor(Eval, PropertyFlag.Configurable | PropertyFlag.Writable));

            Options = new Options();

            options?.Invoke(this, Options);

            // gather some options as fields for faster checks
            _isDebugMode = Options.IsDebugMode;
            _isStrict = Options.IsStrict;
            _constraints = Options._Constraints;
            _referenceResolver = Options.ReferenceResolver;

            _referencePool = new ReferencePool();
            _argumentsInstancePool = new ArgumentsInstancePool(this);
            _jsValueArrayPool = new JsValueArrayPool();

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

        internal DebugHandler DebugHandler => _debugHandler ??= new DebugHandler(this);

        public List<BreakPoint> BreakPoints => _breakPoints ??= new List<BreakPoint>();

        internal StepMode? InvokeStepEvent(DebugInformation info)
        {
            return Step?.Invoke(this, info);
        }

        internal StepMode? InvokeBreakEvent(DebugInformation info)
        {
            return Break?.Invoke(this, info);
        }
        #endregion

        public ExecutionContext EnterExecutionContext(
            LexicalEnvironment lexicalEnvironment,
            LexicalEnvironment variableEnvironment)
        {
            var context = new ExecutionContext(
                lexicalEnvironment,
                variableEnvironment);

            _executionContexts.Push(context);
            return context;
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

        public Engine Execute(string source)
        {
            return Execute(source, DefaultParserOptions);
        }

        public Engine Execute(string source, ParserOptions parserOptions)
        {
            var parser = new JavaScriptParser(source, parserOptions);
            return Execute(parser.ParseScript());
        }

        public Engine Execute(Script program)
        {
            ResetConstraints();
            ResetLastStatement();
            ResetCallStack();

            using (new StrictModeScope(_isStrict || program.Strict))
            {
                GlobalDeclarationInstantiation(
                    program,
                    GlobalEnvironment);

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
                if (_referenceResolver.TryUnresolvableReference(this, reference, out JsValue val))
                {
                    return val;
                }

                ExceptionHelper.ThrowReferenceError(this, reference);
            }

            if ((baseValue._type & InternalTypes.ObjectEnvironmentRecord) == 0
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
        /// https://tc39.es/ecma262/#sec-putvalue
        /// </summary>
        internal void PutValue(Reference reference, JsValue value)
        {
            var baseValue = reference.GetBase();
            if (reference.IsUnresolvableReference())
            {
                if (reference.IsStrictReference())
                {
                    ExceptionHelper.ThrowReferenceError(this, reference);
                }

                Global.Set(reference.GetReferencedName(), value, throwOnError: false);
            }
            else if (reference.IsPropertyReference())
            {
                if (reference.HasPrimitiveBase())
                {
                    baseValue = TypeConverter.ToObject(this, baseValue);
                }

                var succeeded = baseValue.Set(reference.GetReferencedName(), value, reference.GetThisValue());
                if (!succeeded && reference.IsStrictReference())
                {
                    ExceptionHelper.ThrowTypeError(this);
                }
            }
            else
            {
                ((EnvironmentRecord) baseValue).SetMutableBinding(TypeConverter.ToString(reference.GetReferencedName()), value, reference.IsStrictReference());
            }
        }
        
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-initializereferencedbinding
        /// </summary>
        public void InitializeReferenceBinding(Reference reference, JsValue value)
        {
            var baseValue = (EnvironmentRecord) reference.GetBase();
            baseValue.InitializeBinding(TypeConverter.ToString(reference.GetReferencedName()), value);
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
        public Node GetLastSyntaxNode()
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
        
        /// <summary>
        /// https://tc39.es/ecma262/#sec-resolvebinding
        /// </summary>
        internal Reference ResolveBinding(string name, LexicalEnvironment env = null)
        {
            env ??= ExecutionContext.LexicalEnvironment;
            return GetIdentifierReference(env, name, StrictModeScope.IsStrictModeCode);
        }

        private Reference GetIdentifierReference(LexicalEnvironment lex, string name, in bool strict)
        {
            if (lex is null)
            {
                return new Reference(JsValue.Undefined, name, strict);
            }

            var envRec = lex._record;
            if (envRec.HasBinding(name))
            {
                return new Reference(envRec, name, strict);
            }

            return GetIdentifierReference(lex._outer, name, strict);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getthisenvironment
        /// </summary>
        internal EnvironmentRecord GetThisEnvironment()
        {
            // The loop will always terminate because the list of environments always
            // ends with the global environment which has a this binding.
            var lex = ExecutionContext.LexicalEnvironment;
            while (true)
            {
                var envRec = lex._record;
                var exists = envRec.HasThisBinding();
                if (exists)
                {
                    return envRec;
                }

                var outer = lex._outer;
                lex = outer;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-resolvethisbinding
        /// </summary>
        internal JsValue ResolveThisBinding()
        {
            var envRec = GetThisEnvironment();
            return envRec.GetThisBinding();
        }
        
        /// <summary>
        /// https://tc39.es/ecma262/#sec-globaldeclarationinstantiation
        /// </summary>
        private void GlobalDeclarationInstantiation(
            Script script,
            LexicalEnvironment env)
        {
            var envRec = (GlobalEnvironmentRecord) env._record;

            var hoistingScope = HoistingScope.GetProgramLevelDeclarations(script);
            var functionDeclarations = hoistingScope._functionDeclarations;
            var varDeclarations = hoistingScope._variablesDeclarations;
            var lexDeclarations = hoistingScope._lexicalDeclarations;
            
            var functionToInitialize = new LinkedList<FunctionDeclaration>();
            var declaredFunctionNames = new HashSet<string>();
            var declaredVarNames = new List<string>();

            if (functionDeclarations != null)
            {
                for (var i = functionDeclarations.Count - 1; i >= 0; i--)
                {
                    var d = functionDeclarations[i];
                    var fn = d.Id.Name;
                    if (!declaredFunctionNames.Contains(fn))
                    {
                        var fnDefinable = envRec.CanDeclareGlobalFunction(fn);
                        if (!fnDefinable)
                        {
                            ExceptionHelper.ThrowTypeError(this);
                        }

                        declaredFunctionNames.Add(fn);
                        functionToInitialize.AddFirst(d);
                    }
                }
            }

            var boundNames = new List<string>();
            if (varDeclarations != null)
            {
                for (var i = 0; i < varDeclarations.Count; i++)
                {
                    var d = varDeclarations[i];
                    boundNames.Clear();
                    d.GetBoundNames(boundNames);
                    for (var j = 0; j < boundNames.Count; j++)
                    {
                        var vn = boundNames[j];
                        if (!declaredFunctionNames.Contains(vn))
                        {
                            var vnDefinable = envRec.CanDeclareGlobalVar(vn);
                            if (!vnDefinable)
                            {
                                ExceptionHelper.ThrowTypeError(this);
                            }

                            declaredVarNames.Add(vn);
                        }
                    }
                }
            }

            if (lexDeclarations != null)
            {
                for (var i = 0; i < lexDeclarations.Count; i++)
                {
                    var d = lexDeclarations[i];
                    boundNames.Clear();
                    d.GetBoundNames(boundNames);
                    for (var j = 0; j < boundNames.Count; j++)
                    {
                        var dn = boundNames[j];
                        if (envRec.HasVarDeclaration(dn) 
                            || envRec.HasLexicalDeclaration(dn)
                            || envRec.HasRestrictedGlobalProperty(dn))
                        {
                            ExceptionHelper.ThrowSyntaxError(this, $"Identifier '{dn}' has already been declared");
                        }
                        
                        if (d.Kind == VariableDeclarationKind.Const)
                        {
                            envRec.CreateImmutableBinding(dn, strict: true);
                        }
                        else
                        {
                            envRec.CreateMutableBinding(dn, canBeDeleted: false);
                        }
                    }
                }
            }

            foreach (var f in functionToInitialize)
            {
                var fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f, env);
                envRec.CreateGlobalFunctionBinding(fn, fo, canBeDeleted: false);
            }

            for (var i = 0; i < declaredVarNames.Count; i++)
            {
                var vn = declaredVarNames[i];
                envRec.CreateGlobalVarBinding(vn, canBeDeleted: false);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-functiondeclarationinstantiation
        /// </summary>
        internal ArgumentsInstance FunctionDeclarationInstantiation(
            FunctionInstance functionInstance,
            JsValue[] argumentsList,
            LexicalEnvironment env)
        {
            var func = functionInstance._functionDefinition;

            var envRec = (FunctionEnvironmentRecord) env._record;
            var strict = StrictModeScope.IsStrictModeCode;

            var configuration = func.Initialize(this, functionInstance);
            var parameterNames = configuration.ParameterNames;
            var hasDuplicates = configuration.HasDuplicates;
            var simpleParameterList = configuration.IsSimpleParameterList;
            var hasParameterExpressions = configuration.HasParameterExpressions;

            var canInitializeParametersOnDeclaration = simpleParameterList && !configuration.HasDuplicates;
            envRec.InitializeParameters(parameterNames, hasDuplicates, canInitializeParametersOnDeclaration ? argumentsList : null);

            ArgumentsInstance ao = null;
            if (configuration.ArgumentsObjectNeeded)
            {
                if (strict || !simpleParameterList)
                {
                    ao = CreateUnmappedArgumentsObject(argumentsList);
                }
                else
                {
                    // NOTE: mapped argument object is only provided for non-strict functions that don't have a rest parameter,
                    // any parameter default value initializers, or any destructured parameters.
                    ao = CreateMappedArgumentsObject(functionInstance, parameterNames, argumentsList, envRec, configuration.HasRestParameter);
                }

                if (strict)
                {
                    envRec.CreateImmutableBindingAndInitialize(KnownKeys.Arguments, strict: false, ao);
                }
                else
                {
                    envRec.CreateMutableBindingAndInitialize(KnownKeys.Arguments, canBeDeleted: false, ao);
                }
            }

            if (!canInitializeParametersOnDeclaration)
            {
                // slower set
                envRec.AddFunctionParameters(func.Function, argumentsList);
            }
            
            // Let iteratorRecord be CreateListIteratorRecord(argumentsList).
            // If hasDuplicates is true, then
            //     Perform ? IteratorBindingInitialization for formals with iteratorRecord and undefined as arguments.
            // Else,
            //     Perform ? IteratorBindingInitialization for formals with iteratorRecord and env as arguments.

            LexicalEnvironment varEnv;
            DeclarativeEnvironmentRecord varEnvRec;
            if (!hasParameterExpressions)
            {
                // NOTE: Only a single lexical environment is needed for the parameters and top-level vars.
                for (var i = 0; i < configuration.VarsToInitialize.Count; i++)
                {
                    var pair = configuration.VarsToInitialize[i];
                    envRec.CreateMutableBindingAndInitialize(pair.Name, canBeDeleted: false, JsValue.Undefined);
                }

                varEnv = env;
                varEnvRec = envRec;
            }
            else
            {
                // NOTE: A separate Environment Record is needed to ensure that closures created by expressions
                // in the formal parameter list do not have visibility of declarations in the function body.
                varEnv = LexicalEnvironment.NewDeclarativeEnvironment(this, env);
                varEnvRec = (DeclarativeEnvironmentRecord) varEnv._record;
                
                UpdateVariableEnvironment(varEnv);

                for (var i = 0; i < configuration.VarsToInitialize.Count; i++)
                {
                    var pair = configuration.VarsToInitialize[i];
                    var initialValue = pair.InitialValue ?? envRec.GetBindingValue(pair.Name, strict: false);
                    varEnvRec.CreateMutableBindingAndInitialize(pair.Name, canBeDeleted: false, initialValue);
                }
            }
            
            // NOTE: Annex B.3.3.1 adds additional steps at this point. 
            // A https://tc39.es/ecma262/#sec-web-compat-functiondeclarationinstantiation

            LexicalEnvironment lexEnv;
            if (!strict)
            {
                lexEnv = LexicalEnvironment.NewDeclarativeEnvironment(this, varEnv);
                // NOTE: Non-strict functions use a separate lexical Environment Record for top-level lexical declarations
                // so that a direct eval can determine whether any var scoped declarations introduced by the eval code conflict
                // with pre-existing top-level lexically scoped declarations. This is not needed for strict functions
                // because a strict direct eval always places all declarations into a new Environment Record.
            }
            else
            {
                lexEnv = varEnv;
            }

            var lexEnvRec = lexEnv._record;
            
            UpdateLexicalEnvironment(lexEnv);

            if (configuration.LexicalDeclarations.Length > 0)
            {
                InitializeLexicalDeclarations(configuration.LexicalDeclarations, lexEnvRec);
            }

            if (configuration.FunctionsToInitialize != null)
            {
                InitializeFunctions(configuration.FunctionsToInitialize, lexEnv, varEnvRec);
            }

            return ao;
        }

        private void InitializeFunctions(
            LinkedList<FunctionDeclaration> functionsToInitialize,
            LexicalEnvironment lexEnv,
            DeclarativeEnvironmentRecord varEnvRec)
        {
            foreach (var f in functionsToInitialize)
            {
                var fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f, lexEnv);
                varEnvRec.SetMutableBinding(fn, fo, strict: false);
            }
        }

        private static void InitializeLexicalDeclarations(
            JintFunctionDefinition.State.LexicalVariableDeclaration[] lexicalDeclarations, 
            EnvironmentRecord lexEnvRec)
        {
            foreach (var d in lexicalDeclarations)
            {
                for (var j = 0; j < d.BoundNames.Count; j++)
                {
                    var dn = d.BoundNames[j];
                    if (d.Kind == VariableDeclarationKind.Const)
                    {
                        lexEnvRec.CreateImmutableBinding(dn, strict: true);
                    }
                    else
                    {
                        lexEnvRec.CreateMutableBinding(dn, canBeDeleted: false);
                    }
                }
            }
        }

        private ArgumentsInstance CreateMappedArgumentsObject(
            FunctionInstance func, 
            Key[] formals,
            JsValue[] argumentsList, 
            DeclarativeEnvironmentRecord envRec,
            bool hasRestParameter)
        {
            return _argumentsInstancePool.Rent(func, formals, argumentsList, envRec, hasRestParameter);
        }

        private ArgumentsInstance CreateUnmappedArgumentsObject(JsValue[] argumentsList)
        {
            return _argumentsInstancePool.Rent(argumentsList);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-evaldeclarationinstantiation
        /// </summary>
        internal void EvalDeclarationInstantiation(
            Script script,
            LexicalEnvironment varEnv,
            LexicalEnvironment lexEnv,
            bool strict)
        {
            var hoistingScope = HoistingScope.GetProgramLevelDeclarations(script);
            
            var lexEnvRec = (DeclarativeEnvironmentRecord) lexEnv._record;
            var varEnvRec = varEnv._record;

            if (!strict && hoistingScope._variablesDeclarations != null)
            {
                if (varEnvRec is GlobalEnvironmentRecord globalEnvironmentRecord)
                {
                    ref readonly var nodes = ref hoistingScope._variablesDeclarations;
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var variablesDeclaration = nodes[i];
                        var identifier = (Identifier) variablesDeclaration.Declarations[0].Id;
                        if (globalEnvironmentRecord.HasLexicalDeclaration(identifier.Name))
                        {
                            ExceptionHelper.ThrowSyntaxError(this, "Identifier '" + identifier.Name + "' has already been declared");
                        }
                    }
                }

                var thisLex = lexEnv;
                while (thisLex != varEnv)
                {
                    var thisEnvRec = thisLex._record;
                    if (!(thisEnvRec is ObjectEnvironmentRecord))
                    {
                        ref readonly var nodes = ref hoistingScope._variablesDeclarations;
                        for (var i = 0; i < nodes.Count; i++)
                        {
                            var variablesDeclaration = nodes[i];
                            var identifier = (Identifier) variablesDeclaration.Declarations[0].Id;
                            if (thisEnvRec.HasBinding(identifier.Name))
                            {
                                ExceptionHelper.ThrowSyntaxError(this);
                            }
                        }
                    }

                    thisLex = thisLex._outer;
                }
            }
            
            var functionDeclarations = hoistingScope._functionDeclarations;
            var functionsToInitialize = new LinkedList<FunctionDeclaration>();
            var declaredFunctionNames = new HashSet<string>();

            if (functionDeclarations != null)
            {
                for (var i = functionDeclarations.Count - 1; i >= 0; i--)
                {
                    var d = functionDeclarations[i];
                    var fn = d.Id.Name;
                    if (!declaredFunctionNames.Contains(fn))
                    {
                        if (varEnvRec is GlobalEnvironmentRecord ger)
                        {
                            var fnDefinable = ger.CanDeclareGlobalFunction(fn);
                            if (!fnDefinable)
                            {
                                ExceptionHelper.ThrowTypeError(this);
                            }
                        }
                        declaredFunctionNames.Add(fn);
                        functionsToInitialize.AddFirst(d);
                    }
                }
            }

            var boundNames = new List<string>();
            var declaredVarNames = new List<string>();
            var variableDeclarations = hoistingScope._variablesDeclarations;
            var variableDeclarationsCount = variableDeclarations?.Count;
            for (var i = 0; i < variableDeclarationsCount; i++)
            {
                var variableDeclaration = variableDeclarations[i];
                boundNames.Clear();
                variableDeclaration.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var vn = boundNames[j];
                    if (!declaredFunctionNames.Contains(vn))
                    {
                        if (varEnvRec is GlobalEnvironmentRecord ger)
                        {
                            var vnDefinable = ger.CanDeclareGlobalFunction(vn);
                            if (!vnDefinable)
                            {
                                ExceptionHelper.ThrowTypeError(this);
                            }
                        }

                        declaredVarNames.Add(vn);
                    }
                }
            }
            
            var lexicalDeclarations = hoistingScope._lexicalDeclarations;
            var lexicalDeclarationsCount = lexicalDeclarations?.Count;
            for (var i = 0; i < lexicalDeclarationsCount; i++)
            {
                boundNames.Clear();
                var d = lexicalDeclarations[i];
                d.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var dn = boundNames[j];
                    if (d.Kind == VariableDeclarationKind.Const)
                    {
                        lexEnvRec.CreateImmutableBinding(dn, strict: true);
                    }
                    else
                    {
                        lexEnvRec.CreateMutableBinding(dn, canBeDeleted: false);
                    }
                }
            }

            foreach (var f in functionsToInitialize)
            {
                var fn = f.Id.Name;
                var fo = Function.CreateFunctionObject(f, lexEnv);
                if (varEnvRec is GlobalEnvironmentRecord ger)
                {
                    ger.CreateGlobalFunctionBinding(fn, fo, canBeDeleted: true);
                }
                else
                {
                    var bindingExists = varEnvRec.HasBinding(fn);
                    if (!bindingExists)
                    {
                        varEnvRec.CreateMutableBinding(fn, canBeDeleted: true);
                        varEnvRec.InitializeBinding(fn, fo);
                    }
                    else
                    {
                        varEnvRec.SetMutableBinding(fn, fo, strict: false);
                    }
                }
            }

            foreach (var vn in declaredVarNames)
            {
                if (varEnvRec is GlobalEnvironmentRecord ger)
                {
                    ger.CreateGlobalVarBinding(vn, true);
                }
                else
                {
                    var bindingExists = varEnvRec.HasBinding(vn);
                    if (!bindingExists)
                    {
                        varEnvRec.CreateMutableBinding(vn, canBeDeleted: true);
                        varEnvRec.InitializeBinding(vn, JsValue.Undefined);
                    }
                }
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateLexicalEnvironment(LexicalEnvironment newEnv)
        {
            _executionContexts.ReplaceTopLexicalEnvironment(newEnv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateVariableEnvironment(LexicalEnvironment newEnv)
        {
            _executionContexts.ReplaceTopVariableEnvironment(newEnv);
        }
    }
}
