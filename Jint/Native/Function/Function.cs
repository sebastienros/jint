using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

[DebuggerDisplay("{ToString(),nq}")]
#pragma warning disable MA0049
public abstract partial class Function : ObjectInstance, ICallable
#pragma warning restore MA0049
{
    protected PropertyDescriptor? _prototypeDescriptor;

    protected internal PropertyDescriptor? _length;
    internal PropertyDescriptor? _nameDescriptor;

    // Shared sentinel marking a script function's own name/length/prototype property as
    // "exists, but its descriptor has not been materialized yet". Descriptors cannot be shared
    // across function instances (DefineOwnProperty mutates them in place), so instead of
    // allocating per instantiation — nested function declarations re-instantiate on every call
    // of their enclosing function — the sentinel defers the allocation until the property is
    // actually read. GetOwnProperty (which DefineOwnProperty and every other property protocol
    // entry point consults first) swaps it for the real descriptor on first access; a null field
    // still means "property absent/deleted". Never bumps _propertiesVersion: materialization is
    // identity-stable and function receivers are outside the version-based inline caches anyway.
    // The sentinel must never escape — its value accessors throw to make any leak loud.
    private protected static readonly PropertyDescriptor _pendingDescriptor = new PendingPropertyDescriptor();

    private sealed class PendingPropertyDescriptor : PropertyDescriptor
    {
        public PendingPropertyDescriptor() : base(PropertyFlag.CustomJsValue)
        {
        }

        protected internal override JsValue? CustomValue
        {
            get
            {
                Throw.InvalidOperationException("a pending lazy property descriptor leaked without being materialized");
                return null;
            }
            set => Throw.InvalidOperationException("a pending lazy property descriptor leaked without being materialized");
        }
    }

    internal Environment? _environment;
    internal readonly JintFunctionDefinition? _functionDefinition;
    internal readonly FunctionThisMode _thisMode;
    internal JsValue _homeObject = Undefined;
    internal ConstructorKind _constructorKind = ConstructorKind.Base;

    internal Realm _realm;
    internal PrivateEnvironment? _privateEnvironment;
    internal readonly IScriptOrModule? _scriptOrModule;

    protected Function(
        Engine engine,
        Realm realm,
        JsString? name)
        : this(engine, realm, name, FunctionThisMode.Global)
    {
    }

    internal Function(
        Engine engine,
        Realm realm,
        JintFunctionDefinition function,
        Environment env,
        FunctionThisMode thisMode)
        : this(engine, realm, name: null, thisMode)
    {
        _functionDefinition = function;
        _environment = env;
        if (function.JsName is not null)
        {
            // The own "name" property exists from birth, but its descriptor is materialized
            // lazily from the definition's cached JsName on first read (see _pendingDescriptor).
            _nameDescriptor = _pendingDescriptor;
        }
    }

    internal Function(
        Engine engine,
        Realm realm,
        JsString? name,
        FunctionThisMode thisMode = FunctionThisMode.Global)
        : base(engine, ObjectClass.Function)
    {
        if (name is not null)
        {
            _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
        }
        _realm = realm;
        _thisMode = thisMode;
        _scriptOrModule = _engine.GetActiveScriptOrModule();
    }

    // for example RavenDB wants to inspect this
    public IFunction? FunctionDeclaration => _functionDefinition?.Function;

    /// <summary>
    /// True when the function already carries a non-empty own name, materialized or pending
    /// (a pending descriptor stands for the definition's own name, which is never empty).
    /// </summary>
    internal bool HasNonEmptyOwnName
    {
        get
        {
            var nameDescriptor = _nameDescriptor;
            if (nameDescriptor is null)
            {
                return false;
            }
            return ReferenceEquals(nameDescriptor, _pendingDescriptor)
                   || !string.IsNullOrWhiteSpace(nameDescriptor._value?.ToString());
        }
    }

    internal override bool IsCallable => true;

    JsValue ICallable.Call(JsValue thisObject, params JsCallArguments arguments) => Call(thisObject, arguments);

    /// <summary>
    /// Executed when a function object is used as a function
    /// </summary>
    protected internal abstract JsValue Call(JsValue thisObject, JsCallArguments arguments);

    public bool Strict => _thisMode == FunctionThisMode.Strict;

    internal override bool IsConstructor => this is IConstructor;

    /// <summary>
    /// True for built-in constructors whose zero-argument [[Construct]] with newTarget == this
    /// runs no user-observable code and cannot raise a JavaScript error, allowing call sites to
    /// skip the call-stack frame and constructor-resolution ceremony. Must stay false whenever a
    /// user callback (e.g. a custom time system) could observe the call or throw through it.
    /// </summary>
    internal virtual bool IsZeroArgLeafConstructor => false;

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        var prototypeDescriptor = ReferenceEquals(_prototypeDescriptor, _pendingDescriptor)
            ? MaterializePrototypeDescriptor()
            : _prototypeDescriptor;
        if (prototypeDescriptor != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Prototype, prototypeDescriptor);
        }

        var length = ReferenceEquals(_length, _pendingDescriptor) ? MaterializeLengthDescriptor() : _length;
        if (length != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, length);
        }
        if (_nameDescriptor != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Name, GetOwnProperty(CommonProperties.Name));
        }

        if (this is ScriptFunction scriptFunction)
        {
            var argumentsDescriptor = ReferenceEquals(scriptFunction._argumentsDescriptor, _pendingDescriptor)
                ? scriptFunction.MaterializeArgumentsDescriptor()
                : scriptFunction._argumentsDescriptor;
            if (argumentsDescriptor is not null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Arguments, argumentsDescriptor);
            }

            var callerDescriptor = ReferenceEquals(scriptFunction._callerDescriptor, _pendingDescriptor)
                ? scriptFunction.MaterializeCallerDescriptor()
                : scriptFunction._callerDescriptor;
            if (callerDescriptor is not null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Caller, callerDescriptor);
            }
        }

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    internal sealed override IEnumerable<JsValue> GetInitialOwnStringPropertyKeys()
    {
        if (_length != null)
        {
            yield return CommonProperties.Length;
        }

        if (_nameDescriptor != null)
        {
            yield return CommonProperties.Name;
        }

        if (_prototypeDescriptor != null)
        {
            yield return CommonProperties.Prototype;
        }

        if (this is ScriptFunction scriptFunction)
        {
            if (scriptFunction._argumentsDescriptor is not null)
            {
                yield return CommonProperties.Arguments;
            }

            if (scriptFunction._callerDescriptor is not null)
            {
                yield return CommonProperties.Caller;
            }
        }
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Prototype.Equals(property))
        {
            var prototypeDescriptor = _prototypeDescriptor;
            if (ReferenceEquals(prototypeDescriptor, _pendingDescriptor))
            {
                prototypeDescriptor = MaterializePrototypeDescriptor();
            }
            return prototypeDescriptor ?? PropertyDescriptor.Undefined;
        }
        if (CommonProperties.Length.Equals(property))
        {
            var length = _length;
            if (ReferenceEquals(length, _pendingDescriptor))
            {
                length = MaterializeLengthDescriptor();
            }
            return length ?? PropertyDescriptor.Undefined;
        }
        if (CommonProperties.Name.Equals(property))
        {
            var nameDescriptor = _nameDescriptor;
            if (ReferenceEquals(nameDescriptor, _pendingDescriptor))
            {
                nameDescriptor = MaterializeNameDescriptor();
            }
            return nameDescriptor ?? PropertyDescriptor.Undefined;
        }

        if (this is ScriptFunction scriptFunction)
        {
            if (scriptFunction._argumentsDescriptor is { } argumentsDescriptor && CommonProperties.Arguments.Equals(property))
            {
                return ReferenceEquals(argumentsDescriptor, _pendingDescriptor)
                    ? scriptFunction.MaterializeArgumentsDescriptor()
                    : argumentsDescriptor;
            }
            if (scriptFunction._callerDescriptor is { } callerDescriptor && CommonProperties.Caller.Equals(property))
            {
                return ReferenceEquals(callerDescriptor, _pendingDescriptor)
                    ? scriptFunction.MaterializeCallerDescriptor()
                    : callerDescriptor;
            }
        }

        return base.GetOwnProperty(property);
    }

    private PropertyDescriptor MaterializeNameDescriptor()
    {
        return _nameDescriptor = new PropertyDescriptor(_functionDefinition!.JsName!, PropertyFlag.Configurable);
    }

    private PropertyDescriptor MaterializeLengthDescriptor()
    {
        return _length = new PropertyDescriptor(JsNumber.Create(_functionDefinition!.Initialize().Length), PropertyFlag.Configurable);
    }

    private protected PropertyDescriptor MaterializePrototypeDescriptor()
    {
        // Flags match the eager MakeConstructor path: writable, non-enumerable, non-configurable.
        // The pending marker is only ever installed for writableProperty: true (the only caller shape).
        return _prototypeDescriptor = new PropertyDescriptor(
            CreateConstructorPrototype(),
            PropertyFlag.Writable | PropertyFlag.WritableSet | PropertyFlag.EnumerableSet | PropertyFlag.ConfigurableSet);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (CommonProperties.Prototype.Equals(property))
        {
            _prototypeDescriptor = desc;
        }
        else if (CommonProperties.Length.Equals(property))
        {
            _length = desc;
        }
        else if (CommonProperties.Name.Equals(property))
        {
            _nameDescriptor = desc;
        }
        else if (this is ScriptFunction scriptFunction && scriptFunction.TrySetRestrictedOwnProperty(property, desc))
        {
        }
        else
        {
            base.SetOwnProperty(property, desc);
        }
    }

    public override void RemoveOwnProperty(JsValue property)
    {
        if (CommonProperties.Prototype.Equals(property))
        {
            _prototypeDescriptor = null;
        }
        if (CommonProperties.Length.Equals(property))
        {
            _length = null;
        }
        if (CommonProperties.Name.Equals(property))
        {
            _nameDescriptor = null;
        }
        if (this is ScriptFunction scriptFunction)
        {
            if (scriptFunction._argumentsDescriptor is not null && CommonProperties.Arguments.Equals(property))
            {
                scriptFunction._argumentsDescriptor = null;
            }
            if (scriptFunction._callerDescriptor is not null && CommonProperties.Caller.Equals(property))
            {
                scriptFunction._callerDescriptor = null;
            }
        }

        base.RemoveOwnProperty(property);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-setfunctionname
    /// </summary>
    internal void SetFunctionName(JsValue name, string? prefix = null, bool force = false)
    {
        var nameDescriptor = _nameDescriptor;
        if (!force && nameDescriptor != null
            // a pending descriptor stands for the definition's own (never empty) name
            && (ReferenceEquals(nameDescriptor, _pendingDescriptor) || UnwrapJsValue(nameDescriptor) != JsString.Empty))
        {
            return;
        }

        if (name is JsSymbol symbol)
        {
            name = symbol._value.IsUndefined()
                ? JsString.Empty
                : new JsString("[" + symbol._value + "]");
        }
        else if (name is PrivateName privateName)
        {
            name = "#" + privateName.Description;
        }

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            name = prefix + " " + name;
        }

        _nameDescriptor = new PropertyDescriptor(name, PropertyFlag.Configurable);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarycreatefromconstructor
    /// </summary>
    /// <remarks>
    /// Uses separate builder to get correct type with state support to prevent allocations.
    /// In spec intrinsicDefaultProto is string pointing to intrinsic, but we do a selector.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T OrdinaryCreateFromConstructor<T, TState>(
        JsValue constructor,
        Func<Intrinsics, ObjectInstance> intrinsicDefaultProto,
        Func<Engine, Realm, TState?, T> objectCreator,
        TState? state = default) where T : ObjectInstance
    {
        var proto = GetPrototypeFromConstructor(constructor, intrinsicDefaultProto);

        var obj = objectCreator(_engine, _realm, state);
        obj._prototype = proto;
        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getprototypefromconstructor
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ObjectInstance GetPrototypeFromConstructor(JsValue constructor, Func<Intrinsics, ObjectInstance> intrinsicDefaultProto)
    {
        if (constructor.Get(CommonProperties.Prototype) is not ObjectInstance proto)
        {
            var realm = GetFunctionRealm(constructor);
            proto = intrinsicDefaultProto(realm.Intrinsics);
        }
        return proto;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getfunctionrealm
    /// </summary>
    internal Realm GetFunctionRealm(JsValue obj)
    {
        if (obj is Function functionInstance && functionInstance._realm is not null)
        {
            return functionInstance._realm;
        }

        if (obj is BindFunction bindFunctionInstance)
        {
            return GetFunctionRealm(bindFunctionInstance.BoundTargetFunction);
        }

        if (obj is JsProxy proxyInstance)
        {
            if (proxyInstance.IsRevoked)
            {
                Throw.TypeErrorNoEngine();
            }

            return GetFunctionRealm(proxyInstance._target);
        }

        return _engine.ExecutionContext.Realm;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-makemethod
    /// </summary>
    internal void MakeMethod(ObjectInstance homeObject)
    {
        _homeObject = homeObject;
        // Per ECMAScript spec, methods must not have own "arguments" or "caller" properties.
        // Use the cached JsStrings so this allocates nothing.
        RemoveOwnProperty(CommonProperties.Arguments);
        RemoveOwnProperty(CommonProperties.Caller);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarycallbindthis
    /// </summary>
    internal void OrdinaryCallBindThis(in ExecutionContext calleeContext, JsValue thisArgument)
    {
        if (_thisMode == FunctionThisMode.Lexical)
        {
            return;
        }

        var calleeRealm = _realm;

        var localEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;
        JsValue thisValue;
        if (_thisMode == FunctionThisMode.Strict)
        {
            thisValue = thisArgument;
        }
        else
        {
            if (thisArgument is null || thisArgument.IsNullOrUndefined())
            {
                var globalEnv = calleeRealm.GlobalEnv;
                thisValue = globalEnv.GlobalThisValue;
            }
            else
            {
                thisValue = TypeConverter.ToObject(calleeRealm, thisArgument);
            }
        }

        localEnv.BindThisValue(thisValue);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-prepareforordinarycall
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly ExecutionContext PrepareForOrdinaryCall(JsValue newTarget, JintFunctionDefinition.State state, bool strict)
    {
        var localEnv = JintEnvironment.NewFunctionEnvironment(_engine, this, newTarget, state);
        var calleeRealm = _realm;

        var calleeContext = new ExecutionContext(
            _scriptOrModule,
            lexicalEnvironment: localEnv,
            variableEnvironment: localEnv,
            _privateEnvironment,
            calleeRealm,
            generator: null,
            function: this,
            strict: strict);

        // If callerContext is not already suspended, suspend callerContext.
        // Push calleeContext onto the execution context stack; calleeContext is now the running execution context.
        // NOTE: Any exception objects produced after this point are associated with calleeRealm.
        // Return calleeContext.

        _engine.EnterExecutionContext(calleeContext);
        return ref _engine.ExecutionContext;
    }

    internal void MakeConstructor(bool writableProperty = true, ObjectInstance? prototype = null)
    {
        _constructorKind = ConstructorKind.Base;
        if (prototype is null && writableProperty)
        {
            // Lazily create both the .prototype descriptor and its object. Functions that are never
            // used as a constructor and whose .prototype is never read (e.g. the hundreds of helper
            // functions declared by linq-js, or nested declarations re-instantiated per call) then
            // skip the descriptor + ObjectInstanceWithConstructor allocations entirely. GetOwnProperty
            // materializes on first access (see _pendingDescriptor), so .prototype identity is stable.
            _prototypeDescriptor = _pendingDescriptor;
        }
        else if (prototype is null)
        {
            // no caller today combines writableProperty: false with a lazily created prototype
            _prototypeDescriptor = new PropertyDescriptor(
                CreateConstructorPrototype(),
                PropertyFlag.WritableSet | PropertyFlag.EnumerableSet | PropertyFlag.ConfigurableSet);
        }
        else
        {
            _prototypeDescriptor = new PropertyDescriptor(prototype, writableProperty, enumerable: false, configurable: false);
        }
    }

    private ObjectInstanceWithConstructor CreateConstructorPrototype()
    {
        return new ObjectInstanceWithConstructor(_engine, this)
        {
            _prototype = _realm.Intrinsics.Object.PrototypeObject
        };
    }

    internal void SetFunctionLength(JsNumber length)
    {
        DefinePropertyOrThrow(CommonProperties.Length, new PropertyDescriptor(length, writable: false, enumerable: false, configurable: true));
    }

    // native syntax doesn't expect to have private identifier indicator
    private static readonly char[] _functionNameTrimStartChars = ['#'];

    public sealed override object ToObject()
    {
        return (JsCallDelegate) Call;
    }

    public override string ToString()
    {
        if (_functionDefinition is not null)
        {
            var sourceTextNode = (Node) _functionDefinition.SourceTextNode;
            if (_engine.Options.Host.FunctionToStringHandler(this, sourceTextNode) is { } s)
            {
                return s;
            }

            var state = _functionDefinition.Initialize();
            if (state.SourceText.GetValue(sourceTextNode) is { } sourceText)
            {
                return sourceText;
            }
        }

        var nameDescriptor = _nameDescriptor;
        JsValue nameValue;
        if (nameDescriptor is null)
        {
            nameValue = JsString.Empty;
        }
        else if (ReferenceEquals(nameDescriptor, _pendingDescriptor))
        {
            nameValue = _functionDefinition!.JsName!;
        }
        else
        {
            nameValue = UnwrapJsValue(nameDescriptor);
        }

        var name = "";
        if (!nameValue.IsUndefined())
        {
            name = TypeConverter.ToString(nameValue);
        }

        name = name.TrimStart(_functionNameTrimStartChars);

        return $"function {name}() {{ [native code] }}";
    }

    private sealed class ObjectInstanceWithConstructor : ObjectInstance
    {
        private PropertyDescriptor? _constructor;

        public ObjectInstanceWithConstructor(Engine engine, ObjectInstance thisObj) : base(engine)
        {
            _constructor = new PropertyDescriptor(thisObj, PropertyFlag.NonEnumerable);
        }

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            if (_constructor != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Constructor, _constructor);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (CommonProperties.Constructor.Equals(property))
            {
                return _constructor ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(property);
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (CommonProperties.Constructor.Equals(property))
            {
                _constructor = desc;
            }
            else
            {
                base.SetOwnProperty(property, desc);
            }
        }

        public override void RemoveOwnProperty(JsValue property)
        {
            if (CommonProperties.Constructor.Equals(property))
            {
                _constructor = null;
            }
            else
            {
                base.RemoveOwnProperty(property);
            }
        }
    }
}
