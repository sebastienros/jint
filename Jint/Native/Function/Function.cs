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

    internal Environment? _environment;
    internal readonly JintFunctionDefinition? _functionDefinition;
    internal readonly FunctionThisMode _thisMode;
    internal JsValue _homeObject = Undefined;
    internal ConstructorKind _constructorKind = ConstructorKind.Base;

    internal Realm _realm;
    internal PrivateEnvironment? _privateEnvironment;
    private readonly IScriptOrModule? _scriptOrModule;

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
        : this(
            engine,
            realm,
            !string.IsNullOrWhiteSpace(function.Name) ? new JsString(function.Name!) : null,
            thisMode)
    {
        _functionDefinition = function;
        _environment = env;
    }

    internal Function(
        Engine engine,
        Realm realm,
        JsString? name,
        FunctionThisMode thisMode = FunctionThisMode.Global,
        ObjectClass objectClass = ObjectClass.Function)
        : base(engine, objectClass)
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

    internal override bool IsCallable => true;

    JsValue ICallable.Call(JsValue thisObject, params JsCallArguments arguments) => Call(thisObject, arguments);

    /// <summary>
    /// Executed when a function object is used as a function
    /// </summary>
    protected internal abstract JsValue Call(JsValue thisObject, JsCallArguments arguments);

    public bool Strict => _thisMode == FunctionThisMode.Strict;

    internal override bool IsConstructor => this is IConstructor;

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        if (_prototypeDescriptor != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Prototype, _prototypeDescriptor);
        }
        if (_length != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, _length);
        }
        if (_nameDescriptor != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Name, GetOwnProperty(CommonProperties.Name));
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
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Prototype.Equals(property))
        {
            return _prototypeDescriptor ?? PropertyDescriptor.Undefined;
        }
        if (CommonProperties.Length.Equals(property))
        {
            return _length ?? PropertyDescriptor.Undefined;
        }
        if (CommonProperties.Name.Equals(property))
        {
            return _nameDescriptor ?? PropertyDescriptor.Undefined;
        }

        return base.GetOwnProperty(property);
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

        base.RemoveOwnProperty(property);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-setfunctionname
    /// </summary>
    internal void SetFunctionName(JsValue name, string? prefix = null, bool force = false)
    {
        if (!force && _nameDescriptor != null && UnwrapJsValue(_nameDescriptor) != JsString.Empty)
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
            if (proxyInstance._handler is null)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine();
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
    internal ref readonly ExecutionContext PrepareForOrdinaryCall(JsValue newTarget)
    {
        var callerContext = _engine.ExecutionContext;

        var localEnv = JintEnvironment.NewFunctionEnvironment(_engine, this, newTarget);
        var calleeRealm = _realm;

        var calleeContext = new ExecutionContext(
            _scriptOrModule,
            lexicalEnvironment: localEnv,
            variableEnvironment: localEnv,
            _privateEnvironment,
            calleeRealm,
            generator: null,
            function: this);

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
        if (prototype is null)
        {
            prototype = new ObjectInstanceWithConstructor(_engine, this)
            {
                _prototype = _realm.Intrinsics.Object.PrototypeObject
            };
        }

        _prototypeDescriptor = new PropertyDescriptor(prototype, writableProperty, enumerable: false, configurable: false);
    }

    internal void SetFunctionLength(JsNumber length)
    {
        DefinePropertyOrThrow(CommonProperties.Length, new PropertyDescriptor(length, writable: false, enumerable: false, configurable: true));
    }

    // native syntax doesn't expect to have private identifier indicator
    private static readonly char[] _functionNameTrimStartChars = ['#'];

    public override string ToString()
    {
        if (_functionDefinition?.Function is Node node && _engine.Options.Host.FunctionToStringHandler(this, node) is { } s)
        {
            return s;
        }

        var nameValue = _nameDescriptor != null ? UnwrapJsValue(_nameDescriptor) : JsString.Empty;
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
