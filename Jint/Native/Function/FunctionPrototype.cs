using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Function
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.4
    /// </summary>
    public sealed class FunctionPrototype : FunctionInstance
    {
        internal FunctionPrototype(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype)
            : base(engine, realm, JsString.Empty)
        {
            _prototype = objectPrototype;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(7, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_realm.Intrinsics.Function, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString, 0, lengthFlags), propertyFlags),
                ["apply"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "apply", Apply, 2, lengthFlags), propertyFlags),
                ["call"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "call", CallImpl, 1, lengthFlags), propertyFlags),
                ["bind"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "bind", Bind, 1, PropertyFlag.AllForbidden), propertyFlags),
                ["arguments"] = _engine._callerCalleeArgumentsThrowerConfigurable,
                ["caller"] = _engine._callerCalleeArgumentsThrowerConfigurable
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.HasInstance] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "[Symbol.hasInstance]", HasInstance, 1, PropertyFlag.Configurable), PropertyFlag.AllForbidden)
            };
            SetSymbols(symbols);
        }

        private static JsValue HasInstance(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is not FunctionInstance f)
            {
                return false;
            }

            return f.OrdinaryHasInstance(arguments.At(0));
        }

        private JsValue Bind(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is not ICallable)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Bind must be called on a function");
            }

            var thisArg = arguments.At(0);
            var f = BoundFunctionCreate((ObjectInstance) thisObj, thisArg, arguments.Skip(1));

            JsNumber l;
            var targetHasLength = thisObj.HasOwnProperty(CommonProperties.Length);
            if (targetHasLength)
            {
                var targetLen = thisObj.Get(CommonProperties.Length);
                if (!targetLen.IsNumber())
                {
                    l = JsNumber.PositiveZero;
                }
                else
                {
                    targetLen = TypeConverter.ToInteger(targetLen);
                    // first argument is target
                    var argumentsLength = System.Math.Max(0, arguments.Length - 1);
                    l = JsNumber.Create((uint) System.Math.Max(((JsNumber) targetLen)._value - argumentsLength, 0));
                }
            }
            else
            {
                l = JsNumber.PositiveZero;
            }

            f._length = new PropertyDescriptor(l, PropertyFlag.Configurable);

            var targetName = thisObj.Get(CommonProperties.Name);
            if (!targetName.IsString())
            {
                targetName = JsString.Empty;
            }

            f.SetFunctionName(targetName, "bound");

            return f;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-boundfunctioncreate
        /// </summary>
        private FunctionInstance BoundFunctionCreate(ObjectInstance targetFunction, JsValue boundThis, JsValue[] boundArgs)
        {
            var proto = targetFunction.GetPrototypeOf();
            var obj = new BindFunctionInstance(_engine, _realm)
            {
                _prototype = proto,
                TargetFunction = targetFunction,
                BoundThis = boundThis,
                BoundArgs = boundArgs
            };
            return obj;
        }

        private JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj.IsObject() && thisObj.IsCallable)
            {
                return thisObj.ToString();
            }

            ExceptionHelper.ThrowTypeError(_realm, "Function.prototype.toString requires that 'this' be a Function");
            return null;
        }

        internal JsValue Apply(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject as ICallable;
            if (func is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            var thisArg = arguments.At(0);
            var argArray = arguments.At(1);

            if (argArray.IsNullOrUndefined())
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argList = CreateListFromArrayLike(argArray);

            var result = func.Call(thisArg, argList);

            return result;
        }

        internal JsValue[] CreateListFromArrayLike(JsValue argArray, Types? elementTypes = null)
        {
            var argArrayObj = argArray as ObjectInstance;
            if (argArrayObj is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            var operations = ArrayOperations.For(argArrayObj);
            var allowedTypes = elementTypes ??
                               Types.Undefined | Types.Null | Types.Boolean | Types.String | Types.Symbol | Types.Number | Types.Object;

            var argList = operations.GetAll(allowedTypes);
            return argList;
        }

        private JsValue CallImpl(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject as ICallable;
            if (func is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            JsValue[] values = System.Array.Empty<JsValue>();
            if (arguments.Length > 1)
            {
                values = new JsValue[arguments.Length - 1];
                System.Array.Copy(arguments, 1, values, 0, arguments.Length - 1);
            }

            var result = func.Call(arguments.At(0), values);

            return result;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Undefined;
        }
    }
}