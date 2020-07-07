using System.Collections.Generic;
using System.Threading.Tasks;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.String
{
    public sealed class StringConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("String");

        public StringConstructor(Engine engine)
            : base(engine, _functionName, FunctionThisMode.Global)
        {
        }

        public static StringConstructor CreateStringConstructor(Engine engine)
        {
            var obj = new StringConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the String constructor is the Function prototype object
            obj.PrototypeObject = StringPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(JsNumber.One, PropertyFlag.Configurable);

            // The initial value of String.prototype is the String prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(3, checkExistingKeys: false)
            {
                ["fromCharCode"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "fromCharCode", FromCharCode, 1), PropertyFlag.NonEnumerable)),
                ["fromCodePoint"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "fromCodePoint", FromCodePoint, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)),
                ["raw"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "raw", Raw, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable))
            };
            SetProperties(properties);
        }

        private static JsValue FromCharCode(JsValue thisObj, JsValue[] arguments)
        {
            var chars = new char[arguments.Length];
            for (var i = 0; i < chars.Length; i++ )
            {
                chars[i] = (char)TypeConverter.ToUint16(arguments[i]);
            }

            return JsString.Create(new string(chars));
        }

        private JsValue FromCodePoint(JsValue thisObj, JsValue[] arguments)
        {
            var codeUnits = new List<JsValue>();
            string result = "";
            for (var i = 0; i < arguments.Length; i++ )
            {
                var codePoint = TypeConverter.ToNumber(arguments[i]);
                if (codePoint < 0
                    || codePoint > 0x10FFFF
                    || double.IsInfinity(codePoint)
                    || double.IsNaN(codePoint)
                    || TypeConverter.ToInt32(codePoint) != codePoint)
                {
                    return ExceptionHelper.ThrowRangeError<JsValue>(_engine, "Invalid code point " + codePoint);
                }

                var point = (uint) codePoint;
                if (point <= 0xFFFF)
                {
                    // BMP code point
                    codeUnits.Add(JsNumber.Create(point));
                }
                else
                {
                    // Astral code point; split in surrogate halves
                    // https://mathiasbynens.be/notes/javascript-encoding#surrogate-formulae
                    point -= 0x10000;
                    codeUnits.Add(JsNumber.Create((point >> 10) + 0xD800)); // highSurrogate
                    codeUnits.Add(JsNumber.Create((point % 0x400) + 0xDC00)); // lowSurrogate
                }
                if (codeUnits.Count >= 0x3fff)
                {
                    result += FromCharCode(null, codeUnits.ToArray());
                    codeUnits.Clear();
                }
            }

            return result + FromCharCode(null, codeUnits.ToArray());
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/#sec-string.raw
        /// </summary>
        private JsValue Raw(JsValue thisObj, JsValue[] arguments)
        {
            var cooked = TypeConverter.ToObject(_engine, arguments.At(0));
            var raw = TypeConverter.ToObject(_engine, cooked.Get(JintTaggedTemplateExpression.PropertyRaw, cooked));

            var operations = ArrayOperations.For(raw);
            var length = operations.GetLength();

            if (length <= 0)
            {
                return JsString.Empty;
            }

            using (var result = StringBuilderPool.Rent())
            {
                for (var i = 0; i < length; i++)
                {
                    if (i > 0)
                    {
                        if (i < arguments.Length && !arguments[i].IsUndefined())
                        {
                            result.Builder.Append(TypeConverter.ToString(arguments[i]));
                        }
                    }

                    result.Builder.Append(TypeConverter.ToString(operations.Get((ulong) i)));
                }

                return result.ToString();
            }
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return JsString.Empty;
            }

            var arg = arguments[0];
            var str = arg is JsSymbol s
                ? s.ToString()
                : TypeConverter.ToString(arg);

            return JsString.Create(str);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            string value = "";
            if (arguments.Length > 0)
            {
                value = TypeConverter.ToString(arguments[0]);
            }
            return Construct(value);
        }

        public StringPrototype PrototypeObject { get; private set; }

        public StringInstance Construct(string value)
        {
            return Construct(JsString.Create(value));
        }

        public StringInstance Construct(JsString value)
        {
            var instance = new StringInstance(Engine)
            {
                _prototype = PrototypeObject,
                PrimitiveValue = value,
                _length = PropertyDescriptor.AllForbiddenDescriptor.ForNumber(value.Length)
            };

            return instance;
        }

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}
