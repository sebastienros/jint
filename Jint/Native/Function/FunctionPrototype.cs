using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.4
    /// </summary>
    public sealed class FunctionPrototype : FunctionInstance
    {
        private FunctionPrototype(Engine engine)
            : base(engine, JsString.Empty)
        {
        }

        public static FunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new FunctionPrototype(engine)
            {
                // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
                _prototype = engine.Object.PrototypeObject,
                _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(7, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(Engine.Function, PropertyFlag.NonEnumerable),
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
            if (!(thisObj is FunctionInstance f))
            {
                return false;
            }
            
            return f.HasInstance(arguments.At(0));
        }

        private JsValue Bind(JsValue thisObj, JsValue[] arguments)
        {
            if (!(thisObj is ICallable))
            {
                ExceptionHelper.ThrowTypeError(Engine, "Bind must be called on a function");
            }

            var thisArg = arguments.At(0);
            var f = new BindFunctionInstance(Engine)
            {
                TargetFunction = thisObj,
                BoundThis = thisObj is ArrowFunctionInstance ? Undefined : thisArg,
                BoundArgs = arguments.Skip(1),
                _prototype = Engine.Function.PrototypeObject,
            };

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

        private JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj.IsObject() && thisObj.IsCallable)
            {
                return thisObj.ToString();
            }

            return ExceptionHelper.ThrowTypeError<FunctionInstance>(_engine, "Function.prototype.toString requires that 'this' be a Function");
        }

        internal JsValue Apply(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(Engine);
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
            var argArrayObj = argArray as ObjectInstance ?? ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine);
            var operations = ArrayOperations.For(argArrayObj);
            var allowedTypes = elementTypes ??
                               Types.Undefined | Types.Null | Types.Boolean | Types.String | Types.Symbol | Types.Number | Types.Object;
            
            var argList = operations.GetAll(allowedTypes);
            return argList;
        }

        private JsValue CallImpl(JsValue thisObject, JsValue[] arguments)
        {
            var func = thisObject as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(Engine);
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

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}