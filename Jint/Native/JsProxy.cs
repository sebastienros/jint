using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native;

internal sealed class JsProxy : ObjectInstance, IConstructor, ICallable
{
    internal ObjectInstance _target;
    internal ObjectInstance? _handler;

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

    private static readonly JsString KeyFunctionRevoke = new JsString("revoke");
    private static readonly JsString KeyIsArray = new JsString("isArray");

    public JsProxy(
        Engine engine,
        ObjectInstance target,
        ObjectInstance handler)
        : base(engine, target.Class)
    {
        _target = target;
        _handler = handler;
        IsCallable = target.IsCallable;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-call-thisargument-argumentslist
    /// </summary>
    JsValue ICallable.Call(JsValue thisObject, params JsCallArguments arguments)
    {
        if (_target is not ICallable)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "(intermediate value) is not a function");
        }

        var jsValues = new[] { _target, thisObject, _engine.Realm.Intrinsics.Array.ConstructFast(arguments) };
        if (TryCallHandler(TrapApply, jsValues, out var result))
        {
            return result;
        }

        var callable = _target as ICallable;
        if (callable is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, _target + " is not a function");
        }

        return callable.Call(thisObject, arguments);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-construct-argumentslist-newtarget
    /// </summary>
    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (_target is not ICallable)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "(intermediate value) is not a constructor");
        }

        var argArray = _engine.Realm.Intrinsics.Array.Construct(arguments, _engine.Realm.Intrinsics.Array);

        if (!TryCallHandler(TrapConstruct, [_target, argArray, newTarget], out var result))
        {
            var constructor = _target as IConstructor;
            if (constructor is null)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
            return constructor.Construct(arguments, newTarget);
        }

        var oi = result as ObjectInstance;
        if (oi is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        return oi;
    }

    internal override bool IsArray()
    {
        AssertNotRevoked(KeyIsArray);
        return _target.IsArray();
    }

    public override object ToObject() => _target.ToObject();

    internal override bool IsConstructor
    {
        get
        {
            if (_target is not null && _target.IsConstructor)
            {
                return true;
            }

            if (_handler is not null && _handler.TryGetValue(TrapConstruct, out var handlerFunction) && handlerFunction is IConstructor)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-get-p-receiver
    /// </summary>
    public override JsValue Get(JsValue property, JsValue receiver)
    {
        AssertTargetNotRevoked(property);
        var target = _target;

        if (KeyFunctionRevoke.Equals(property) || !TryCallHandler(TrapGet, [target, TypeConverter.ToPropertyKey(property), receiver], out var result))
        {
            return target.Get(property, receiver);
        }

        var targetDesc = target.GetOwnProperty(property);
        if (targetDesc != PropertyDescriptor.Undefined)
        {
            if (targetDesc.IsDataDescriptor())
            {
                var targetValue = targetDesc.Value;
                if (!targetDesc.Configurable && !targetDesc.Writable && !SameValue(result, targetValue))
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, $"'get' on proxy: property '{property}' is a read-only and non-configurable data property on the proxy target but the proxy did not return its actual value (expected '{targetValue}' but got '{result}')");
                }
            }

            if (targetDesc.IsAccessorDescriptor())
            {
                if (!targetDesc.Configurable && (targetDesc.Get ?? Undefined).IsUndefined() && !result.IsUndefined())
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, $"'get' on proxy: property '{property}' is a non-configurable accessor property on the proxy target and does not have a getter function, but the trap did not return 'undefined' (got '{result}')");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-ownpropertykeys
    /// </summary>
    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        if (!TryCallHandler(TrapOwnKeys, [_target], out var result))
        {
            return _target.GetOwnPropertyKeys(types);
        }

        var trapResult = new List<JsValue>(FunctionPrototype.CreateListFromArrayLike(_engine.Realm, result, Types.String | Types.Symbol));

        if (trapResult.Count != new HashSet<JsValue>(trapResult).Count)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        var extensibleTarget = _target.Extensible;
        var targetKeys = _target.GetOwnPropertyKeys();
        var targetConfigurableKeys = new List<JsValue>();
        var targetNonconfigurableKeys = new List<JsValue>();

        foreach (var property in targetKeys)
        {
            var desc = _target.GetOwnProperty(property);
            if (desc != PropertyDescriptor.Undefined && !desc.Configurable)
            {
                targetNonconfigurableKeys.Add(property);
            }
            else
            {
                targetConfigurableKeys.Add(property);
            }
        }

        var uncheckedResultKeys = new HashSet<JsValue>(trapResult);
        for (var i = 0; i < targetNonconfigurableKeys.Count; i++)
        {
            var key = targetNonconfigurableKeys[i];
            if (!uncheckedResultKeys.Remove(key))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
        }

        if (extensibleTarget)
        {
            return trapResult;
        }

        for (var i = 0; i < targetConfigurableKeys.Count; i++)
        {
            var key = targetConfigurableKeys[i];
            if (!uncheckedResultKeys.Remove(key))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
        }

        if (uncheckedResultKeys.Count > 0)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        return trapResult;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getownproperty-p
    /// </summary>
    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (!TryCallHandler(TrapGetOwnPropertyDescriptor, [_target, TypeConverter.ToPropertyKey(property)], out var trapResultObj))
        {
            return _target.GetOwnProperty(property);
        }

        if (!trapResultObj.IsObject() && !trapResultObj.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        var targetDesc = _target.GetOwnProperty(property);

        if (trapResultObj.IsUndefined())
        {
            if (targetDesc == PropertyDescriptor.Undefined)
            {
                return targetDesc;
            }

            if (!targetDesc.Configurable || !_target.Extensible)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            return PropertyDescriptor.Undefined;
        }

        var extensibleTarget = _target.Extensible;
        var resultDesc = PropertyDescriptor.ToPropertyDescriptor(_engine.Realm, trapResultObj);
        CompletePropertyDescriptor(resultDesc);

        var valid = IsCompatiblePropertyDescriptor(extensibleTarget, resultDesc, targetDesc);
        if (!valid)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        if (!resultDesc.Configurable)
        {
            if (targetDesc == PropertyDescriptor.Undefined || targetDesc.Configurable)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            if (resultDesc.WritableSet && !resultDesc.Writable)
            {
                if (targetDesc.Writable)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm);
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
        if (!TryCallHandler(TrapSet, [_target, TypeConverter.ToPropertyKey(property), value, receiver], out var trapResult))
        {
            return _target.Set(property, value, receiver);
        }

        var result = TypeConverter.ToBoolean(trapResult);
        if (!result)
        {
            return false;
        }

        var targetDesc = _target.GetOwnProperty(property);
        if (targetDesc != PropertyDescriptor.Undefined)
        {
            if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && !targetDesc.Writable)
            {
                var targetValue = targetDesc.Value;
                if (!SameValue(targetValue, value))
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, $"'set' on proxy: trap returned truish for property '{property}' which exists in the proxy target as a non-configurable and non-writable data property with a different value");
                }
            }

            if (targetDesc.IsAccessorDescriptor() && !targetDesc.Configurable)
            {
                if ((targetDesc.Set ?? Undefined).IsUndefined())
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, $"'set' on proxy: trap returned truish for property '{property}' which exists in the proxy target as a non-configurable and non-writable accessor property without a setter");
                }
            }
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-defineownproperty-p-desc
    /// </summary>
    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        var arguments = new[] { _target, TypeConverter.ToPropertyKey(property), PropertyDescriptor.FromPropertyDescriptor(_engine, desc, strictUndefined: true) };
        if (!TryCallHandler(TrapDefineProperty, arguments, out var result))
        {
            return _target.DefineOwnProperty(property, desc);
        }

        var success = TypeConverter.ToBoolean(result);
        if (!success)
        {
            return false;
        }

        var targetDesc = _target.GetOwnProperty(property);
        var extensibleTarget = _target.Extensible;
        var settingConfigFalse = desc.ConfigurableSet && !desc.Configurable;

        if (targetDesc == PropertyDescriptor.Undefined)
        {
            if (!extensibleTarget || settingConfigFalse)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
        }
        else
        {
            if (!IsCompatiblePropertyDescriptor(extensibleTarget, desc, targetDesc))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
            if (targetDesc.Configurable && settingConfigFalse)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && targetDesc.Writable)
            {
                if (desc.WritableSet && !desc.Writable)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm);
                }
            }
        }

        return true;
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
        if (!TryCallHandler(TrapHas, [_target, TypeConverter.ToPropertyKey(property)], out var jsValue))
        {
            return _target.HasProperty(property);
        }

        var trapResult = TypeConverter.ToBoolean(jsValue);

        if (!trapResult)
        {
            var targetDesc = _target.GetOwnProperty(property);
            if (targetDesc != PropertyDescriptor.Undefined)
            {
                if (!targetDesc.Configurable)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm);
                }

                if (!_target.Extensible)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm);
                }
            }
        }

        return trapResult;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-delete-p
    /// </summary>
    public override bool Delete(JsValue property)
    {
        if (!TryCallHandler(TrapDeleteProperty, [_target, TypeConverter.ToPropertyKey(property)], out var result))
        {
            return _target.Delete(property);
        }

        var booleanTrapResult = TypeConverter.ToBoolean(result);

        if (!booleanTrapResult)
        {
            return false;
        }

        var targetDesc = _target.GetOwnProperty(property);

        if (targetDesc == PropertyDescriptor.Undefined)
        {
            return true;
        }

        if (!targetDesc.Configurable)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, $"'deleteProperty' on proxy: trap returned truish for property '{property}' which is non-configurable in the proxy target");
        }

        if (!_target.Extensible)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-preventextensions
    /// </summary>
    public override bool PreventExtensions()
    {
        if (!TryCallHandler(TrapPreventExtensions, [_target], out var result))
        {
            return _target.PreventExtensions();
        }

        var success = TypeConverter.ToBoolean(result);

        if (success && _target.Extensible)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
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
            if (!TryCallHandler(TrapIsExtensible, [_target], out var result))
            {
                return _target.Extensible;
            }

            var booleanTrapResult = TypeConverter.ToBoolean(result);
            var targetResult = _target.Extensible;
            if (booleanTrapResult != targetResult)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
            return booleanTrapResult;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getprototypeof
    /// </summary>
    protected internal override ObjectInstance? GetPrototypeOf()
    {
        if (!TryCallHandler(TrapGetProtoTypeOf, [_target], out var handlerProto))
        {
            return _target.Prototype;
        }

        if (!handlerProto.IsObject() && !handlerProto.IsNull())
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "'getPrototypeOf' on proxy: trap returned neither object nor null");
        }

        if (_target.Extensible)
        {
            return (ObjectInstance) handlerProto;
        }

        if (!ReferenceEquals(handlerProto, _target.Prototype))
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        return (ObjectInstance) handlerProto;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-setprototypeof-v
    /// </summary>
    internal override bool SetPrototypeOf(JsValue value)
    {
        if (!TryCallHandler(TrapSetProtoTypeOf, [_target, value], out var result))
        {
            return _target.SetPrototypeOf(value);
        }

        var success = TypeConverter.ToBoolean(result);

        if (!success)
        {
            return false;
        }

        if (_target.Extensible)
        {
            return true;
        }

        if (!ReferenceEquals(value, _target.Prototype))
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        return true;
    }

    internal override bool IsCallable { get; }

    private bool TryCallHandler(JsValue propertyName, JsCallArguments arguments, out JsValue result)
    {
        AssertNotRevoked(propertyName);

        result = Undefined;
        var handlerFunction = _handler!.Get(propertyName);
        if (!handlerFunction.IsNullOrUndefined())
        {
            var callable = handlerFunction as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, $"{_handler} returned for property '{propertyName}' of object '{_target}' is not a function");
            }

            result = callable.Call(_handler, arguments);
            return true;
        }

        return false;
    }

    private void AssertNotRevoked(JsValue key)
    {
        if (_handler is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, $"Cannot perform '{key}' on a proxy that has been revoked");
        }
    }

    private void AssertTargetNotRevoked(JsValue key)
    {
        if (_target is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, $"Cannot perform '{key}' on a proxy that has been revoked");
        }
    }

    public override string ToString() => IsCallable ? "function () { [native code] }" : base.ToString();
}
