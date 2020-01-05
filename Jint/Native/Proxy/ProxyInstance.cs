using System.Collections.Generic;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Proxy
{
    public class ProxyInstance : FunctionInstance, IConstructor
    {
        internal ObjectInstance _target;
        internal ObjectInstance _handler;

        private static readonly Key TrapApply = "apply";
        private static readonly Key TrapGet = "get";
        private static readonly Key TrapSet = "set";
        private static readonly Key TrapPreventExtensions = "preventExtensions";
        private static readonly Key TrapIsExtensible = "isExtensible";
        private static readonly Key TrapDefineProperty = "defineProperty";
        private static readonly Key TrapDeleteProperty = "deleteProperty";
        private static readonly Key TrapGetOwnPropertyDescriptor = "getOwnPropertyDescriptor";
        private static readonly Key TrapHas = "has";
        private static readonly Key TrapGetProtoTypeOf = "getPrototypeOf";
        private static readonly Key TrapSetProtoTypeOf = "setPrototypeOf";
        private static readonly Key TrapOwnKeys = "ownKeys";
        private static readonly Key TrapConstruct = "construct";

        private static readonly Key KeyFunctionRevoke = "revoke";

        public ProxyInstance(
            Engine engine,
            ObjectInstance target,
            ObjectInstance handler)
            : base(engine, JsString.Empty, false, "Proxy")
        {
            _target = target;
            _handler = handler;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var jsValues = new[] { _target, thisObject, _engine.Array.Construct(arguments) };
            if (TryCallHandler(TrapApply, jsValues, out var result))
            {
                return result;
            }

            return ((ICallable) _target).Call(thisObject, arguments);
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
            if (_handler is null)
            {
                return ExceptionHelper.ThrowTypeError<bool>(_engine);
            }
            return _target.IsArray();
        }

        internal override bool IsConstructor => 
            _handler.TryGetValue(TrapConstruct, out var handlerFunction) && handlerFunction is IConstructor;

        public override JsValue Get(in Key propertyName, JsValue receiver)
        {
            if (propertyName == KeyFunctionRevoke || !TryCallHandler(TrapGet, new JsValue[] {_target, propertyName, this}, out var result))
            {
                AssertTargetNotRevoked(propertyName);
                return _target.Get(in propertyName, receiver);
            }

            AssertTargetNotRevoked(propertyName);
            var targetDesc = _target.GetOwnProperty(propertyName);
            if (targetDesc != PropertyDescriptor.Undefined)
            {
                if (targetDesc.IsDataDescriptor() && !targetDesc.Configurable && !targetDesc.Writable && !ReferenceEquals(result, targetDesc._value))
                {
                   ExceptionHelper.ThrowTypeError(_engine);
                }
                if (targetDesc.IsAccessorDescriptor() && !targetDesc.Configurable && targetDesc.Get.IsUndefined() && !result.IsUndefined())
                {
                   ExceptionHelper.ThrowTypeError(_engine, $"'get' on proxy: property '{propertyName}' is a non-configurable accessor property on the proxy target and does not have a getter function, but the trap did not return 'undefined' (got '{result}')");
                }
            }

            return result;
        }

        internal override List<JsValue> GetOwnPropertyKeys(Types types)
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
            var targetConfigurableKeys = new List<Key>();
            var targetNonconfigurableKeys = new List<Key>();

            foreach (var jsValue in targetKeys)
            {
                var key = jsValue.ToPropertyKey();
                var desc = _target.GetOwnProperty(key);
                if (desc != PropertyDescriptor.Undefined && !desc.Configurable)
                {
                    targetNonconfigurableKeys.Add(key);
                }
                else
                {
                    targetConfigurableKeys.Add(key);
                }
                
            }

            if (extensibleTarget && targetNonconfigurableKeys.Count == 0)
            {
                return trapResult;
            }
            
            var uncheckedResultKeys = new HashSet<Key>();
            foreach (var jsValue in trapResult)
            {
                uncheckedResultKeys.Add(jsValue.ToPropertyKey());
            }

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

        public override PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            if (!TryCallHandler(TrapGetOwnPropertyDescriptor, new JsValue[] {_target, propertyName, this}, out var result))
            {
                return _target.GetOwnProperty(propertyName);
            }

            if (!result.IsObject() && !result.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var targetDesc = _target.GetOwnProperty(propertyName);

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

        public override bool Set(in Key propertyName, JsValue value, JsValue receiver)
        {
            if (!TryCallHandler(TrapSet, new[] { _target, propertyName, value, this }, out var trapResult))
            {
                return _target.Set(propertyName, value, receiver);
            }

            var result = TypeConverter.ToBoolean(trapResult);
            if (!result)
            {
                return false;
            }

            var targetDesc  = _target.GetOwnProperty(propertyName);
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

        public override bool DefineOwnProperty(in Key propertyName, PropertyDescriptor desc)
        {
            var arguments = new[] { _target, propertyName, PropertyDescriptor.FromPropertyDescriptor(_engine, desc) };
            if (!TryCallHandler(TrapDefineProperty, arguments, out var result))
            {
                return _target.DefineOwnProperty(propertyName, desc);
            }

            var success = TypeConverter.ToBoolean(result);
            if (!success)
            {
                return false;
            }

            var targetDesc = _target.GetOwnProperty(propertyName);
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
            return ValidateAndApplyPropertyDescriptor(null, "", extensible, desc, current);
        }

        public override bool HasProperty(in Key propertyName)
        {
            if (!TryCallHandler(TrapHas, new JsValue[] { _target, propertyName }, out var jsValue))
            {
                return _target.HasProperty(propertyName);
            }

            var trapResult = TypeConverter.ToBoolean(jsValue);

            if (!trapResult)
            {
                var targetDesc = _target.GetOwnProperty(propertyName);
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

        public override bool Delete(in Key propertyName)
        {
            if (!TryCallHandler(TrapDeleteProperty, new JsValue[] { _target, propertyName }, out var result))
            {
                return _target.Delete(propertyName);
            }

            var success = TypeConverter.ToBoolean(result);

            if (success)
            {
                var targetDesc = _target.GetOwnProperty(propertyName);
                if (targetDesc != PropertyDescriptor.Undefined && !targetDesc.Configurable)
                {
                    ExceptionHelper.ThrowTypeError(_engine, $"'deleteProperty' on proxy: trap returned truish for property '{propertyName}' which is non-configurable in the proxy target");
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

        protected override ObjectInstance GetPrototypeOf()
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

        private bool TryCallHandler(in Key propertyName, JsValue[] arguments, out JsValue result)
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

        internal void AssertNotRevoked(in Key key)
        {
            if (_handler is null)
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Cannot perform '{key}' on a proxy that has been revoked");
            }
        }

        internal void AssertTargetNotRevoked(in Key key)
        {
            if (_target is null)
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Cannot perform '{key}' on a proxy that has been revoked");
            }
        }
    }
}