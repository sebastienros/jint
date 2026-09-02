using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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

    /// <summary>
    /// Coerces a built-in's callback argument, raising the TypeError from this function's own
    /// <c>[[Realm]]</c> rather than from whichever realm is running. See the same helper on
    /// <see cref="Prototype"/> for why it is not declared on <see cref="ObjectInstance"/>.
    /// </summary>
    private protected ICallable GetCallable(JsValue source) => source.GetCallable(_realm);

    /// <summary>
    /// Whether a CLR exception escaping host code propagates untouched, which it does exactly when the
    /// engine still carries the default interop exception handler. Resolved once per function object rather
    /// than per call: <see cref="Options"/> is frozen the moment an engine reads it, so the answer cannot
    /// change under a function that has already been built.
    /// </summary>
    private protected static bool ClrExceptionsBubble(Engine engine)
        => engine.Options.Interop.ExceptionHandler == Options.InteropOptions._defaultExceptionHandler;

    /// <summary>
    /// Offers a CLR exception that escaped host code to the configured interop exception handler: converted
    /// into a catchable JavaScript error when it accepts, rethrown with its original stack when it declines.
    /// Never returns — the <see cref="JsValue"/> return type only lets a <c>catch</c> block be an expression.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected static JsValue TranslateClrException(Engine engine, Exception e)
    {
        if (engine.Options.Interop.ExceptionHandler(e))
        {
            Throw.FromClrException(engine, e);
        }
        else
        {
            ExceptionDispatchInfo.Capture(e).Throw();
        }

        return Undefined;
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
        // every Function is an ICallable; the flags let call sites skip the interface-map scan and,
        // for Function specifically, the class-hierarchy walk that decides call-stack framing
        _type |= InternalTypes.Callable | InternalTypes.Function;
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

    internal override bool HasCall => true;

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

    /// <summary>
    /// How this function may be invoked with <paramref name="argumentCount"/> arguments. Call sites
    /// resolve this ONCE and cache it next to the callee identity, so the per-call cost is a
    /// reference compare rather than a virtual call. The default declines both lanes.
    /// </summary>
    /// <remarks>
    /// Keyed on arity because a body that distinguishes an absent argument from an explicit
    /// <c>undefined</c> is only safe to invoke positionally at the arities the implementation
    /// actually accounts for.
    /// </remarks>
    internal virtual FastCallShape GetFastCallShape(int argumentCount) => default;

    /// <summary>
    /// Arity-specialized entry point: same observable behaviour as <see cref="Call"/>, but with
    /// arguments passed directly instead of through a pooled <c>JsValue[]</c>. Absent arguments
    /// arrive as <see cref="JsValue.Undefined"/>, matching <c>Arguments.At</c>. Only valid when
    /// <see cref="GetFastCallShape"/> reported <see cref="FastCallShape.Supported"/> for the arity.
    /// </summary>
    internal virtual JsValue CallFast(JsValue thisObject, JsValue arg0, JsValue arg1)
        => throw new InvalidOperationException($"{GetType()} does not implement CallFast; GetFastCallShape must not report Supported.");

    /// <summary>
    /// The <see cref="FastCallShape.Variadic"/> counterpart of <see cref="CallFast"/>, for built-ins
    /// whose tail is a <c>[Rest]</c> parameter. The span carries the call site's real arity, so an
    /// omitted argument is genuinely absent here rather than an <see cref="JsValue.Undefined"/>
    /// standing in for one — which is what a variadic body would otherwise mistake for an extra
    /// element. Only valid when <see cref="GetFastCallShape"/> reported both
    /// <see cref="FastCallShape.Supported"/> and <see cref="FastCallShape.Variadic"/> for the arity.
    /// </summary>
    internal virtual JsValue CallFastVariadic(JsValue thisObject, ReadOnlySpan<JsValue> arguments)
        => throw new InvalidOperationException($"{GetType()} does not implement CallFastVariadic; GetFastCallShape must not report Variadic.");

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
            return;
        }

        // These own properties live in fields rather than in the property bag, so the base store's version
        // bump does not run for them. It has to run anyway: every one of them is configurable, so a define
        // can make the name own again after a delete removed it, and the member-read inline caches read
        // _propertiesVersion as proof that no own property has appeared to shadow a cached prototype hit.
        // Materializing a pending descriptor deliberately does not bump — that changes the descriptor, not
        // which names are own.
        unchecked { _propertiesVersion++; }
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
        // Step 2 before step 3: a bound function is a Function too, and its own realm is not the answer —
        // the specification asks its [[BoundTargetFunction]], which is what a cross-realm bind depends on.
        if (obj is BindFunction bindFunctionInstance)
        {
            return GetFunctionRealm(bindFunctionInstance.BoundTargetFunction);
        }

        if (obj is Function functionInstance && functionInstance._realm is not null)
        {
            return functionInstance._realm;
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

        _engine.EnterExecutionContext(in calleeContext);
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
        return (JsCallDelegate) CallFromHost;
    }

    private JsValue CallFromHost(JsValue thisObject, JsValue[] arguments)
    {
        using var ownership = _engine.EnterHostCallback();
        return Call(thisObject, arguments);
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

            if (_functionDefinition.GetSourceText() is { } sourceText)
            {
                return sourceText;
            }
        }

        var name = GetOwnFunctionName().TrimStart(_functionNameTrimStartChars);

        return $"function {name}() {{ [native code] }}";
    }

    /// <summary>
    /// The retained source text of this function's declaration, for a describing path that may not run
    /// script and may not call the host's <c>FunctionToStringHandler</c>.
    /// </summary>
    /// <remarks>
    /// The same text <see cref="ToString"/> returns when <see cref="Options.RetainFunctionSourceText"/> kept
    /// it. False for every function the parser did not build one for — a <c>ClrFunction</c>, a bound
    /// function, anything native — and for a program parsed with retention off.
    /// </remarks>
    internal bool TryGetOwnSourceText([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? sourceText)
    {
        sourceText = _functionDefinition?.GetSourceText();
        return sourceText is not null;
    }

    /// <summary>
    /// The function's own <c>name</c> as a string, or the empty string when it has none. Resolved off the
    /// descriptor field rather than through <c>Get</c> so the answer is the function's
    /// own name and not something inherited, and a pending descriptor stands for the definition's
    /// own name (see <see cref="_pendingDescriptor"/>).
    /// <para>
    /// This is where a name reaches a function object however it was acquired — a declaration's binding, an
    /// expression's own identifier, or the target NamedEvaluation gave an anonymous one — which makes it the
    /// only source a diagnostic can quote and be right for all three.
    /// </para>
    /// </summary>
    internal string GetOwnFunctionName()
    {
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

        return nameValue.IsUndefined() ? "" : TypeConverter.ToString(nameValue);
    }

    /// <summary>
    /// The function's own <c>name</c> for a diagnostic rendering — the console's <c>[Function: foo]</c> tag —
    /// or <see langword="null"/> when it cannot be read without running script.
    /// <para>
    /// This is <see cref="GetOwnFunctionName"/>'s pending-descriptor handling with
    /// <see cref="GetOwnFunctionNameForMessage"/>'s refusal to coerce. The pending sentinel is answered from
    /// the definition's cached <c>JsName</c>, exactly as materializing it would, so an ordinary
    /// <c>function foo() {}</c> reports <c>foo</c> without its descriptor being allocated. Past that, a
    /// script may have replaced <c>name</c> — it is configurable on every function — with an accessor or with
    /// an object whose <c>toString</c> is observable, and a console that renders a function must never be a
    /// way to run either. Both answer <see langword="null"/>, which the caller renders as anonymous.
    /// </para>
    /// </summary>
    internal string? GetOwnFunctionNameForDisplay()
    {
        var descriptor = _nameDescriptor;
        if (descriptor is null)
        {
            return string.Empty;
        }

        if (ReferenceEquals(descriptor, _pendingDescriptor))
        {
            return _functionDefinition?.JsName?.ToString() ?? string.Empty;
        }

        if (descriptor.IsAccessorDescriptor())
        {
            return null;
        }

        return descriptor.Value is JsString name ? name.ToString() : null;
    }

    /// <summary>
    /// The function's own <c>name</c> for an error message, rendered without ever running script.
    /// <para>
    /// <c>name</c> is configurable on every built-in, so a script can replace it with an object whose
    /// <c>toString</c> throws. <see cref="GetOwnFunctionName"/> coerces through <c>TypeConverter.ToString</c>
    /// and would let that object hijack the error being built — an extra observable call, and a thrown
    /// value replacing the <c>TypeError</c> the caller meant to raise. So only an actual string is quoted
    /// here; anything else falls back to the CLR type's name, which no script can influence. A descriptor
    /// carrying <see cref="PropertyFlag.CustomJsValue"/> is skipped for the same reason: its
    /// <see cref="PropertyDescriptor.Value"/> routes through the <c>CustomValue</c> accessor rather than the
    /// raw field, and one of those accessors — the shared pending-lazy sentinel a script function's name
    /// starts with — throws by design. An error-message path must never introduce a throw of its own.
    /// </para>
    /// </summary>
    internal string GetOwnFunctionNameForMessage()
    {
        var descriptor = _nameDescriptor;
        if (descriptor is null || (descriptor.Flags & PropertyFlag.CustomJsValue) != PropertyFlag.None)
        {
            return GetType().Name;
        }

        return descriptor.Value is JsString name ? name.ToString() : GetType().Name;
    }

    /// <summary>
    /// A constructor's lazily created <c>prototype</c> object, which serves <c>constructor</c> from a field
    /// instead of the property bag. It goes through the internal constructor rather than the protected one so
    /// its type flags are stated rather than derived per type: it does not override
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/>, so the derivation would answer
    /// <see cref="Native.Object.PropertyAccessSemantics.Ordinary"/> — correct, but it would also put an in-box
    /// type on the lane meant for objects whose own-property set the engine cannot version, and this one keeps
    /// its version accurate itself (below).
    /// </summary>
    private sealed class ObjectInstanceWithConstructor : ObjectInstance
    {
        private PropertyDescriptor? _constructor;

        public ObjectInstanceWithConstructor(Engine engine, ObjectInstance thisObj) : base(engine, ObjectClass.Object)
        {
            _constructor = new PropertyDescriptor(thisObj, PropertyFlag.NonEnumerable);
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (CommonProperties.Constructor.Equals(property))
            {
                return _constructor ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(property);
        }

        /// <summary>
        /// <c>constructor</c> lives in a field rather than the property bag, so the key enumerations have to
        /// be told about it the way <see cref="Function"/> tells them about <c>length</c>/<c>name</c>: without
        /// this, <c>Object.getOwnPropertyNames(f.prototype)</c> and <c>Reflect.ownKeys(f.prototype)</c>
        /// answered an empty list for a property <c>hasOwnProperty</c> and
        /// <c>Object.getOwnPropertyDescriptor</c> both reported.
        /// </summary>
        internal override IEnumerable<JsValue> GetInitialOwnStringPropertyKeys()
        {
            if (_constructor != null)
            {
                yield return CommonProperties.Constructor;
            }
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (CommonProperties.Constructor.Equals(property))
            {
                // Same reason as Function.SetOwnProperty: the field is own-property storage the base store's
                // version bump never sees, and `constructor` is configurable, so it can be deleted and defined
                // again while a member-read inline cache holds a prototype hit for that name.
                _constructor = desc;
                unchecked { _propertiesVersion++; }
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
                unchecked { _propertiesVersion++; }
            }
            else
            {
                base.RemoveOwnProperty(property);
            }
        }
    }
}
