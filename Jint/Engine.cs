using System.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Generator;
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
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint;

/// <summary>
/// Engine is the main API to JavaScript interpretation. Engine instances are not thread-safe.
/// </summary>
[DebuggerTypeProxy(typeof(EngineDebugView))]
public sealed partial class Engine : IDisposable
{
    private static readonly Options _defaultEngineOptions = new();

    private readonly Parser _defaultParser;
    private ParserOptions? _defaultModuleParserOptions; // cache default ParserOptions for ModuleBuilder instances

    private readonly ExecutionContextStack _executionContexts;
    private JsValue _completionValue = JsValue.Undefined;
    internal EvaluationContext? _activeEvaluationContext;
    internal ErrorDispatchInfo? _error;

    private readonly EventLoop _eventLoop = new();

    private readonly Agent _agent = new();

    // lazy properties
    private DebugHandler? _debugger;

    // cached access
    internal readonly IObjectConverter[]? _objectConverters;
    internal readonly Constraint[] _constraints;
    internal readonly bool _isDebugMode;
    internal readonly bool _isStrict;

    private bool _customResolver;
    internal readonly IReferenceResolver _referenceResolver;

    internal readonly ReferencePool _referencePool;
    internal readonly ArgumentsInstancePool _argumentsInstancePool;
    internal readonly JsValueArrayPool _jsValueArrayPool;
    internal readonly ExtensionMethodCache _extensionMethods;

    public ITypeConverter TypeConverter { get; internal set; }

    // cache of types used when resolving CLR type names
    internal readonly Dictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    // we use registered type reference as prototype if it's known
    internal Dictionary<Type, TypeReference>? _typeReferences;

    // cache for already wrapped CLR objects to keep object identity
    internal ConditionalWeakTable<object, ObjectInstance>? _objectWrapperCache;

    internal readonly JintCallStack CallStack;
    internal readonly StackGuard _stackGuard;

    // needed in initial engine setup, for example CLR function construction
    internal Intrinsics _originalIntrinsics = null!;
    internal Host _host = null!;

    // we need to cache reflection accessors on engine level as configuration options can affect outcome
    internal readonly record struct ClrPropertyDescriptorFactoriesKey(Type Type, Key PropertyName);
    internal Dictionary<ClrPropertyDescriptorFactoriesKey, ReflectionAccessor> _reflectionAccessors = new();

    /// <summary>
    /// Constructs a new engine instance.
    /// </summary>
    public Engine() : this(null, null)
    {
    }

    /// <summary>
    /// Constructs a new engine instance and allows customizing options.
    /// </summary>
    public Engine(Action<Options>? options)
        : this(null, options != null ? (_, opts) => options.Invoke(opts) : null)
    {
    }

    /// <summary>
    /// Constructs a new engine with a custom <see cref="Options"/> instance.
    /// </summary>
    public Engine(Options options) : this(options, null)
    {
    }

    /// <summary>
    /// Constructs a new engine instance and allows customizing options.
    /// </summary>
    /// <remarks>The provided engine instance in callback is not guaranteed to be fully configured</remarks>
    public Engine(Action<Engine, Options> options) : this(null, options)
    {
    }

    private Engine(Options? options, Action<Engine, Options>? configure)
    {
        Advanced = new AdvancedOperations(this);
        Constraints = new ConstraintOperations(this);
        TypeConverter = new DefaultTypeConverter(this);

        _executionContexts = new ExecutionContextStack(2);

        // we can use default options if there's no action to modify it
        Options = options ?? (configure is not null ? new Options() : _defaultEngineOptions);

        configure?.Invoke(this, Options);

        _extensionMethods = ExtensionMethodCache.Build(Options.Interop.ExtensionMethodTypes);

        Reset();

        // gather some options as fields for faster checks
        _isDebugMode = Options.Debugger.Enabled;
        _isStrict = Options.Strict;

        _objectConverters = Options.Interop.ObjectConverters.Count > 0
            ? Options.Interop.ObjectConverters.ToArray()
            : null;

        _constraints = Options.Constraints.Constraints.ToArray();
        _referenceResolver = Options.ReferenceResolver;
        _customResolver = !ReferenceEquals(_referenceResolver, DefaultReferenceResolver.Instance);

        _referencePool = new ReferencePool();
        _argumentsInstancePool = new ArgumentsInstancePool(this);
        _jsValueArrayPool = new JsValueArrayPool();

        Options.Apply(this);

        CallStack = new JintCallStack(Options.Constraints.MaxRecursionDepth >= 0);
        _stackGuard = new StackGuard(this);

        var defaultParserOptions = ScriptParsingOptions.Default.GetParserOptions(Options);
        _defaultParser = new Parser(defaultParserOptions);
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
    internal Realm? _realmInConstruction;

    internal Node? _lastSyntaxElement;

    internal Realm Realm => _realmInConstruction ?? ExecutionContext.Realm;

    /// <summary>
    /// The well-known intrinsics for this engine instance.
    /// </summary>
    public Intrinsics Intrinsics => Realm.Intrinsics;

    /// <summary>
    /// The global object for this engine instance.
    /// </summary>
    public ObjectInstance Global => Realm.GlobalObject;

    internal GlobalSymbolRegistry GlobalSymbolRegistry { get; } = new();

    internal long CurrentMemoryUsage { get; private set; }

    internal Options Options
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }

    public DebugHandler Debugger => _debugger ??= new DebugHandler(this, Options.Debugger.InitialStepMode);

    internal ParserOptions DefaultModuleParserOptions => _defaultModuleParserOptions ??= ModuleParsingOptions.Default.GetParserOptions(Options);

    internal ParserOptions GetActiveParserOptions()
    {
        return _executionContexts?.GetActiveParserOptions() ?? _defaultParser.Options;
    }

    internal Parser GetParserFor(ScriptParsingOptions parsingOptions)
    {
        return ReferenceEquals(parsingOptions, ScriptParsingOptions.Default)
            ? _defaultParser
            : new Parser(parsingOptions.GetParserOptions(Options));
    }

    internal Parser GetParserFor(ParserOptions parserOptions)
    {
        return ReferenceEquals(parserOptions, _defaultParser.Options) ? _defaultParser : new Parser(parserOptions);
    }

    internal void EnterExecutionContext(
        Environment lexicalEnvironment,
        Environment variableEnvironment,
        Realm realm,
        PrivateEnvironment? privateEnvironment)
    {
        var context = new ExecutionContext(
            null,
            lexicalEnvironment,
            variableEnvironment,
            privateEnvironment,
            realm,
            null);

        _executionContexts.Push(context);
    }

    internal void EnterExecutionContext(in ExecutionContext context)
    {
        _executionContexts.Push(context);
    }

    /// <summary>
    /// Registers a delegate with given name. Delegate becomes a JavaScript function that can be called.
    /// </summary>
    public Engine SetValue(string name, Delegate value)
    {
        Realm.GlobalObject.FastSetProperty(name, new PropertyDescriptor(new DelegateWrapper(this, value), PropertyFlag.NonEnumerable));
        return this;
    }

    /// <summary>
    /// Registers a string value as variable.
    /// </summary>
    public Engine SetValue(string name, string? value)
    {
        return SetValue(name, value is null ? JsValue.Null : JsString.Create(value));
    }

    /// <summary>
    /// Registers a double value as variable.
    /// </summary>
    public Engine SetValue(string name, double value)
    {
        return SetValue(name, (JsValue) JsNumber.Create(value));
    }

    /// <summary>
    /// Registers an integer value as variable.
    /// </summary>
    public Engine SetValue(string name, int value)
    {
        return SetValue(name, (JsValue) JsNumber.Create(value));
    }

    /// <summary>
    /// Registers a boolean value as variable.
    /// </summary>
    public Engine SetValue(string name, bool value)
    {
        return SetValue(name, (JsValue) (value ? JsBoolean.True : JsBoolean.False));
    }

    /// <summary>
    /// Registers a native JS value as variable.
    /// </summary>
    public Engine SetValue(string name, JsValue value)
    {
        Realm.GlobalObject.Set(name, value);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue(string name, object? obj)
    {
        var value = obj is Type t
            ? TypeReference.CreateTypeReference(this, t)
            : JsValue.FromObject(this, obj);

        return SetValue(name, value);
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue(string name, [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
#pragma warning disable IL2111
        return SetValue(name, TypeReference.CreateTypeReference(this, type));
#pragma warning restore IL2111
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name, T? obj)
    {
        return obj is Type t
            ? SetValue(name, t)
            : SetValue(name, JsValue.FromObject(this, obj));
    }

    internal void LeaveExecutionContext()
    {
        _executionContexts.Pop();
    }

    internal void ResetConstraints()
    {
        foreach (var constraint in _constraints)
        {
            constraint.Reset();
        }
    }

    /// <summary>
    /// Initializes list of references of called functions
    /// </summary>
    internal void ResetCallStack()
    {
        CallStack.Clear();
    }

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(string code, string? source = null)
    {
        var script = _defaultParser.ParseScriptGuarded(Realm, code, source ?? "<anonymous>", _isStrict);
        return Evaluate(new Prepared<Script>(script, _defaultParser.Options));
    }

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(string code, ScriptParsingOptions parsingOptions)
        => Evaluate(code, "<anonymous>", parsingOptions);

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(string code, string source, ScriptParsingOptions parsingOptions)
    {
        var parser = GetParserFor(parsingOptions);
        var script = parser.ParseScriptGuarded(Realm, code, source, _isStrict);
        return Evaluate(new Prepared<Script>(script, parser.Options));
    }

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(in Prepared<Script> preparedScript)
        => Execute(preparedScript)._completionValue;

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(string code, string? source = null)
    {
        var script = _defaultParser.ParseScriptGuarded(Realm, code, source ?? "<anonymous>", _isStrict);
        return Execute(new Prepared<Script>(script, _defaultParser.Options));
    }

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(string code, ScriptParsingOptions parsingOptions)
        => Execute(code, "<anonymous>", parsingOptions);

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(string code, string source, ScriptParsingOptions parsingOptions)
    {
        var parser = GetParserFor(parsingOptions);
        var script = parser.ParseScriptGuarded(Realm, code, source, _isStrict);
        return Execute(new Prepared<Script>(script, parser.Options));
    }

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(in Prepared<Script> preparedScript)
    {
        if (!preparedScript.IsValid)
        {
            ExceptionHelper.ThrowInvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var script = preparedScript.Program;
        var parserOptions = preparedScript.ParserOptions;
        var strict = _isStrict || script.Strict;
        ExecuteWithConstraints(strict, () => ScriptEvaluation(new ScriptRecord(Realm, script, script.Location.SourceFile), parserOptions));

        return this;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-scriptevaluation
    /// </summary>
    private Engine ScriptEvaluation(ScriptRecord scriptRecord, ParserOptions parserOptions)
    {
        Debugger.OnBeforeEvaluate(scriptRecord.EcmaScriptCode);

        var globalEnv = Realm.GlobalEnv;

        var scriptContext = new ExecutionContext(
            scriptRecord,
            lexicalEnvironment: globalEnv,
            variableEnvironment: globalEnv,
            privateEnvironment: null,
            Realm,
            parserOptions: parserOptions);

        EnterExecutionContext(scriptContext);
        try
        {
            var script = scriptRecord.EcmaScriptCode;
            GlobalDeclarationInstantiation(script, globalEnv);

            var list = new JintStatementList(null, script.Body);

            Completion result;
            try
            {
                result = list.Execute(_activeEvaluationContext!);
            }
            catch
            {
                // unhandled exception
                ResetCallStack();
                throw;
            }

            if (result.Type == CompletionType.Throw)
            {
                var ex = new JavaScriptException(result.GetValueOrDefault()).SetJavaScriptCallstack(this, result.Location);
                ResetCallStack();
                throw ex;
            }

            _completionValue = result.GetValueOrDefault();

            // TODO what about callstack and thrown exceptions?
            RunAvailableContinuations();

            return this;
        }
        finally
        {
            LeaveExecutionContext();
        }
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
    internal ManualPromise RegisterPromise()
    {
        var promise = new JsPromise(this)
        {
            _prototype = Realm.Intrinsics.Promise.PrototypeObject
        };

        var (resolve, reject) = promise.CreateResolvingFunctions();


        Action<JsValue> SettleWith(Function settle) => value =>
        {
            settle.Call(JsValue.Undefined, [value]);
            RunAvailableContinuations();
        };

        return new ManualPromise(promise, SettleWith(resolve), SettleWith(reject));
    }

    internal void AddToEventLoop(Action continuation)
    {
        _eventLoop.Events.Enqueue(continuation);
    }

    internal void AddToKeptObjects(JsValue target)
    {
        _agent.AddToKeptObjects(target);
    }

    internal void RunAvailableContinuations()
    {
        var queue = _eventLoop.Events;
        DoProcessEventLoop(queue);
    }

    private static void DoProcessEventLoop(ConcurrentQueue<Action> queue)
    {
        while (queue.TryDequeue(out var nextContinuation))
        {
            // note that continuation can enqueue new events
            nextContinuation();
        }
    }

    internal void RunBeforeExecuteStatementChecks(StatementOrExpression? statement)
    {
        // Avoid allocating the enumerator because we run this loop very often.
        foreach (var constraint in _constraints)
        {
            constraint.Check();
        }

        if (_isDebugMode && statement != null && statement.Type != NodeType.BlockStatement)
        {
            Debugger.OnStep(statement);
        }
    }

    internal JsValue GetValue(object value)
    {
        return GetValue(value, false);
    }

    internal JsValue GetValue(object value, bool returnReferenceToPool)
    {
        if (value is JsValue jsValue)
        {
            return jsValue;
        }

        if (value is not Reference reference)
        {
            return ((Completion) value).Value;
        }

        return GetValue(reference, returnReferenceToPool);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getvalue
    /// </summary>
    internal JsValue GetValue(Reference reference, bool returnReferenceToPool)
    {
        var baseValue = reference.Base;

        if (baseValue.IsUndefined())
        {
            if (_customResolver)
            {
                reference.EvaluateAndCachePropertyKey();
                if (_referenceResolver.TryUnresolvableReference(this, reference, out var val))
                {
                    return val;
                }
            }

            ExceptionHelper.ThrowReferenceError(Realm, reference);
        }

        if ((baseValue._type & InternalTypes.ObjectEnvironmentRecord) == InternalTypes.Empty && _customResolver)
        {
            reference.EvaluateAndCachePropertyKey();
            if (_referenceResolver.TryPropertyReference(this, reference, ref baseValue))
            {
                return baseValue;
            }
        }

        if (reference.IsPropertyReference)
        {
            var property = reference.ReferencedName;
            if (returnReferenceToPool)
            {
                _referencePool.Return(reference);
            }

            if (baseValue.IsObject())
            {
                var baseObj = Runtime.TypeConverter.ToObject(Realm, baseValue);

                if (reference.IsPrivateReference)
                {
                    return baseObj.PrivateGet((PrivateName) reference.ReferencedName);
                }

                reference.EvaluateAndCachePropertyKey();
                var v = baseObj.Get(reference.ReferencedName, reference.ThisValue);
                return v;
            }

            // check if we are accessing a string, boxing operation can be costly to do index access
            // we have good chance to have fast path with integer or string indexer
            ObjectInstance? o = null;
            if ((property._type & (InternalTypes.String | InternalTypes.Integer)) != InternalTypes.Empty
                && baseValue is JsString s
                && TryHandleStringValue(property, s, ref o, out var jsValue))
            {
                return jsValue;
            }

            if (o is null)
            {
                o = Runtime.TypeConverter.ToObject(Realm, baseValue);
            }

            if (reference.IsPrivateReference)
            {
                return o.PrivateGet((PrivateName) reference.ReferencedName);
            }

            return o.Get(property, reference.ThisValue);
        }

        var record = (Environment) baseValue;
        var bindingValue = record.GetBindingValue(reference.ReferencedName.ToString(), reference.Strict);

        if (returnReferenceToPool)
        {
            _referencePool.Return(reference);
        }

        return bindingValue;
    }

    private bool TryHandleStringValue(JsValue property, JsString s, ref ObjectInstance? o, out JsValue jsValue)
    {
        if (CommonProperties.Length.Equals(property))
        {
            jsValue = JsNumber.Create((uint) s.Length);
            return true;
        }

        if (property is JsNumber number && number.IsInteger())
        {
            var index = number.AsInteger();
            if (index < 0 || index >= s.Length)
            {
                jsValue = JsValue.Undefined;
                return true;
            }

            jsValue = JsString.Create(s[index]);
            return true;
        }

        if (property is JsString { Length: > 0 } propertyString && char.IsLower(propertyString[0]))
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
        var property = reference.ReferencedName;
        if (reference.IsUnresolvableReference)
        {
            if (reference.Strict && property != CommonProperties.Arguments)
            {
                ExceptionHelper.ThrowReferenceError(Realm, reference);
            }

            Realm.GlobalObject.Set(property, value, throwOnError: false);
        }
        else if (reference.IsPropertyReference)
        {
            var baseObject = Runtime.TypeConverter.ToObject(Realm, reference.Base);
            if (reference.IsPrivateReference)
            {
                baseObject.PrivateSet((PrivateName) property, value);
                return;
            }

            reference.EvaluateAndCachePropertyKey();
            var succeeded = baseObject.Set(reference.ReferencedName, value, reference.ThisValue);
            if (!succeeded && reference.Strict)
            {
                ExceptionHelper.ThrowTypeError(Realm, $"Cannot assign to read only property '{property}' of {baseObject}");
            }
        }
        else
        {
            ((Environment) reference.Base).SetMutableBinding(Runtime.TypeConverter.ToString(property), value, reference.Strict);
        }
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(string propertyName, params object?[] arguments)
    {
        return Invoke(propertyName, thisObj: null, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(string propertyName, object? thisObj, object?[] arguments)
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
    public JsValue Invoke(JsValue value, params object?[] arguments)
    {
        return Invoke(value, thisObj: null, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="value">The function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(JsValue value, object? thisObj, object?[] arguments)
    {
        var callable = value as ICallable;
        if (callable is null)
        {
            ExceptionHelper.ThrowJavaScriptException(Realm.Intrinsics.TypeError, "Can only invoke functions");
        }

        JsValue DoInvoke()
        {
            var items = _jsValueArrayPool.RentArray(arguments.Length);
            for (var i = 0; i < arguments.Length; ++i)
            {
                items[i] = JsValue.FromObject(this, arguments[i]);
            }

            // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!
            JsValue result;
            var thisObject = JsValue.FromObject(this, thisObj);
            if (callable is Function functionInstance)
            {
                var callStack = CallStack;
                callStack.Push(functionInstance, expression: null, ExecutionContext);
                try
                {
                    result = functionInstance.Call(thisObject, items);
                }
                finally
                {
                    // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
                    if (callStack.Count > 0)
                    {
                        callStack.Pop();
                    }
                }
            }
            else
            {
                result = callable.Call(thisObject, items);
            }

            _jsValueArrayPool.ReturnArray(items);
            return result;
        }

        return ExecuteWithConstraints(Options.Strict, DoInvoke);
    }

    internal T ExecuteWithConstraints<T>(bool strict, Func<T> callback)
    {
        ResetConstraints();

        var ownsContext = _activeEvaluationContext is null;
        _activeEvaluationContext ??= new EvaluationContext(this);

        try
        {
            using (new StrictModeScope(strict))
            {
                return callback();
            }
        }
        finally
        {
            if (ownsContext)
            {
                _activeEvaluationContext = null!;
            }
            ResetConstraints();
            _agent.ClearKeptObjects();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-invoke
    /// </summary>
    internal JsValue Invoke(JsValue v, JsValue p, JsCallArguments arguments)
    {
        var ownsContext = _activeEvaluationContext is null;
        _activeEvaluationContext ??= new EvaluationContext(this);
        try
        {
            var func = GetV(v, p);
            var callable = func as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine("Can only invoke functions");
            }

            return callable.Call(v, arguments);
        }
        finally
        {
            if (ownsContext)
            {
                _activeEvaluationContext = null!;
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getv
    /// </summary>
    private JsValue GetV(JsValue v, JsValue p)
    {
        var o = Runtime.TypeConverter.ToObject(Realm, v);
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
    internal Node GetLastSyntaxElement()
    {
        return _lastSyntaxElement!;
    }

    /// <summary>
    /// Gets a named value from the specified scope.
    /// </summary>
    /// <param name="scope">The scope to get the property from.</param>
    /// <param name="property">The name of the property to return.</param>
    public JsValue GetValue(JsValue scope, JsValue property)
    {
        var reference = _referencePool.Rent(scope, property, _isStrict, thisValue: null);
        var jsValue = GetValue(reference, returnReferenceToPool: false);
        _referencePool.Return(reference);
        return jsValue;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-resolvebinding
    /// </summary>
    internal Reference ResolveBinding(string name, Environment? env = null)
    {
        env ??= ExecutionContext.LexicalEnvironment;
        return GetIdentifierReference(env, name, StrictModeScope.IsStrictModeCode);
    }

    private static Reference GetIdentifierReference(Environment? env, string name, bool strict)
    {
        Key key = name;
        while (true)
        {
            if (env is null)
            {
                return new Reference(JsValue.Undefined, name, strict);
            }

            if (env.HasBinding(key))
            {
                return new Reference(env, name, strict);
            }

            env = env._outerEnv;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getnewtarget
    /// </summary>
    internal JsValue GetNewTarget(Environment? thisEnvironment = null)
    {
        // we can take as argument if caller site has already determined the value, otherwise resolve
        thisEnvironment ??= ExecutionContext.GetThisEnvironment();
        return thisEnvironment.NewTarget ?? JsValue.Undefined;
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
        GlobalEnvironment env)
    {
        var hoistingScope = script.GetHoistingScope();
        var functionDeclarations = hoistingScope._functionDeclarations;

        var functionToInitialize = new List<JintFunctionDefinition>();
        var declaredFunctionNames = new HashSet<Key>();
        var declaredVarNames = new List<Key>();

        var realm = Realm;

        if (functionDeclarations != null)
        {
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                var fn = (Key) d.Id!.Name;
                if (!declaredFunctionNames.Contains(fn))
                {
                    var fnDefinable = env.CanDeclareGlobalFunction(fn);
                    if (!fnDefinable)
                    {
                        ExceptionHelper.ThrowTypeError(realm, "Cannot declare global function " + fn);
                    }

                    declaredFunctionNames.Add(fn);
                    functionToInitialize.Add(new JintFunctionDefinition(d));
                }
            }
        }

        var varNames = script.GetVarNames(hoistingScope);
        for (var j = 0; j < varNames.Count; j++)
        {
            var vn = varNames[j];
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

        PrivateEnvironment? privateEnv = null;
        var lexNames = script.GetLexNames(hoistingScope);
        for (var i = 0; i < lexNames.Count; i++)
        {
            var declaration = lexNames[i];
            foreach (var dn in declaration.BoundNames)
            {
                if (env.HasLexicalDeclaration(dn) || env.HasRestrictedGlobalProperty(dn))
                {
                    ExceptionHelper.ThrowSyntaxError(realm, $"Identifier '{dn}' has already been declared");
                }

                if (declaration.IsConstantDeclaration)
                {
                    env.CreateImmutableBinding(dn, strict: true);
                }
                else
                {
                    env.CreateMutableBinding(dn, canBeDeleted: false);
                }
            }
        }

        // we need to go through in reverse order to handle the hoisting correctly
        for (var i = functionToInitialize.Count - 1; i > -1; i--)
        {
            var f = functionToInitialize[i];
            Key fn = f.Name!;

            if (env.HasLexicalDeclaration(fn))
            {
                ExceptionHelper.ThrowSyntaxError(realm, $"Identifier '{fn}' has already been declared");
            }

            var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, env, privateEnv);
            env.CreateGlobalFunctionBinding(fn, fo, canBeDeleted: false);
        }

        env.CreateGlobalVarBindings(declaredVarNames, canBeDeleted: false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-functiondeclarationinstantiation
    /// </summary>
    internal JsArguments? FunctionDeclarationInstantiation(
        Function function,
        JsCallArguments argumentsList)
    {
        var calleeContext = ExecutionContext;
        var func = function._functionDefinition;

        var env = (FunctionEnvironment) ExecutionContext.LexicalEnvironment;
        var strict = _isStrict || StrictModeScope.IsStrictModeCode;

        var configuration = func!.Initialize();
        var parameterNames = configuration.ParameterNames;
        var hasDuplicates = configuration.HasDuplicates;
        var simpleParameterList = configuration.IsSimpleParameterList;
        var hasParameterExpressions = configuration.HasParameterExpressions;

        var canInitializeParametersOnDeclaration = simpleParameterList && !configuration.HasDuplicates;
        var arguments = canInitializeParametersOnDeclaration ? argumentsList : null;
        env.InitializeParameters(parameterNames, hasDuplicates, arguments);

        JsArguments? ao = null;
        if (configuration.ArgumentsObjectNeeded || _isDebugMode)
        {
            if (strict || !simpleParameterList)
            {
                ao = CreateUnmappedArgumentsObject(argumentsList);
            }
            else
            {
                // NOTE: mapped argument object is only provided for non-strict functions that don't have a rest parameter,
                // any parameter default value initializers, or any destructured parameters.
                ao = CreateMappedArgumentsObject(function, parameterNames, argumentsList, env, configuration.HasRestParameter);
            }

            if (strict)
            {
                env.CreateImmutableBindingAndInitialize(KnownKeys.Arguments, strict: false, ao);
            }
            else
            {
                env.CreateMutableBindingAndInitialize(KnownKeys.Arguments, canBeDeleted: false, ao);
            }
        }

        if (!canInitializeParametersOnDeclaration)
        {
            // slower set
            env.AddFunctionParameters(_activeEvaluationContext!, func.Function, argumentsList);
        }

        // Let iteratorRecord be CreateListIteratorRecord(argumentsList).
        // If hasDuplicates is true, then
        //     Perform ? IteratorBindingInitialization for formals with iteratorRecord and undefined as arguments.
        // Else,
        //     Perform ? IteratorBindingInitialization for formals with iteratorRecord and env as arguments.

        DeclarativeEnvironment varEnv;
        if (!hasParameterExpressions)
        {
            // NOTE: Only a single lexical environment is needed for the parameters and top-level vars.
            var varsToInitialize = configuration.VarsToInitialize!;
            for (var i = 0; i < varsToInitialize.Count; i++)
            {
                var pair = varsToInitialize[i];
                env.CreateMutableBindingAndInitialize(pair.Name, canBeDeleted: false, JsValue.Undefined);
            }

            varEnv = env;
        }
        else
        {
            // NOTE: A separate Environment Record is needed to ensure that closures created by expressions
            // in the formal parameter list do not have visibility of declarations in the function body.
            varEnv = JintEnvironment.NewDeclarativeEnvironment(this, env);

            UpdateVariableEnvironment(varEnv);

            var varsToInitialize = configuration.VarsToInitialize!;
            for (var i = 0; i < varsToInitialize.Count; i++)
            {
                var pair = varsToInitialize[i];
                var initialValue = pair.InitialValue ?? env.GetBindingValue(pair.Name, strict: false);
                varEnv.CreateMutableBindingAndInitialize(pair.Name, canBeDeleted: false, initialValue);
            }
        }

        // NOTE: Annex B.3.3.1 adds additional steps at this point.
        // A https://tc39.es/ecma262/#sec-web-compat-functiondeclarationinstantiation

        DeclarativeEnvironment lexEnv;
        if (configuration.NeedsEvalContext || _isDebugMode)
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

        UpdateLexicalEnvironment(lexEnv);

        var declarations = configuration.LexicalDeclarations;
        if (declarations?.Declarations.Count > 0)
        {
            var lexicalDeclarations = declarations.Value.Declarations;
            var checkExistingKeys = (lexEnv._dictionary is not null && lexEnv._dictionary.Count > 0) || !declarations.Value.AllLexicalScoped;
            var dictionary = lexEnv._dictionary ??= new HybridDictionary<Binding>(lexicalDeclarations.Count, checkExistingKeys);
            dictionary.EnsureCapacity(dictionary.Count + lexicalDeclarations.Count);

            for (var i = 0; i < lexicalDeclarations.Count; i++)
            {
                var declaration = lexicalDeclarations[i];
                foreach (var bn in declaration.BoundNames)
                {
                    if (declaration.IsConstantDeclaration)
                    {
                        dictionary.CreateImmutableBinding(bn, strict);
                    }
                    else
                    {
                        dictionary.CreateMutableBinding(bn, canBeDeleted: false);
                    }
                }
            }

            dictionary.CheckExistingKeys = true;
        }

        if (configuration.FunctionsToInitialize != null)
        {
            var privateEnv = calleeContext.PrivateEnvironment;
            var realm = Realm;
            foreach (var f in configuration.FunctionsToInitialize)
            {
                var jintFunctionDefinition = new JintFunctionDefinition(f);
                var fn = jintFunctionDefinition.Name!;
                var fo = realm.Intrinsics.Function.InstantiateFunctionObject(jintFunctionDefinition, lexEnv, privateEnv);
                varEnv.SetMutableBinding(fn, fo, strict: false);
            }
        }

        return ao;
    }

    private JsArguments CreateMappedArgumentsObject(
        Function func,
        Key[] formals,
        JsCallArguments argumentsList,
        DeclarativeEnvironment envRec,
        bool hasRestParameter)
    {
        return _argumentsInstancePool.Rent(func, formals, argumentsList, envRec, hasRestParameter);
    }

    private JsArguments CreateUnmappedArgumentsObject(JsCallArguments argumentsList)
    {
        return _argumentsInstancePool.Rent(argumentsList);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-evaldeclarationinstantiation
    /// </summary>
    internal void EvalDeclarationInstantiation(
        Script script,
        Environment varEnv,
        Environment lexEnv,
        PrivateEnvironment? privateEnv,
        bool strict)
    {
        var hoistingScope = HoistingScope.GetProgramLevelDeclarations(script);

        var lexEnvRec = (DeclarativeEnvironment) lexEnv;
        var varEnvRec = varEnv;

        var realm = Realm;

        if (!strict && hoistingScope._variablesDeclarations != null)
        {
            if (varEnvRec is GlobalEnvironment globalEnvironmentRecord)
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
                if (thisEnvRec is not ObjectEnvironment)
                {
                    ref readonly var nodes = ref hoistingScope._variablesDeclarations;
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var variablesDeclaration = nodes[i];
                        var identifier = (Identifier) variablesDeclaration.Declarations[0].Id;
                        if (thisEnvRec!.HasBinding(identifier.Name))
                        {
                            ExceptionHelper.ThrowSyntaxError(realm);
                        }
                    }
                }

                thisLex = thisLex!._outerEnv;
            }
        }

        HashSet<PrivateIdentifier>? privateIdentifiers = null;
        var pointer = privateEnv;
        while (pointer is not null)
        {
            foreach (var name in pointer.Names)
            {
                privateIdentifiers ??= new HashSet<PrivateIdentifier>(PrivateIdentifierNameComparer._instance);
                privateIdentifiers.Add(name.Key);
            }

            pointer = pointer.OuterPrivateEnvironment;
        }

        script.AllPrivateIdentifiersValid(realm, privateIdentifiers);

        var functionDeclarations = hoistingScope._functionDeclarations;
        var functionsToInitialize = new LinkedList<JintFunctionDefinition>();
        var declaredFunctionNames = new HashSet<Key>();

        if (functionDeclarations != null)
        {
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                Key fn = d.Id!.Name;
                if (!declaredFunctionNames.Contains(fn))
                {
                    if (varEnvRec is GlobalEnvironment ger)
                    {
                        var fnDefinable = ger.CanDeclareGlobalFunction(fn);
                        if (!fnDefinable)
                        {
                            ExceptionHelper.ThrowTypeError(realm);
                        }
                    }

                    declaredFunctionNames.Add(fn);
                    functionsToInitialize.AddFirst(new JintFunctionDefinition(d));
                }
            }
        }

        var boundNames = new List<Key>();
        var declaredVarNames = new List<Key>();
        var variableDeclarations = hoistingScope._variablesDeclarations;
        var variableDeclarationsCount = variableDeclarations?.Count;
        for (var i = 0; i < variableDeclarationsCount; i++)
        {
            var variableDeclaration = variableDeclarations![i];
            boundNames.Clear();
            variableDeclaration.GetBoundNames(boundNames);
            for (var j = 0; j < boundNames.Count; j++)
            {
                var vn = boundNames[j];
                if (!declaredFunctionNames.Contains(vn))
                {
                    if (varEnvRec is GlobalEnvironment ger)
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
            var d = lexicalDeclarations![i];
            d.GetBoundNames(boundNames);
            for (var j = 0; j < boundNames.Count; j++)
            {
                Key dn = boundNames[j];
                if (d.IsConstantDeclaration())
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
            var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, lexEnv, privateEnv);
            if (varEnvRec is GlobalEnvironment ger)
            {
                ger.CreateGlobalFunctionBinding(f.Name!, fo, canBeDeleted: true);
            }
            else
            {
                Key fn = f.Name!;
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
            if (varEnvRec is GlobalEnvironment ger)
            {
                ger.CreateGlobalVarBinding(vn, canBeDeleted: true);
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
    internal void UpdateLexicalEnvironment(Environment newEnv)
    {
        _executionContexts.ReplaceTopLexicalEnvironment(newEnv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateVariableEnvironment(Environment newEnv)
    {
        _executionContexts.ReplaceTopVariableEnvironment(newEnv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdatePrivateEnvironment(PrivateEnvironment? newEnv)
    {
        _executionContexts.ReplaceTopPrivateEnvironment(newEnv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly ExecutionContext UpdateGenerator(GeneratorInstance generator)
    {
        return ref _executionContexts.ReplaceTopGenerator(generator);
    }

    /// <summary>
    /// Invokes the named callable and returns the resulting object.
    /// </summary>
    /// <param name="callableName">The name of the callable.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public JsValue Call(string callableName, params JsCallArguments arguments)
    {
        var callable = Evaluate(callableName);
        return Call(callable, arguments);
    }

    /// <summary>
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <param name="callable">The callable.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public JsValue Call(JsValue callable, params JsCallArguments arguments)
        => Call(callable, thisObject: JsValue.Undefined, arguments);

    /// <summary>
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <param name="callable">The callable.</param>
    /// <param name="thisObject">Value bound as this.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public JsValue Call(JsValue callable, JsValue thisObject, JsCallArguments arguments)
    {
        JsValue Callback()
        {
            if (!callable.IsCallable)
            {
                ExceptionHelper.ThrowArgumentException(callable + " is not callable");
            }

            return Call((ICallable) callable, thisObject, arguments, null);
        }

        return ExecuteWithConstraints(Options.Strict, Callback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue Call(ICallable callable, JsValue thisObject, JsCallArguments arguments, JintExpression? expression)
    {
        if (callable is Function functionInstance)
        {
            return Call(functionInstance, thisObject, arguments, expression);
        }

        return callable.Call(thisObject, arguments);
    }

    /// <summary>
    /// Calls the named constructor and returns the resulting object.
    /// </summary>
    /// <param name="constructorName">The name of the constructor to call.</param>
    /// <param name="arguments">The arguments of the constructor call.</param>
    /// <returns>The value returned by the constructor call.</returns>
    public ObjectInstance Construct(string constructorName, params JsCallArguments arguments)
    {
        var constructor = Evaluate(constructorName);
        return Construct(constructor, arguments);
    }

    /// <summary>
    /// Calls the constructor and returns the resulting object.
    /// </summary>
    /// <param name="constructor">The name of the constructor to call.</param>
    /// <param name="arguments">The arguments of the constructor call.</param>
    /// <returns>The value returned by the constructor call.</returns>
    public ObjectInstance Construct(JsValue constructor, params JsCallArguments arguments)
    {
        ObjectInstance Callback()
        {
            if (!constructor.IsConstructor)
            {
                ExceptionHelper.ThrowArgumentException(constructor + " is not a constructor");
            }

            return Construct(constructor, arguments, constructor, null);
        }

        return ExecuteWithConstraints(Options.Strict, Callback);
    }

    internal ObjectInstance Construct(
        JsValue constructor,
        JsCallArguments arguments,
        JsValue newTarget,
        JintExpression? expression)
    {
        if (constructor is Function functionInstance)
        {
            return Construct(functionInstance, arguments, newTarget, expression);
        }

        return ((IConstructor) constructor).Construct(arguments, newTarget);
    }

    internal JsValue Call(Function function, JsValue thisObject)
        => Call(function, thisObject, Arguments.Empty, null);

    internal JsValue Call(
        Function function,
        JsValue thisObject,
        JsCallArguments arguments,
        JintExpression? expression)
    {
        // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!

        var recursionDepth = CallStack.Push(function, expression, ExecutionContext);

        if (recursionDepth > Options.Constraints.MaxRecursionDepth)
        {
            // automatically pops the current element as it was never reached
            ExceptionHelper.ThrowRecursionDepthOverflowException(CallStack);
        }

        JsValue result;
        try
        {
            result = function.Call(thisObject, arguments);
        }
        finally
        {
            // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
            if (CallStack.Count > 0)
            {
                CallStack.Pop();
            }
        }

        return result;
    }

    private ObjectInstance Construct(
        Function function,
        JsCallArguments arguments,
        JsValue newTarget,
        JintExpression? expression)
    {
        // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!

        var recursionDepth = CallStack.Push(function, expression, ExecutionContext);

        if (recursionDepth > Options.Constraints.MaxRecursionDepth)
        {
            // automatically pops the current element as it was never reached
            ExceptionHelper.ThrowRecursionDepthOverflowException(CallStack);
        }

        ObjectInstance result;
        try
        {
            result = ((IConstructor) function).Construct(arguments, newTarget);
        }
        finally
        {
            CallStack.Pop();
        }

        return result;
    }

    internal void SignalError(ErrorDispatchInfo error)
    {
        _error = error;
    }

    internal void RegisterTypeReference(TypeReference reference)
    {
        _typeReferences ??= new Dictionary<Type, TypeReference>();
        _typeReferences[reference.ReferenceType] = reference;
    }

    internal ref readonly ExecutionContext GetExecutionContext(int fromTop)
    {
        return ref _executionContexts.Peek(fromTop);
    }

    public void Dispose()
    {
        if (_objectWrapperCache is null)
        {
            return;
        }

#if SUPPORTS_WEAK_TABLE_CLEAR
            _objectWrapperCache.Clear();
#else
        // we can expect that reflection is OK as we've been generating object wrappers already
        var clearMethod = _objectWrapperCache.GetType().GetMethod("Clear", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        clearMethod?.Invoke(_objectWrapperCache, []);
#endif
    }

    [DebuggerDisplay("Engine")]
    private sealed class EngineDebugView
    {
        private readonly Engine _engine;

        public EngineDebugView(Engine engine)
        {
            _engine = engine;
        }

        public ObjectInstance Globals => _engine.Realm.GlobalObject;
        public Options Options => _engine.Options;

        public Environment VariableEnvironment => _engine.ExecutionContext.VariableEnvironment;
        public Environment LexicalEnvironment => _engine.ExecutionContext.LexicalEnvironment;
    }
}
