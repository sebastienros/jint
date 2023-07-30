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
    /// https://tc39.es/ecma262/#sec-properties-of-the-function-prototype-object
    /// </summary>
    internal sealed class FunctionPrototype : FunctionInstance
    {
        internal FunctionPrototype(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype)
            : base(engine, realm, JsString.Empty)
        {
            _prototype = objectPrototype;
            _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(7, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_realm.Intrinsics.Function, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "toString", ToString, 0, lengthFlags), propertyFlags),
                ["apply"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "apply", Apply, 2, lengthFlags), propertyFlags),
                ["call"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "call", CallImpl, 1, lengthFlags), propertyFlags),
                ["bind"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "bind", Bind, 1, lengthFlags), propertyFlags),
                ["arguments"] = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(_engine, PropertyFlag.Configurable | PropertyFlag.CustomJsValue),
                ["caller"] = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(_engine, PropertyFlag.Configurable | PropertyFlag.CustomJsValue)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.HasInstance] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "[Symbol.hasInstance]", HasInstance, 1, PropertyFlag.Configurable), PropertyFlag.AllForbidden)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function.prototype-@@hasinstance
        /// </summary>
        private static JsValue HasInstance(JsValue thisObject, JsValue[] arguments)
        {
            return thisObject.OrdinaryHasInstance(arguments.At(0));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function.prototype.bind
        /// </summary>
        private JsValue Bind(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject is not (ICallable and ObjectInstance oi))
            {
                ExceptionHelper.ThrowTypeError(_realm, "Bind must be called on a function");
                return default;
            }

            var thisArg = arguments.At(0);
            var f = BoundFunctionCreate(oi, thisArg, arguments.Skip(1));

            JsNumber l;
            var targetHasLength = oi.HasOwnProperty(CommonProperties.Length) == true;
            if (targetHasLength)
            {
                var targetLen = oi.Get(CommonProperties.Length);
                if (targetLen is not JsNumber number)
                {
                    l = JsNumber.PositiveZero;
                }
                else
                {
                    if (number.IsPositiveInfinity())
                    {
                        l = number;
                    }
                    else if (number.IsNegativeInfinity())
                    {
                        l = JsNumber.PositiveZero;
                    }
                    else
                    {
                        var targetLenAsInt = (long) TypeConverter.ToIntegerOrInfinity(targetLen);
                        // first argument is target
                        var argumentsLength = System.Math.Max(0, arguments.Length - 1);
                        l = JsNumber.Create((ulong) System.Math.Max(targetLenAsInt - argumentsLength, 0));
                    }
                }
            }
            else
            {
                l = JsNumber.PositiveZero;
            }

            f.DefinePropertyOrThrow(CommonProperties.Length, new PropertyDescriptor(l, PropertyFlag.Configurable));

            var targetName = oi.Get(CommonProperties.Name);
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
        private BindFunctionInstance BoundFunctionCreate(ObjectInstance targetFunction, JsValue boundThis, JsValue[] boundArgs)
        {
            var proto = targetFunction.GetPrototypeOf();
            var obj = new BindFunctionInstance(_engine, _realm, proto, targetFunction, boundThis, boundArgs);
            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function.prototype.tostring
        /// </summary>
        private JsValue ToString(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsObject() && thisObject.IsCallable)
            {
                return thisObject.ToString();
            }

            ExceptionHelper.ThrowTypeError(_realm, "Function.prototype.toString requires that 'this' be a Function");
            return null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function.prototype.apply
        /// </summary>
        private JsValue Apply(JsValue thisObject, JsValue[] arguments)
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

            var argList = CreateListFromArrayLike(_realm, argArray);

            var result = func.Call(thisArg, argList);

            return result;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createlistfromarraylike
        /// </summary>
        internal static JsValue[] CreateListFromArrayLike(Realm realm, JsValue argArray, Types? elementTypes = null)
        {
            var argArrayObj = argArray as ObjectInstance;
            if (argArrayObj is null)
            {
                ExceptionHelper.ThrowTypeError(realm);
            }
            var operations = ArrayOperations.For(argArrayObj);
            var argList = elementTypes is null ? operations.GetAll() : operations.GetAll(elementTypes.Value);
            return argList;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function.prototype.call
        /// </summary>
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

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Undefined;
        }
    }
}
