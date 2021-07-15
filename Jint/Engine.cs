using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

namespace Jint
{
    public partial class Engine
    {
        private static readonly ParserOptions DefaultParserOptions = new("<anonymous>")
        {
            AdaptRegexp = true,
            Tolerant = true
        };

        private readonly ExecutionContextStack _executionContexts;
        private JsValue _completionValue = JsValue.Undefined;
        internal Node _lastSyntaxNode;

        private readonly EventLoop _eventLoop = new();

        // lazy properties
        private DebugHandler _debugHandler;

        // cached access
        internal readonly IObjectConverter[] _objectConverters;
        private readonly IConstraint[] _constraints;
        internal readonly bool _isDebugMode;
        internal readonly bool _isStrict;
        internal readonly IReferenceResolver _referenceResolver;
        internal readonly ReferencePool _referencePool;
        internal readonly ArgumentsInstancePool _argumentsInstancePool;
        internal readonly JsValueArrayPool _jsValueArrayPool;
        internal readonly ExtensionMethodCache _extensionMethods;

        public ITypeConverter ClrTypeConverter { get; internal set; }

        // cache of types used when resolving CLR type names
        internal readonly Dictionary<string, Type> TypeCache = new();

        // shared frozen version
        internal readonly PropertyDescriptor _callerCalleeArgumentsThrowerConfigurable;
        internal readonly PropertyDescriptor _callerCalleeArgumentsThrowerNonConfigurable;

        internal static Dictionary<ClrPropertyDescriptorFactoriesKey, ReflectionAccessor> ReflectionAccessors = new();

        internal readonly JintCallStack CallStack;

        // needed in initial engine setup, for example CLR function construction
        internal Intrinsics _originalIntrinsics;
        internal Host _host;

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
        /// Constructs a new engine with a custom <see cref="Options"/> instance.
        /// </summary>
        public Engine(Options options) : this((e, o) => e.Options = options)
        {
        }

        /// <summary>
        /// Constructs a new engine instance and allows customizing options.
        /// </summary>
        /// <remarks>The provided engine instance in callback is not guaranteed to be fully configured</remarks>
        public Engine(Action<Engine, Options> options)
        {
            _executionContexts = new ExecutionContextStack(2);

            _callerCalleeArgumentsThrowerConfigurable = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(this,
                PropertyFlag.Configurable | PropertyFlag.CustomJsValue,
                "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions or the arguments objects for calls to them");
            _callerCalleeArgumentsThrowerNonConfigurable = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(this,
                PropertyFlag.CustomJsValue,
                "'caller', 'callee', and 'arguments' properties may not be accessed on strict mode functions or the arguments objects for calls to them");

            Options = new Options();
            options?.Invoke(this, Options);

            Reset();

            // gather some options as fields for faster checks
            _isDebugMode = Options.Debugger.Enabled;
            _isStrict = Options.Strict;

            _objectConverters = Options.Interop.ObjectConverters.Count > 0
                ? Options.Interop.ObjectConverters.ToArray()
                : null;

            _constraints = Options.Constraints.Constraints.ToArray();
            _referenceResolver = Options.ReferenceResolver;
            _extensionMethods = ExtensionMethodCache.Build(Options.Interop.ExtensionMethodTypes);
            CallStack = new JintCallStack(Options.Constraints.MaxRecursionDepth >= 0);

            _referencePool = new ReferencePool();
            _argumentsInstancePool = new ArgumentsInstancePool(this);
            _jsValueArrayPool = new JsValueArrayPool();

            Options.Apply(this);
        }

        private void Reset()
        {
            _host = Options.Host.Factory(this);
            _host.Initialize(this);
        }

        internal ref readonly ExecutionContext ExecutionContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _executionContexts.Peek();
        }

        // temporary state for realm so that we can easily pass it to functions while still not
        // having a proper execution context established
        internal Realm _realmInConstruction;

        public Realm Realm => _realmInConstruction ?? ExecutionContext.Realm;

        internal GlobalSymbolRegistry GlobalSymbolRegistry { get; } = new();

        internal long CurrentMemoryUsage { get; private set; }

        internal Options Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }

        public DebugHandler DebugHandler => _debugHandler ??= new DebugHandler(this);


        internal ExecutionContext EnterExecutionContext(
            EnvironmentRecord lexicalEnvironment,
            EnvironmentRecord variableEnvironment,
            Realm realm,
            PrivateEnvironmentRecord privateEnvironment)
        {
            var context = new ExecutionContext(
                lexicalEnvironment,
                variableEnvironment,
                privateEnvironment,
                realm,
                null);

            _executionContexts.Push(context);
            return context;
        }

        internal ExecutionContext EnterExecutionContext(ExecutionContext context)
        {
            _executionContexts.Push(context);
            return context;
        }

        public Engine SetValue(JsValue name, Delegate value)
        {
            Realm.GlobalObject.FastAddProperty(name, new DelegateWrapper(this, value), true, false, true);
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
            Realm.GlobalObject.Set(name, value);
            return this;
        }

        public Engine SetValue(JsValue name, object obj)
        {
            var value = obj is Type t
                ? TypeReference.CreateTypeReference(this, t)
                : JsValue.FromObject(this, obj);

            return SetValue(name, value);
        }

        internal void LeaveExecutionContext()
        {
            _executionContexts.Pop();
        }

        /// <summary>
        /// Initializes the statements count
        /// </summary>
        public void ResetConstraints()
        {
            foreach (var constraint in _constraints)
            {
                constraint.Reset();
            }
        }

        /// <summary>
        /// Initializes list of references of called functions
        /// </summary>
        public void ResetCallStack()
        {
            CallStack.Clear();
        }

        public JsValue Evaluate(string source)
            => Execute(source, DefaultParserOptions)._completionValue;

        public JsValue Evaluate(string source, ParserOptions parserOptions)
            => Execute(source, parserOptions)._completionValue;

        public JsValue Evaluate(Script script)
            => Execute(script)._completionValue;

        public Engine Execute(string source)
            => Execute(source, DefaultParserOptions);

        public Engine Execute(string source, ParserOptions parserOptions)
            => Execute(new JavaScriptParser(source, parserOptions).ParseScript());

        public Engine Execute(Script script)
        {
            ResetConstraints();
            ResetLastStatement();

            using (new StrictModeScope(_isStrict || script.Strict))
            {
                GlobalDeclarationInstantiation(
                    script,
                    Realm.GlobalEnv);

                var list = new JintStatementList(this, null, script.Body);

                Completion result;
                try
                {
                    result = list.Execute();
                }
                catch
                {
                    // unhandled exception
                    ResetCallStack();
                    throw;
                }

                if (result.Type == CompletionType.Throw)
                {
                    var ex = new JavaScriptException(result.GetValueOrDefault())
                        .SetCallstack(this, result.Location);
                    ResetCallStack();
                    throw ex;
                }

                // TODO what about callstack and thrown exceptions?
                RunAvailableContinuations(_eventLoop);

                _completionValue = result.GetValueOrDefault();
            }

            return this;
        }

        /// <summary>
        /// EXPERIMENTAL! Subject to change.
        ///
        /// Registers a promise within the currently running EventLoop (has to be called within "ExecuteWithEventLoop" call).
        /// Note that ExecuteWithEventLoop will not trigger "onFinished" callback until ALL manual promises are settled.
        ///
        /// NOTE: that resolve and reject need to be called withing the same thread as "ExecuteWithEventLoop".
        /// The API assumes that the Engine is called from a single thread.
        /// </summary>
        /// <returns>a Promise instance and functions to either resolve or reject it</returns>
        public ManualPromise RegisterPromise()
        {
            var promise = new PromiseInstance(this)
            {
                _prototype = Realm.Intrinsics.Promise.PrototypeObject
            };

            var (resolve, reject) = promise.CreateResolvingFunctions();


            Action<JsValue> SettleWith(FunctionInstance settle) => value =>
            {
                settle.Call(JsValue.Undefined, new[] {value});
                RunAvailableContinuations(_eventLoop);
            };

            return new ManualPromise(promise, SettleWith(resolve), SettleWith(reject));
        }

        internal void AddToEventLoop(Action continuation)
        {
            _eventLoop.Events.Enqueue(continuation);
        }


        private static void RunAvailableContinuations(EventLoop loop)
        {
            var queue = loop.Events;

            while (true)
            {
                if (queue.Count == 0)
                {
                    return;
                }

                var nextContinuation = queue.Dequeue();

                // note that continuation can enqueue new events
                nextContinuation();
            }
        }

        private void ResetLastStatement()
        {
            _lastSyntaxNode = null;
        }

        internal void RunBeforeExecuteStatementChecks(Statement statement)
        {
            // Avoid allocating the enumerator because we run this loop very often.
            foreach (var constraint in _constraints)
            {
                constraint.Check();
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

                ExceptionHelper.ThrowReferenceError(Realm, reference);
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
                    var o = TypeConverter.ToObject(Realm, baseValue);
                    var v = o.Get(property, reference.GetThisValue());
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
                        o = TypeConverter.ToObject(Realm, baseValue);
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

            var record = baseValue as EnvironmentRecord;
            if (record is null)
            {
                ExceptionHelper.ThrowArgumentException();
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
                o = Realm.Intrinsics.String.PrototypeObject;
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
                    ExceptionHelper.ThrowReferenceError(Realm, reference);
                }

                Realm.GlobalObject.Set(reference.GetReferencedName(), value, throwOnError: false);
            }
            else if (reference.IsPropertyReference())
            {
                if (reference.HasPrimitiveBase())
                {
                    baseValue = TypeConverter.ToObject(Realm, baseValue);
                }

                var succeeded = baseValue.Set(reference.GetReferencedName(), value, reference.GetThisValue());
                if (!succeeded && reference.IsStrictReference())
                {
                    ExceptionHelper.ThrowTypeError(Realm);
                }
            }
            else
            {
                ((EnvironmentRecord) baseValue).SetMutableBinding(TypeConverter.ToString(reference.GetReferencedName()),
                    value, reference.IsStrictReference());
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
            var callable = value as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(Realm, "Can only invoke functions");
            }

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
        /// https://tc39.es/ecma262/#sec-invoke
        /// </summary>
        internal JsValue Invoke(JsValue v, JsValue p, JsValue[] arguments)
        {
            var func = GetV(v, p);
            var callable = func as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine("Can only invoke functions");
            }
            return callable.Call(v, arguments);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getv
        /// </summary>
        internal JsValue GetV(JsValue v, JsValue p)
        {
            var o = TypeConverter.ToObject(Realm, v);
            return o.Get(p);
        }

        /// <summary>
        /// Gets a named value from the Global scope.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        public JsValue GetValue(string propertyName)
        {
            return GetValue(Realm.GlobalObject, new JsString(propertyName));
        }

        /// <summary>
        /// Gets the last evaluated <see cref="Node"/>.
        /// </summary>
        internal Node GetLastSyntaxNode()
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
            var reference = _referencePool.Rent(scope, property, _isStrict, thisValue: null);
            var jsValue = GetValue(reference, false);
            _referencePool.Return(reference);
            return jsValue;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-resolvebinding
        /// </summary>
        internal Reference ResolveBinding(string name, EnvironmentRecord env = null)
        {
            env ??= ExecutionContext.LexicalEnvironment;
            return GetIdentifierReference(env, name, StrictModeScope.IsStrictModeCode);
        }

        private Reference GetIdentifierReference(EnvironmentRecord env, string name, bool strict)
        {
            if (env is null)
            {
                return new Reference(JsValue.Undefined, name, strict);
            }

            var envRec = env;
            if (envRec.HasBinding(name))
            {
                return new Reference(envRec, name, strict);
            }

            return GetIdentifierReference(env._outerEnv, name, strict);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getnewtarget
        /// </summary>
        internal JsValue GetNewTarget(EnvironmentRecord thisEnvironment = null)
        {
            // we can take as argument if caller site has already determined the value, otherwise resolve
            thisEnvironment ??= ExecutionContext.GetThisEnvironment();
            return thisEnvironment.NewTarget;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-resolvethisbinding
        /// </summary>
        internal JsValue ResolveThisBinding()
        {
            var envRec = ExecutionContext.GetThisEnvironment();
            return envRec.GetThisBinding();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-globaldeclarationinstantiation
        /// </summary>
        private void GlobalDeclarationInstantiation(
            Script script,
            GlobalEnvironmentRecord env)
        {
            var hoistingScope = HoistingScope.GetProgramLevelDeclarations(script);
            var functionDeclarations = hoistingScope._functionDeclarations;
            var varDeclarations = hoistingScope._variablesDeclarations;
            var lexDeclarations = hoistingScope._lexicalDeclarations;

            var functionToInitialize = new LinkedList<FunctionDeclaration>();
            var declaredFunctionNames = new HashSet<string>();
            var declaredVarNames = new List<string>();

            var realm = Realm;

            if (functionDeclarations != null)
            {
                for (var i = functionDeclarations.Count - 1; i >= 0; i--)
                {
                    var d = functionDeclarations[i];
                    var fn = d.Id.Name;
                    if (!declaredFunctionNames.Contains(fn))
                    {
                        var fnDefinable = env.CanDeclareGlobalFunction(fn);
                        if (!fnDefinable)
                        {
                            ExceptionHelper.ThrowTypeError(realm);
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

                        if (env.HasLexicalDeclaration(vn))
                        {
                            ExceptionHelper.ThrowSyntaxError(realm, $"Identifier '{vn}' has already been declared");
                        }

                        if (!declaredFunctionNames.Contains(vn))
                        {
                            var vnDefinable = env.CanDeclareGlobalVar(vn);
                            if (!vnDefinable)
                            {
                                ExceptionHelper.ThrowTypeError(realm);
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
                        if (env.HasVarDeclaration(dn)
                            || env.HasLexicalDeclaration(dn)
                            || env.HasRestrictedGlobalProperty(dn))
                        {
                            ExceptionHelper.ThrowSyntaxError(realm, $"Identifier '{dn}' has already been declared");
                        }

                        if (d.Kind == VariableDeclarationKind.Const)
                        {
                            env.CreateImmutableBinding(dn, strict: true);
                        }
                        else
                        {
                            env.CreateMutableBinding(dn, canBeDeleted: false);
                        }
                    }
                }
            }

            foreach (var f in functionToInitialize)
            {
                var fn = f.Id!.Name;

                if (env.HasLexicalDeclaration(fn))
                {
                    ExceptionHelper.ThrowSyntaxError(realm, $"Identifier '{fn}' has already been declared");
                }

                var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, env);
                env.CreateGlobalFunctionBinding(fn, fo, canBeDeleted: false);
            }

            for (var i = 0; i < declaredVarNames.Count; i++)
            {
                var vn = declaredVarNames[i];
                env.CreateGlobalVarBinding(vn, canBeDeleted: false);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-functiondeclarationinstantiation
        /// </summary>
        internal ArgumentsInstance FunctionDeclarationInstantiation(
            FunctionInstance functionInstance,
            JsValue[] argumentsList,
            EnvironmentRecord env)
        {
            var func = functionInstance._functionDefinition;

            var envRec = (FunctionEnvironmentRecord) env;
            var strict = StrictModeScope.IsStrictModeCode;

            var configuration = func.Initialize(functionInstance);
            var parameterNames = configuration.ParameterNames;
            var hasDuplicates = configuration.HasDuplicates;
            var simpleParameterList = configuration.IsSimpleParameterList;
            var hasParameterExpressions = configuration.HasParameterExpressions;

            var canInitializeParametersOnDeclaration = simpleParameterList && !configuration.HasDuplicates;
            envRec.InitializeParameters(parameterNames, hasDuplicates,
                canInitializeParametersOnDeclaration ? argumentsList : null);

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
                    ao = CreateMappedArgumentsObject(functionInstance, parameterNames, argumentsList, envRec,
                        configuration.HasRestParameter);
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

            EnvironmentRecord varEnv;
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
                varEnv = JintEnvironment.NewDeclarativeEnvironment(this, env);
                varEnvRec = (DeclarativeEnvironmentRecord) varEnv;

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

            EnvironmentRecord lexEnv;
            if (!strict)
            {
                lexEnv = JintEnvironment.NewDeclarativeEnvironment(this, varEnv);
                // NOTE: Non-strict functions use a separate lexical Environment Record for top-level lexical declarations
                // so that a direct eval can determine whether any var scoped declarations introduced by the eval code conflict
                // with pre-existing top-level lexically scoped declarations. This is not needed for strict functions
                // because a strict direct eval always places all declarations into a new Environment Record.
            }
            else
            {
                lexEnv = varEnv;
            }

            var lexEnvRec = lexEnv;

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
            EnvironmentRecord lexEnv,
            DeclarativeEnvironmentRecord varEnvRec)
        {
            var realm = Realm;
            foreach (var f in functionsToInitialize)
            {
                var fn = f.Id.Name;
                var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, lexEnv);
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
            EnvironmentRecord varEnv,
            EnvironmentRecord lexEnv,
            PrivateEnvironmentRecord privateEnv,
            bool strict)
        {
            var hoistingScope = HoistingScope.GetProgramLevelDeclarations(script);

            var lexEnvRec = (DeclarativeEnvironmentRecord) lexEnv;
            var varEnvRec = varEnv;

            var realm = Realm;

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
                            ExceptionHelper.ThrowSyntaxError(realm, "Identifier '" + identifier.Name + "' has already been declared");
                        }
                    }
                }

                var thisLex = lexEnv;
                while (!ReferenceEquals(thisLex, varEnv))
                {
                    var thisEnvRec = thisLex;
                    if (!(thisEnvRec is ObjectEnvironmentRecord))
                    {
                        ref readonly var nodes = ref hoistingScope._variablesDeclarations;
                        for (var i = 0; i < nodes.Count; i++)
                        {
                            var variablesDeclaration = nodes[i];
                            var identifier = (Identifier) variablesDeclaration.Declarations[0].Id;
                            if (thisEnvRec.HasBinding(identifier.Name))
                            {
                                ExceptionHelper.ThrowSyntaxError(realm);
                            }
                        }
                    }

                    thisLex = thisLex._outerEnv;
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
                                ExceptionHelper.ThrowTypeError(realm);
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
                                ExceptionHelper.ThrowTypeError(realm);
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
                var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, lexEnv);
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
        internal void UpdateLexicalEnvironment(EnvironmentRecord newEnv)
        {
            _executionContexts.ReplaceTopLexicalEnvironment(newEnv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateVariableEnvironment(EnvironmentRecord newEnv)
        {
            _executionContexts.ReplaceTopVariableEnvironment(newEnv);
        }

        internal JsValue Call(ICallable callable, JsValue thisObject, JsValue[] arguments, JintExpression expression)
        {
            if (callable is FunctionInstance functionInstance)
            {
                return Call(functionInstance, thisObject, arguments, expression);
            }

            return callable.Call(thisObject, arguments);
        }

        internal JsValue Construct(IConstructor constructor, JsValue[] arguments, JsValue newTarget,
            JintExpression expression)
        {
            if (constructor is FunctionInstance functionInstance)
            {
                return Construct(functionInstance, arguments, newTarget, expression);
            }

            return constructor.Construct(arguments, newTarget);
        }

        internal JsValue Call(
            FunctionInstance functionInstance,
            JsValue thisObject,
            JsValue[] arguments,
            JintExpression expression)
        {
            var callStackElement = new CallStackElement(functionInstance, expression, ExecutionContext);
            var recursionDepth = CallStack.Push(callStackElement);

            if (recursionDepth > Options.Constraints.MaxRecursionDepth)
            {
                // pop the current element as it was never reached
                CallStack.Pop();
                ExceptionHelper.ThrowRecursionDepthOverflowException(CallStack, callStackElement.ToString());
            }

            var result = functionInstance.Call(thisObject, arguments);

            CallStack.Pop();

            return result;
        }

        internal JsValue Construct(
            FunctionInstance functionInstance,
            JsValue[] arguments,
            JsValue newTarget,
            JintExpression expression)
        {
            var callStackElement = new CallStackElement(functionInstance, expression, ExecutionContext);
            var recursionDepth = CallStack.Push(callStackElement);

            if (recursionDepth > Options.Constraints.MaxRecursionDepth)
            {
                // pop the current element as it was never reached
                CallStack.Pop();
                ExceptionHelper.ThrowRecursionDepthOverflowException(CallStack, callStackElement.ToString());
            }

            var result = ((IConstructor) functionInstance).Construct(arguments, newTarget);

            CallStack.Pop();

            return result;
        }
    }
}