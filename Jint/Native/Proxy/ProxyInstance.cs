using System.Collections.Generic;

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Proxy
{
    public class ProxyInstance : ObjectInstance, IConstructor, ICallable
    {
        internal ObjectInstance _target;
        internal ObjectInstance _handler;

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

        public ProxyInstance(
            Engine engine,
            ObjectInstance target,
            ObjectInstance handler)
            : base(engine, target.Class)
        {
            _target = target;
            _handler = handler;
        }

        public JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var jsValues = new[] { _target, thisObject, _engine.Array.Construct(arguments) };
            if (TryCallHandler(TrapApply, jsValues, out var result))
            {
                return result;
            }

            if (!(_target is ICallable callable))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, _target + " is not a function");
            }

            return callable.Call(thisObject, arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var argArray = _engine.Array.Construct(arguments, _engine.Array);

            if (!TryCallHandler(TrapConstruct, new[] { _target, argArray, newTarget }, out var result))
            {
                if (!(_target is IConstructor constructor))
                {
                    return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine);
                }
                return constructor.Construct(arguments, newTarget);
            }

            if (!(result is ObjectInstance oi))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine);
            }
           
            return oi;
        }

        public override bool IsArray()
        {
            AssertNotRevoked(KeyIsArray);
            return _target.IsArray();
        }

        internal override bool IsConstructor => 
            _handler != null
            && _handler.TryGetValue(TrapConstruct, out var handlerFunction) 
            && handlerFunction is IConstructor;

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (property == KeyFunctionRevoke || !TryCallHandler(TrapGet, new JsValue[] {_target, property, this}, out var result))
            {
                AssertTargetNotRevoked(property);
                return _target.Get(property, receiver);
            }

            AssertTargetNotRevoked(property);
            var targetDesc = _target.GetOwnProperty(property);
            if (targetDesc != PropertyDescriptor.Undefined)
            {
                if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && !targetDesc.Writable && !ReferenceEquals(result, targetDesc._value))
                {
                   ExceptionHelper.ThrowTypeError(_engine);
                }
                if (targetDesc.IsAccessorDescriptor() && !targetDesc.Configurable && targetDesc.Get.IsUndefined() && !result.IsUndefined())
                {
                   ExceptionHelper.ThrowTypeError(_engine, $"'get' on proxy: property '{property}' is a non-configurable accessor property on the proxy target and does not have a getter function, but the trap did not return 'undefined' (got '{result}')");
                }
            }

            return result;
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types)
        {
            if (!TryCallHandler(TrapOwnKeys, new JsValue[] {_target }, out var result))
            {
                return _target.GetOwnPropertyKeys(types);
            }

            var trapResult = new List<JsValue>(_engine.Function.PrototypeObject.CreateListFromArrayLike(result, Types.String | Types.Symbol));

            if (trapResult.Count != new HashSet<JsValue>(trapResult).Count)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var extensibleTarget = _target.Extensible;
            var targetKeys = _target.GetOwnPropertyKeys(types);
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
                    ExceptionHelper.ThrowTypeError(_engine);
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
                    ExceptionHelper.ThrowTypeError(_engine);
                }
            }

            if (uncheckedResultKeys.Count > 0)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return trapResult;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (!TryCallHandler(TrapGetOwnPropertyDescriptor, new[] { _target, property }, out var result))
            {
                return _target.GetOwnProperty(property);
            }

            if (!result.IsObject() && !result.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var targetDesc = _target.GetOwnProperty(property);

            if (result.IsUndefined())
            {
                if (targetDesc == PropertyDescriptor.Undefined)
                {
                    return targetDesc;
                }

                if (!targetDesc.Configurable || !_target.Extensible)
                {
                    ExceptionHelper.ThrowTypeError(_engine);
                }

                return PropertyDescriptor.Undefined;
            }

            var extensibleTarget = _target.Extensible;
            var resultDesc = PropertyDescriptor.ToPropertyDescriptor(_engine, result);

            var valid = IsCompatiblePropertyDescriptor(extensibleTarget, resultDesc, targetDesc);
            if (!valid)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            if (!resultDesc.Configurable && (targetDesc == PropertyDescriptor.Undefined || targetDesc.Configurable))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return resultDesc;
        }

        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            if (!TryCallHandler(TrapSet, new[] { _target, property, value, this }, out var trapResult))
            {
                return _target.Set(property, value, receiver);
            }

            var result = TypeConverter.ToBoolean(trapResult);
            if (!result)
            {
                return false;
            }

            var targetDesc  = _target.GetOwnProperty(property);
            if (targetDesc != PropertyDescriptor.Undefined)
            {
                if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && !targetDesc.Writable)
                {
                    if (targetDesc.Value != value)
                    {
                        ExceptionHelper.ThrowTypeError(_engine);
                    }
                }

                if (targetDesc.IsAccessorDescriptor() && !targetDesc.Configurable)
                {
                    if (targetDesc.Set.IsUndefined())
                    {
                        ExceptionHelper.ThrowTypeError(_engine);
                    }
                }
            }
            
            return true;
        }

        public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            var arguments = new[] { _target, property, PropertyDescriptor.FromPropertyDescriptor(_engine, desc) };
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
            var settingConfigFalse = !desc.Configurable;

            if (targetDesc == PropertyDescriptor.Undefined)
            {
                if (!extensibleTarget || settingConfigFalse)
                {
                    ExceptionHelper.ThrowTypeError(_engine);
                }
            }
            else
            {
                if (!IsCompatiblePropertyDescriptor(extensibleTarget, desc, targetDesc))
                {
                    ExceptionHelper.ThrowTypeError(_engine);
                }
                if (targetDesc.Configurable && settingConfigFalse)
                {
                    ExceptionHelper.ThrowTypeError(_engine);
                }
            }

            return true;
        }

        private static bool IsCompatiblePropertyDescriptor(bool extensible, PropertyDescriptor desc, PropertyDescriptor current)
        {
            return ValidateAndApplyPropertyDescriptor(null, JsString.Empty, extensible, desc, current);
        }

        public override bool HasProperty(JsValue property)
        {
            if (!TryCallHandler(TrapHas, new [] { _target, property }, out var jsValue))
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
                        ExceptionHelper.ThrowTypeError(_engine);
                    }

                    if (!_target.Extensible)
                    {
                        ExceptionHelper.ThrowTypeError(_engine);
                    }
                }
            }

            return trapResult;
        }

        public override bool Delete(JsValue property)
        {
            if (!TryCallHandler(TrapDeleteProperty, new JsValue[] { _target, property }, out var result))
            {
                return _target.Delete(property);
            }

            var success = TypeConverter.ToBoolean(result);

            if (success)
            {
                var targetDesc = _target.GetOwnProperty(property);
                if (targetDesc != PropertyDescriptor.Undefined && !targetDesc.Configurable)
                {
                    ExceptionHelper.ThrowTypeError(_engine, $"'deleteProperty' on proxy: trap returned truish for property '{property}' which is non-configurable in the proxy target");
                }
            }

            return success;
        }

        public override JsValue PreventExtensions()
        {
            if (!TryCallHandler(TrapPreventExtensions, new[] { _target }, out var result))
            {
                return _target.PreventExtensions();
            }

            var success = TypeConverter.ToBoolean(result);

            if (success && _target.Extensible)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return success ? JsBoolean.True : JsBoolean.False;
        }

        public override bool Extensible
        {
            get
            {
                if (!TryCallHandler(TrapIsExtensible, new[] { _target }, out var result))
                {
                    return _target.Extensible;
                }

                var booleanTrapResult = TypeConverter.ToBoolean(result);
                var targetResult = _target.Extensible;
                if (booleanTrapResult != targetResult)
                {
                    ExceptionHelper.ThrowTypeError(_engine);
                }
                return booleanTrapResult;
            }
        }

        protected internal override ObjectInstance GetPrototypeOf()
        {
            if (!TryCallHandler(TrapGetProtoTypeOf, new [] { _target }, out var handlerProto ))
            {
                return _target.Prototype;
            }

            if (!handlerProto.IsObject() && !handlerProto.IsNull())
            {
                ExceptionHelper.ThrowTypeError(_engine, "'getPrototypeOf' on proxy: trap returned neither object nor null");
            }

            if (_target.Extensible)
            {
                return (ObjectInstance) handlerProto;
            }

            if (!ReferenceEquals(handlerProto, _target.Prototype))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return (ObjectInstance) handlerProto;
        }

        public override bool SetPrototypeOf(JsValue value)
        {
            if (!TryCallHandler(TrapSetProtoTypeOf, new[] { _target, value }, out var result))
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
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return true;
        }

        internal override bool IsCallable => _target is ICallable;

        private bool TryCallHandler(JsValue propertyName, JsValue[] arguments, out JsValue result)
        {
            AssertNotRevoked(propertyName);

            result = Undefined;
            var handlerFunction = _handler.Get(propertyName);
            if (!handlerFunction.IsNullOrUndefined())
            {
                if (!(handlerFunction is ICallable callable))
                {
                    return ExceptionHelper.ThrowTypeError<bool>(_engine, $"{_handler} returned for property '{propertyName}' of object '{_target}' is not a function");
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
                ExceptionHelper.ThrowTypeError(_engine, $"Cannot perform '{key}' on a proxy that has been revoked");
            }
        }

        private void AssertTargetNotRevoked(JsValue key)
        {
            if (_target is null)
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Cannot perform '{key}' on a proxy that has been revoked");
            }
        }
        
        public override string ToString() => "function () { [native code] }";
    }
}