using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native;

internal sealed class JsProxy : ObjectInstance, IConstructor, ICallable
{
    internal ObjectInstance _target;
    internal ObjectInstance? _handler;
    private ProxyHandler? _clrHandler;

    // https://tc39.es/ecma262/#sec-proxycreate
    // A proxy has [[Construct]] iff its target is a constructor at creation time;
    // a handler "construct" trap cannot confer constructability. Captured here so
    // IsConstructor probes are side-effect free (no handler getter invocation) and
    // remain correct after revocation nulls the target.
    private readonly bool _isConstructor;

    // mirrors ClrFunction: with the default (bubbling) exception handler, CLR trap
    // exceptions propagate untouched; otherwise they are routed through the handler
    private readonly bool _bubbleExceptions;

    private static readonly JsString TrapApply = new JsString("apply");
    private static readonly JsString TrapGet = new JsString("get");
    private static readonly JsString TrapSet = new JsString("set");
    private static readonly JsString TrapPreventExtensions = new JsString("preventExtensions");
    private static readonly JsString TrapIsExtensible = new JsString("isExtensible");
    private static readonly JsString TrapDefineProperty = new JsString("defineProperty");
    private static readonly JsString TrapDeleteProperty = new JsString("deleteProperty");
    private static readonly JsString TrapGetOwnPropertyDescriptor = new JsString("getOwnPropertyDescriptor");
    private static readonly JsString TrapHas = new JsString("has");
    private static readonly JsString TrapGetProtoTypeOf = new JsString("getPrototypeOf");
    private static readonly JsString TrapSetProtoTypeOf = new JsString("setPrototypeOf");
    private static readonly JsString TrapOwnKeys = new JsString("ownKeys");
    private static readonly JsString TrapConstruct = new JsString("construct");

    private static readonly JsString KeyIsArray = new JsString("isArray");

    public JsProxy(
        Engine engine,
        ObjectInstance target,
        ObjectInstance handler)
        : this(engine, target, handler, clrHandler: null)
    {
    }

    internal JsProxy(
        Engine engine,
        ObjectInstance target,
        ProxyHandler clrHandler)
        : this(engine, target, handler: null, clrHandler)
    {
    }

    private JsProxy(
        Engine engine,
        ObjectInstance target,
        ObjectInstance? handler,
        ProxyHandler? clrHandler)
        : base(engine, target.Class)
    {
        _type |= InternalTypes.ExoticGet;
        _target = target;
        _handler = handler;
        _clrHandler = clrHandler;
        IsCallable = target.IsCallable;
        _isConstructor = target.IsConstructor;
        _bubbleExceptions = engine.Options.Interop.ExceptionHandler == Options.InteropOptions._defaultExceptionHandler;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-call-thisargument-argumentslist
    /// </summary>
    JsValue ICallable.Call(JsValue thisObject, params JsCallArguments arguments)
    {
        AssertNotRevoked(TrapApply);

        // a proxy only has [[Call]] if its target does - emulate the missing internal
        // method for callers that reach us through the ICallable interface
        if (!IsCallable)
        {
            Throw.TypeError(_engine.Realm, "Proxy target is not a function");
        }

        var target = _target;

        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrApply(target, thisObject, CopyArgumentsForClrTrap(arguments));
            if (clrResult is not null)
            {
                // the spec has no apply-result invariant, no validation needed
                return clrResult;
            }
        }
        else
        {
            var trap = GetTrap(TrapApply);
            if (trap is not null)
            {
                // step 7: CreateArrayFromList(argumentsList) is only observable by the trap, built lazily
                var argArray = _engine.Realm.Intrinsics.Array.ConstructFast(arguments);
                return CallTrap(trap, target, thisObject, argArray);
            }
        }

        var callable = target as ICallable;
        if (callable is null)
        {
            Throw.TypeError(_engine.Realm, target + " is not a function");
        }

        return callable.Call(thisObject, arguments);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-construct-argumentslist-newtarget
    /// </summary>
    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
    {
        AssertNotRevoked(TrapConstruct);

        if (!_isConstructor)
        {
            Throw.TypeError(_engine.Realm, "Proxy target is not a constructor");
        }

        var target = _target;

        if (_clrHandler is not null)
        {
            // the typed ObjectInstance? return enforces the object invariant at compile time
            var clrResult = InvokeClrConstruct(target, CopyArgumentsForClrTrap(arguments), newTarget);
            if (clrResult is not null)
            {
                return clrResult;
            }
        }
        else
        {
            var trap = GetTrap(TrapConstruct);
            if (trap is not null)
            {
                // step 7: CreateArrayFromList(argumentsList) - must not use Array constructor semantics,
                // a single numeric argument would create a hole-filled array of that length
                var argArray = _engine.Realm.Intrinsics.Array.ConstructFast(arguments);
                var result = CallTrap(trap, target, argArray, newTarget);

                var oi = result as ObjectInstance;
                if (oi is null)
                {
                    Throw.TypeError(_engine.Realm, $"'construct' on proxy: trap returned non-object ('{result}')");
                }

                return oi;
            }
        }

        var constructor = target as IConstructor;
        if (constructor is null)
        {
            Throw.TypeError(_engine.Realm, "Proxy target is not a constructor");
        }

        return constructor.Construct(arguments, newTarget);
    }

    internal override bool IsArray()
    {
        AssertNotRevoked(KeyIsArray);
        return _target.IsArray();
    }

    public override object ToObject() => _target.ToObject();

    internal override bool IsConstructor => _isConstructor;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-get-p-receiver
    /// </summary>
    public override JsValue Get(JsValue property, JsValue receiver)
    {
        AssertNotRevoked(TrapGet);
        var target = _target;

        JsValue result;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrGet(target, TypeConverter.ToPropertyKey(property), receiver);
            if (clrResult is null)
            {
                return target.Get(property, receiver);
            }

            result = clrResult;
        }
        else
        {
            var trap = GetTrap(TrapGet);
            if (trap is null)
            {
                return target.Get(property, receiver);
            }

            result = CallTrap(trap, target, TypeConverter.ToPropertyKey(property), receiver);
        }

        ValidateGetTrapResult(target, property, result);
        return result;
    }

    private void ValidateGetTrapResult(ObjectInstance target, JsValue property, JsValue result)
    {
        var targetDesc = target.GetOwnProperty(property);
        if (targetDesc != PropertyDescriptor.Undefined)
        {
            if (targetDesc.IsDataDescriptor())
            {
                var targetValue = targetDesc.Value;
                if (!targetDesc.Configurable && !targetDesc.Writable && !SameValue(result, targetValue))
                {
                    Throw.TypeError(_engine.Realm, $"'get' on proxy: property '{property}' is a read-only and non-configurable data property on the proxy target but the proxy did not return its actual value (expected '{targetValue}' but got '{result}')");
                }
            }

            if (targetDesc.IsAccessorDescriptor())
            {
                if (!targetDesc.Configurable && (targetDesc.Get ?? Undefined).IsUndefined() && !result.IsUndefined())
                {
                    Throw.TypeError(_engine.Realm, $"'get' on proxy: property '{property}' is a non-configurable accessor property on the proxy target and does not have a getter function, but the trap did not return 'undefined' (got '{result}')");
                }
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-ownpropertykeys
    /// </summary>
    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        AssertNotRevoked(TrapOwnKeys);
        var target = _target;

        JsValue result;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrOwnKeys(target);
            if (clrResult is null)
            {
                return target.GetOwnPropertyKeys(types);
            }

            result = CreateOwnKeysArray(clrResult);
        }
        else
        {
            var trap = GetTrap(TrapOwnKeys);
            if (trap is null)
            {
                return target.GetOwnPropertyKeys(types);
            }

            result = CallTrap(trap, target);
        }

        return ValidateOwnKeysTrapResult(target, result);
    }

    private JsArray CreateOwnKeysArray(IReadOnlyList<JsValue> keys)
    {
        // element-wise copy: the handler may hand out a list it mutates later, and null
        // elements must be rejected before the validator dereferences them
        var copy = new JsValue[keys.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            var key = keys[i];
            if (key is null)
            {
                Throw.TypeError(_engine.Realm, "'ownKeys' on proxy: CLR trap returned a null element");
            }

            copy[i] = key;
        }

        return new JsArray(_engine, copy);
    }

    private List<JsValue> ValidateOwnKeysTrapResult(ObjectInstance target, JsValue result)
    {
        var keys = FunctionPrototype.CreateListFromArrayLike(_engine.Realm, result, Types.String | Types.Symbol);

        // one set serves both duplicate detection and the spec's uncheckedResultKeys below
        var uncheckedResultKeys = new HashSet<JsValue>();
        var trapResult = new List<JsValue>(keys.Length);
        foreach (var key in keys)
        {
            if (!uncheckedResultKeys.Add(key))
            {
                Throw.TypeError(_engine.Realm, $"'ownKeys' on proxy: trap returned duplicate entries ('{key}')");
            }
            trapResult.Add(key);
        }

        var extensibleTarget = target.Extensible;
        var targetKeys = target.GetOwnPropertyKeys();
        List<JsValue>? targetConfigurableKeys = null;
        List<JsValue>? targetNonconfigurableKeys = null;

        // every target key must be probed with [[GetOwnProperty]] before any invariant throw,
        // the probe is observable when the target is itself exotic
        foreach (var property in targetKeys)
        {
            var desc = target.GetOwnProperty(property);
            if (desc != PropertyDescriptor.Undefined && !desc.Configurable)
            {
                (targetNonconfigurableKeys ??= new List<JsValue>()).Add(property);
            }
            else if (!extensibleTarget)
            {
                // the configurable partition is only consulted when the target is non-extensible
                (targetConfigurableKeys ??= new List<JsValue>()).Add(property);
            }
        }

        if (targetNonconfigurableKeys is not null)
        {
            for (var i = 0; i < targetNonconfigurableKeys.Count; i++)
            {
                var key = targetNonconfigurableKeys[i];
                if (!uncheckedResultKeys.Remove(key))
                {
                    Throw.TypeError(_engine.Realm, $"'ownKeys' on proxy: trap result did not include non-configurable property '{key}' of the proxy target");
                }
            }
        }

        if (extensibleTarget)
        {
            return trapResult;
        }

        if (targetConfigurableKeys is not null)
        {
            for (var i = 0; i < targetConfigurableKeys.Count; i++)
            {
                var key = targetConfigurableKeys[i];
                if (!uncheckedResultKeys.Remove(key))
                {
                    Throw.TypeError(_engine.Realm, $"'ownKeys' on proxy: trap result did not include property '{key}' of the non-extensible proxy target");
                }
            }
        }

        if (uncheckedResultKeys.Count > 0)
        {
            Throw.TypeError(_engine.Realm, "'ownKeys' on proxy: trap returned extra keys but proxy target is non-extensible");
        }

        return trapResult;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getownproperty-p
    /// </summary>
    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        AssertNotRevoked(TrapGetOwnPropertyDescriptor);
        var target = _target;

        JsValue trapResultObj;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrGetOwnPropertyDescriptor(target, TypeConverter.ToPropertyKey(property));
            if (clrResult is null)
            {
                return target.GetOwnProperty(property);
            }

            // PropertyDescriptor.Undefined round-trips as JsValue.Undefined ("no such property")
            trapResultObj = PropertyDescriptor.FromPropertyDescriptor(_engine, clrResult, strictUndefined: true);
        }
        else
        {
            var trap = GetTrap(TrapGetOwnPropertyDescriptor);
            if (trap is null)
            {
                return target.GetOwnProperty(property);
            }

            trapResultObj = CallTrap(trap, target, TypeConverter.ToPropertyKey(property));
        }

        return ValidateGetOwnPropertyTrapResult(target, property, trapResultObj);
    }

    private PropertyDescriptor ValidateGetOwnPropertyTrapResult(ObjectInstance target, JsValue property, JsValue trapResultObj)
    {
        if (!trapResultObj.IsObject() && !trapResultObj.IsUndefined())
        {
            Throw.TypeError(_engine.Realm, $"'getOwnPropertyDescriptor' on proxy: trap returned neither object nor undefined for property '{property}'");
        }

        var targetDesc = target.GetOwnProperty(property);

        if (trapResultObj.IsUndefined())
        {
            if (targetDesc == PropertyDescriptor.Undefined)
            {
                return targetDesc;
            }

            if (!targetDesc.Configurable)
            {
                Throw.TypeError(_engine.Realm, $"'getOwnPropertyDescriptor' on proxy: trap returned undefined for property '{property}' which is non-configurable in the proxy target");
            }

            if (!target.Extensible)
            {
                Throw.TypeError(_engine.Realm, $"'getOwnPropertyDescriptor' on proxy: trap returned undefined for property '{property}' which exists in the non-extensible proxy target");
            }

            return PropertyDescriptor.Undefined;
        }

        var extensibleTarget = target.Extensible;
        var resultDesc = PropertyDescriptor.ToPropertyDescriptor(_engine.Realm, trapResultObj);
        CompletePropertyDescriptor(resultDesc);

        var valid = IsCompatiblePropertyDescriptor(extensibleTarget, resultDesc, targetDesc);
        if (!valid)
        {
            Throw.TypeError(_engine.Realm, $"'getOwnPropertyDescriptor' on proxy: trap returned descriptor for property '{property}' that is incompatible with the existing property in the proxy target");
        }

        if (!resultDesc.Configurable)
        {
            if (targetDesc == PropertyDescriptor.Undefined || targetDesc.Configurable)
            {
                Throw.TypeError(_engine.Realm, $"'getOwnPropertyDescriptor' on proxy: trap reported non-configurability for property '{property}' which is either non-existent or configurable in the proxy target");
            }

            if (resultDesc.WritableSet && !resultDesc.Writable)
            {
                if (targetDesc.Writable)
                {
                    Throw.TypeError(_engine.Realm, $"'getOwnPropertyDescriptor' on proxy: trap reported non-configurable and writable: false for property '{property}' which is non-configurable and writable in the proxy target");
                }
            }
        }

        return resultDesc;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-completepropertydescriptor
    /// </summary>
    private static void CompletePropertyDescriptor(PropertyDescriptor desc)
    {
        if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
        {
            desc.Value ??= Undefined;
            if (!desc.WritableSet)
            {
                desc.Writable = false;
            }
        }
        else
        {
            var getSet = (GetSetPropertyDescriptor) desc;
            getSet.SetGet(getSet.Get ?? Undefined);
            getSet.SetSet(getSet.Set ?? Undefined);
        }

        if (!desc.EnumerableSet)
        {
            desc.Enumerable = false;
        }
        if (!desc.ConfigurableSet)
        {
            desc.Configurable = false;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-set-p-v-receiver
    /// </summary>
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        AssertNotRevoked(TrapSet);
        var target = _target;

        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrSet(target, TypeConverter.ToPropertyKey(property), value, receiver);
            if (clrResult is null)
            {
                return target.Set(property, value, receiver);
            }

            if (!clrResult.Value)
            {
                return false;
            }
        }
        else
        {
            var trap = GetTrap(TrapSet);
            if (trap is null)
            {
                return target.Set(property, value, receiver);
            }

            var trapResult = CallTrap(trap, target, TypeConverter.ToPropertyKey(property), value, receiver);
            if (!TypeConverter.ToBoolean(trapResult))
            {
                return false;
            }
        }

        ValidateSetTrapResult(target, property, value);
        return true;
    }

    private void ValidateSetTrapResult(ObjectInstance target, JsValue property, JsValue value)
    {
        var targetDesc = target.GetOwnProperty(property);
        if (targetDesc != PropertyDescriptor.Undefined)
        {
            if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && !targetDesc.Writable)
            {
                var targetValue = targetDesc.Value;
                if (!SameValue(targetValue, value))
                {
                    Throw.TypeError(_engine.Realm, $"'set' on proxy: trap returned truish for property '{property}' which exists in the proxy target as a non-configurable and non-writable data property with a different value");
                }
            }

            if (targetDesc.IsAccessorDescriptor() && !targetDesc.Configurable)
            {
                if ((targetDesc.Set ?? Undefined).IsUndefined())
                {
                    Throw.TypeError(_engine.Realm, $"'set' on proxy: trap returned truish for property '{property}' which exists in the proxy target as a non-configurable and non-writable accessor property without a setter");
                }
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-defineownproperty-p-desc
    /// </summary>
    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        AssertNotRevoked(TrapDefineProperty);
        var target = _target;

        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrDefineProperty(target, TypeConverter.ToPropertyKey(property), desc);
            if (clrResult is null)
            {
                return target.DefineOwnProperty(property, desc);
            }

            if (!clrResult.Value)
            {
                return false;
            }
        }
        else
        {
            var trap = GetTrap(TrapDefineProperty);
            if (trap is null)
            {
                return target.DefineOwnProperty(property, desc);
            }

            var descObj = PropertyDescriptor.FromPropertyDescriptor(_engine, desc, strictUndefined: true);
            var result = CallTrap(trap, target, TypeConverter.ToPropertyKey(property), descObj);
            if (!TypeConverter.ToBoolean(result))
            {
                return false;
            }
        }

        ValidateDefinePropertyTrapResult(target, property, desc);
        return true;
    }

    private void ValidateDefinePropertyTrapResult(ObjectInstance target, JsValue property, PropertyDescriptor desc)
    {
        var targetDesc = target.GetOwnProperty(property);
        var extensibleTarget = target.Extensible;
        var settingConfigFalse = desc.ConfigurableSet && !desc.Configurable;

        if (targetDesc == PropertyDescriptor.Undefined)
        {
            if (!extensibleTarget)
            {
                Throw.TypeError(_engine.Realm, $"'defineProperty' on proxy: trap returned truish for adding property '{property}' to the non-extensible proxy target");
            }
            if (settingConfigFalse)
            {
                Throw.TypeError(_engine.Realm, $"'defineProperty' on proxy: trap returned truish for defining non-configurable property '{property}' which is non-existent in the proxy target");
            }
        }
        else
        {
            if (!IsCompatiblePropertyDescriptor(extensibleTarget, desc, targetDesc))
            {
                Throw.TypeError(_engine.Realm, $"'defineProperty' on proxy: trap returned truish for adding property '{property}' that is incompatible with the existing property in the proxy target");
            }
            if (targetDesc.Configurable && settingConfigFalse)
            {
                Throw.TypeError(_engine.Realm, $"'defineProperty' on proxy: trap returned truish for defining non-configurable property '{property}' which is configurable in the proxy target");
            }

            if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && targetDesc.Writable)
            {
                if (desc.WritableSet && !desc.Writable)
                {
                    Throw.TypeError(_engine.Realm, $"'defineProperty' on proxy: trap returned truish for defining non-writable property '{property}' which is writable in the proxy target");
                }
            }
        }
    }

    private static bool IsCompatiblePropertyDescriptor(bool extensible, PropertyDescriptor desc, PropertyDescriptor current)
    {
        return ValidateAndApplyPropertyDescriptor(null, JsString.Empty, extensible, desc, current);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-hasproperty-p
    /// </summary>
    public override bool HasProperty(JsValue property)
    {
        AssertNotRevoked(TrapHas);
        var target = _target;

        bool trapResult;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrHas(target, TypeConverter.ToPropertyKey(property));
            if (clrResult is null)
            {
                return target.HasProperty(property);
            }

            trapResult = clrResult.Value;
        }
        else
        {
            var trap = GetTrap(TrapHas);
            if (trap is null)
            {
                return target.HasProperty(property);
            }

            trapResult = TypeConverter.ToBoolean(CallTrap(trap, target, TypeConverter.ToPropertyKey(property)));
        }

        if (!trapResult)
        {
            ValidateHasTrapResult(target, property);
        }

        return trapResult;
    }

    private void ValidateHasTrapResult(ObjectInstance target, JsValue property)
    {
        var targetDesc = target.GetOwnProperty(property);
        if (targetDesc != PropertyDescriptor.Undefined)
        {
            if (!targetDesc.Configurable)
            {
                Throw.TypeError(_engine.Realm, $"'has' on proxy: trap returned falsish for property '{property}' which exists in the proxy target as non-configurable");
            }

            if (!target.Extensible)
            {
                Throw.TypeError(_engine.Realm, $"'has' on proxy: trap returned falsish for property '{property}' but the proxy target is not extensible");
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-delete-p
    /// </summary>
    public override bool Delete(JsValue property)
    {
        AssertNotRevoked(TrapDeleteProperty);
        var target = _target;

        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrDeleteProperty(target, TypeConverter.ToPropertyKey(property));
            if (clrResult is null)
            {
                return target.Delete(property);
            }

            if (!clrResult.Value)
            {
                return false;
            }
        }
        else
        {
            var trap = GetTrap(TrapDeleteProperty);
            if (trap is null)
            {
                return target.Delete(property);
            }

            if (!TypeConverter.ToBoolean(CallTrap(trap, target, TypeConverter.ToPropertyKey(property))))
            {
                return false;
            }
        }

        ValidateDeleteTrapResult(target, property);
        return true;
    }

    private void ValidateDeleteTrapResult(ObjectInstance target, JsValue property)
    {
        var targetDesc = target.GetOwnProperty(property);

        if (targetDesc == PropertyDescriptor.Undefined)
        {
            return;
        }

        if (!targetDesc.Configurable)
        {
            Throw.TypeError(_engine.Realm, $"'deleteProperty' on proxy: trap returned truish for property '{property}' which is non-configurable in the proxy target");
        }

        if (!target.Extensible)
        {
            Throw.TypeError(_engine.Realm, $"'deleteProperty' on proxy: trap returned truish for property '{property}' but the proxy target is non-extensible");
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-preventextensions
    /// </summary>
    public override bool PreventExtensions()
    {
        AssertNotRevoked(TrapPreventExtensions);
        var target = _target;

        bool success;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrPreventExtensions(target);
            if (clrResult is null)
            {
                return target.PreventExtensions();
            }

            success = clrResult.Value;
        }
        else
        {
            var trap = GetTrap(TrapPreventExtensions);
            if (trap is null)
            {
                return target.PreventExtensions();
            }

            success = TypeConverter.ToBoolean(CallTrap(trap, target));
        }

        if (success && target.Extensible)
        {
            Throw.TypeError(_engine.Realm, "'preventExtensions' on proxy: trap returned truish but the proxy target is extensible");
        }

        return success;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-isextensible
    /// </summary>
    public override bool Extensible
    {
        get
        {
            AssertNotRevoked(TrapIsExtensible);
            var target = _target;

            bool booleanTrapResult;
            if (_clrHandler is not null)
            {
                var clrResult = InvokeClrIsExtensible(target);
                if (clrResult is null)
                {
                    return target.Extensible;
                }

                booleanTrapResult = clrResult.Value;
            }
            else
            {
                var trap = GetTrap(TrapIsExtensible);
                if (trap is null)
                {
                    return target.Extensible;
                }

                booleanTrapResult = TypeConverter.ToBoolean(CallTrap(trap, target));
            }

            var targetResult = target.Extensible;
            if (booleanTrapResult != targetResult)
            {
                Throw.TypeError(_engine.Realm, $"'isExtensible' on proxy: trap result does not reflect extensibility of proxy target (which is '{(targetResult ? "true" : "false")}')");
            }

            return booleanTrapResult;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getprototypeof
    /// </summary>
    protected internal override ObjectInstance? GetPrototypeOf()
    {
        AssertNotRevoked(TrapGetProtoTypeOf);
        var target = _target;

        JsValue handlerProto;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrGetPrototypeOf(target);
            if (clrResult is null)
            {
                return target.Prototype;
            }

            handlerProto = clrResult;
        }
        else
        {
            var trap = GetTrap(TrapGetProtoTypeOf);
            if (trap is null)
            {
                return target.Prototype;
            }

            handlerProto = CallTrap(trap, target);
        }

        if (!handlerProto.IsObject() && !handlerProto.IsNull())
        {
            Throw.TypeError(_engine.Realm, "'getPrototypeOf' on proxy: trap returned neither object nor null");
        }

        var proto = handlerProto.IsNull() ? null : (ObjectInstance) handlerProto;

        if (target.Extensible)
        {
            return proto;
        }

        if (!ReferenceEquals(proto, target.Prototype))
        {
            Throw.TypeError(_engine.Realm, "'getPrototypeOf' on proxy: proxy target is non-extensible but the trap did not return its actual prototype");
        }

        return proto;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-setprototypeof-v
    /// </summary>
    internal override bool SetPrototypeOf(JsValue value)
    {
        AssertNotRevoked(TrapSetProtoTypeOf);
        var target = _target;

        bool success;
        if (_clrHandler is not null)
        {
            var clrResult = InvokeClrSetPrototypeOf(target, value);
            if (clrResult is null)
            {
                return target.SetPrototypeOf(value);
            }

            success = clrResult.Value;
        }
        else
        {
            var trap = GetTrap(TrapSetProtoTypeOf);
            if (trap is null)
            {
                return target.SetPrototypeOf(value);
            }

            success = TypeConverter.ToBoolean(CallTrap(trap, target, value));
        }

        if (!success)
        {
            return false;
        }

        if (target.Extensible)
        {
            return true;
        }

        // callers have validated that value is either an object or null
        var proto = value.IsNull() ? null : value as ObjectInstance;
        if (!ReferenceEquals(proto, target.Prototype))
        {
            Throw.TypeError(_engine.Realm, "'setPrototypeOf' on proxy: trap returned truish for setting a new prototype on the non-extensible proxy target");
        }

        return true;
    }

    internal override bool IsCallable { get; }

    /// <summary>
    /// Shared trap fetch: GetMethod(handler, trapName). Returns null when the trap is absent
    /// (caller forwards to the target). Callers must have called AssertNotRevoked first and only
    /// call this when the proxy uses a JavaScript handler object. The fetch is spec-observable and
    /// must happen fresh on every operation - the handler may be a proxy itself or use getters,
    /// and traps may be added or removed between operations, so the result must never be cached.
    /// https://tc39.es/ecma262/#sec-getmethod
    /// </summary>
    private ICallable? GetTrap(JsString trapName)
    {
        var handlerFunction = _handler!.Get(trapName);
        if (handlerFunction.IsNullOrUndefined())
        {
            return null;
        }

        var callable = handlerFunction as ICallable;
        if (callable is null)
        {
            Throw.TypeError(_engine.Realm, $"'{trapName}' trap of proxy handler is not a function");
        }

        return callable;
    }

    // trap argument arrays are rented and returned after the call completes, following the
    // interpreter call-site pattern (no finally: an exception lets the array fall to the GC,
    // and JsArguments.Materialize protects arrays captured by the callee)

    private JsValue CallTrap(ICallable trap, JsValue arg0)
    {
        var pool = _engine._jsValueArrayPool;
        var args = pool.RentArray(1);
        args[0] = arg0;
        var result = trap.Call(_handler!, args);
        pool.ReturnArray(args);
        return result;
    }

    private JsValue CallTrap(ICallable trap, JsValue arg0, JsValue arg1)
    {
        var pool = _engine._jsValueArrayPool;
        var args = pool.RentArray(2);
        args[0] = arg0;
        args[1] = arg1;
        var result = trap.Call(_handler!, args);
        pool.ReturnArray(args);
        return result;
    }

    private JsValue CallTrap(ICallable trap, JsValue arg0, JsValue arg1, JsValue arg2)
    {
        var pool = _engine._jsValueArrayPool;
        var args = pool.RentArray(3);
        args[0] = arg0;
        args[1] = arg1;
        args[2] = arg2;
        var result = trap.Call(_handler!, args);
        pool.ReturnArray(args);
        return result;
    }

    private JsValue CallTrap(ICallable trap, JsValue arg0, JsValue arg1, JsValue arg2, JsValue arg3)
    {
        var pool = _engine._jsValueArrayPool;
        var args = pool.RentArray(4);
        args[0] = arg0;
        args[1] = arg1;
        args[2] = arg2;
        args[3] = arg3;
        var result = trap.Call(_handler!, args);
        pool.ReturnArray(args);
        return result;
    }

    private void AssertNotRevoked(JsValue key)
    {
        if (_target is null)
        {
            Throw.TypeError(_engine.Realm, $"Cannot perform '{key}' on a proxy that has been revoked");
        }
    }

    internal bool IsRevoked => _target is null;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-revocation-functions
    /// Revokes the proxy: subsequent trap operations throw a TypeError. Idempotent.
    /// </summary>
    internal void Revoke()
    {
        _target = null!;
        _handler = null;
        _clrHandler = null;
    }

    /// <summary>
    /// Interpreter call sites hand us pooled argument arrays that may also be object[] instances
    /// reinterpreted via Unsafe.As; a CLR trap is user code that may legitimately hold on to (or
    /// reflect over) its arguments, so it must receive a private, genuine JsValue[] copy built
    /// element-wise.
    /// </summary>
    private static JsValue[] CopyArgumentsForClrTrap(JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            return [];
        }

        var copy = new JsValue[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            copy[i] = arguments[i];
        }

        return copy;
    }

    // CLR trap invocation wrappers. Exception routing mirrors ClrFunction: with the default
    // (bubbling) exception handler the catch filter never engages and user exceptions propagate
    // untouched; otherwise Options.Interop.ExceptionHandler decides whether the exception becomes
    // a catchable JavaScript error. The try/catch is free on the non-throwing path.

    private JsValue? InvokeClrGet(ObjectInstance target, JsValue property, JsValue receiver)
    {
        try
        {
            return _clrHandler!.Get(target, property, receiver);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrSet(ObjectInstance target, JsValue property, JsValue value, JsValue receiver)
    {
        try
        {
            return _clrHandler!.Set(target, property, value, receiver);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrHas(ObjectInstance target, JsValue property)
    {
        try
        {
            return _clrHandler!.Has(target, property);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrDeleteProperty(ObjectInstance target, JsValue property)
    {
        try
        {
            return _clrHandler!.DeleteProperty(target, property);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private PropertyDescriptor? InvokeClrGetOwnPropertyDescriptor(ObjectInstance target, JsValue property)
    {
        try
        {
            return _clrHandler!.GetOwnPropertyDescriptor(target, property);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrDefineProperty(ObjectInstance target, JsValue property, PropertyDescriptor descriptor)
    {
        try
        {
            return _clrHandler!.DefineProperty(target, property, descriptor);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private IReadOnlyList<JsValue>? InvokeClrOwnKeys(ObjectInstance target)
    {
        try
        {
            return _clrHandler!.OwnKeys(target);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private JsValue? InvokeClrApply(ObjectInstance target, JsValue thisObject, JsCallArguments arguments)
    {
        try
        {
            return _clrHandler!.Apply(target, thisObject, arguments);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private ObjectInstance? InvokeClrConstruct(ObjectInstance target, JsCallArguments arguments, JsValue newTarget)
    {
        try
        {
            return _clrHandler!.Construct(target, arguments, newTarget);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private JsValue? InvokeClrGetPrototypeOf(ObjectInstance target)
    {
        try
        {
            return _clrHandler!.GetPrototypeOf(target);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrSetPrototypeOf(ObjectInstance target, JsValue prototype)
    {
        try
        {
            return _clrHandler!.SetPrototypeOf(target, prototype);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrIsExtensible(ObjectInstance target)
    {
        try
        {
            return _clrHandler!.IsExtensible(target);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    private bool? InvokeClrPreventExtensions(ObjectInstance target)
    {
        try
        {
            return _clrHandler!.PreventExtensions(target);
        }
        catch (Exception e) when (e is not JavaScriptException && !_bubbleExceptions)
        {
            RethrowClrTrapException(e);
            return null;
        }
    }

    [DoesNotReturn]
    private void RethrowClrTrapException(Exception exception)
    {
        if (_engine.Options.Interop.ExceptionHandler(exception))
        {
            Throw.FromClrException(_engine, exception);
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
#pragma warning disable CS8763
    }
#pragma warning restore CS8763

    public override string ToString() => IsCallable ? "function () { [native code] }" : base.ToString();
}
