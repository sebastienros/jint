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
    /// <summary>
    /// https://tc39.es/ecma262/#sec-string-constructor
    /// </summary>
    internal sealed class StringConstructor : Constructor
    {
        private static readonly JsString _functionName = new JsString("String");

        public StringConstructor(
            Engine engine,
            Realm realm,
            FunctionPrototype functionPrototype,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            _prototype = functionPrototype;
            PrototypeObject = new StringPrototype(engine, realm, this, objectPrototype);
            _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        }

        public StringPrototype PrototypeObject { get; }

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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-string.fromcharcode
        /// </summary>
        private static JsValue FromCharCode(JsValue? thisObj, JsValue[] arguments)
        {
            var length = arguments.Length;

            if (length == 0)
            {
                return JsString.Empty;
            }

            if (arguments.Length == 1)
            {
                return JsString.Create((char) TypeConverter.ToUint16(arguments[0]));
            }

#if SUPPORTS_SPAN_PARSE
            var elements = length < 512 ? stackalloc char[length] : new char[length];
#else
            var elements = new char[length];
#endif
            for (var i = 0; i < elements.Length; i++ )
            {
                var nextCu = TypeConverter.ToUint16(arguments[i]);
                elements[i] = (char) nextCu;
            }

            return JsString.Create(new string(elements));
        }

        private JsValue FromCodePoint(JsValue thisObject, JsValue[] arguments)
        {
            var codeUnits = new List<JsValue>();
            string result = "";
            foreach (var a in arguments)
            {
                var codePoint = TypeConverter.ToNumber(a);
                if (codePoint < 0
                    || codePoint > 0x10FFFF
                    || double.IsInfinity(codePoint)
                    || double.IsNaN(codePoint)
                    || TypeConverter.ToInt32(codePoint) != codePoint)
                {
                    ExceptionHelper.ThrowRangeError(_realm, "Invalid code point " + codePoint);
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
        private JsValue Raw(JsValue thisObject, JsValue[] arguments)
        {
            var cooked = TypeConverter.ToObject(_realm, arguments.At(0));
            var raw = TypeConverter.ToObject(_realm, cooked.Get(JintTaggedTemplateExpression.PropertyRaw));

            var operations = ArrayOperations.For(raw);
            var length = operations.GetLength();

            if (length <= 0)
            {
                return JsString.Empty;
            }

            using var result = StringBuilderPool.Rent();
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

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
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
        /// https://tc39.es/ecma262/#sec-string-constructor-string-value
        /// </summary>
        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            JsString s;
            if (arguments.Length == 0)
            {
                s = JsString.Empty;
            }
            else
            {
                var value = arguments.At(0);
                if (newTarget.IsUndefined() && value.IsSymbol())
                {
                    return StringCreate(JsString.Create(((JsSymbol) value).ToString()), PrototypeObject);
                }
                s = TypeConverter.ToJsString(arguments[0]);
            }

            if (newTarget.IsUndefined())
            {
                return StringCreate(s, PrototypeObject);
            }

            return StringCreate(s, GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.String.PrototypeObject));
        }

        public StringInstance Construct(string value)
        {
            return Construct(JsString.Create(value));
        }

        public StringInstance Construct(JsString value)
        {
            return StringCreate(value, PrototypeObject);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-stringcreate
        /// </summary>
        private StringInstance StringCreate(JsString value, ObjectInstance prototype)
        {
            var instance = new StringInstance(Engine, value)
            {
                _prototype = prototype
            };

            return instance;
        }
    }
}
