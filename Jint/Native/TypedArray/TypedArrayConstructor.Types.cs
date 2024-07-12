using System.Globalization;
using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.TypedArray
{
    public sealed class Int8ArrayConstructor : TypedArrayConstructor
    {
        internal Int8ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Int8)
        {
        }

        public JsTypedArray Construct(sbyte[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint8ArrayConstructor : TypedArrayConstructor
    {
        internal Uint8ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint8)
        {
        }

        protected override void Initialize()
        {
            const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(1, checkExistingKeys: false)
            {
                ["BYTES_PER_ELEMENT"] = new(new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.AllForbidden)),
                ["fromBase64"] = new(new ClrFunction(Engine, "fromBase64", FromBase64, 1, PropertyFlag.Configurable), PropertyFlags),
                ["fromHex"] = new(new ClrFunction(Engine, "fromHex", FromHex, 1, PropertyFlag.Configurable), PropertyFlags),
            };
            SetProperties(properties);
        }

        public JsTypedArray Construct(byte[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }

        private JsTypedArray FromBase64(JsValue thisObject, JsValue[] arguments)
        {
            var s = arguments.At(0);

            if (!s.IsString())
            {
                ExceptionHelper.ThrowTypeError(_realm, "fromBase64 must be called with a string");
            }

            var opts = GetOptionsObject(_engine, arguments.At(1));
            var alphabet = GetAndValidateAlphabetOption(_engine, opts);
            var lastChunkHandling = GetAndValidateLastChunkHandling(_engine, opts);

            var result = FromBase64(s.ToString(), alphabet.ToString(), lastChunkHandling.ToString());
            if (result.Error is not null)
            {
                throw result.Error;
            }

            var resultLength = result.Bytes.Length;
            var ta = AllocateTypedArray(resultLength);
            ta._viewedArrayBuffer._arrayBufferData = result.Bytes;
            return ta;
        }

        internal static JsString GetAndValidateLastChunkHandling(Engine engine, ObjectInstance opts)
        {
            var lastChunkHandling = opts.Get("lastChunkHandling");
            if (lastChunkHandling.IsUndefined())
            {
                lastChunkHandling = "loose";
            }

            if (lastChunkHandling is not JsString s || (s != "loose" && s != "strict" && s != "stop-before-partial"))
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "lastChunkHandling must be either 'loose', 'strict' or 'stop-before-partial'");
                return default;
            }

            return s;
        }

        internal static JsString GetAndValidateAlphabetOption(Engine engine, ObjectInstance opts)
        {
            var alphabet = opts.Get("alphabet");
            if (alphabet.IsUndefined())
            {
                alphabet = "base64";
            }

            if (alphabet is not JsString s || (s != "base64" && s != "base64url"))
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "alphabet must be either 'base64' or 'base64url'");
                return default;
            }

            return s;
        }

        internal readonly record struct FromEncodingResult(byte[] Bytes, JavaScriptException? Error, int Read);

        internal static FromEncodingResult FromBase64(string s, string alphabet, string lastChunkHandling, uint? maxLength = null)
        {
            throw new NotImplementedException();
        }

        private JsTypedArray FromHex(JsValue thisObject, JsValue[] arguments)
        {
            var s = arguments.At(0);

            if (!s.IsString())
            {
                ExceptionHelper.ThrowTypeError(_realm, "fromHex must be called with a string");
            }

            var result = FromHex(_engine, s.ToString());
            if (result.Error is not null)
            {
                throw result.Error;
            }

            var resultLength = result.Bytes.Length;
            var ta = AllocateTypedArray(resultLength);
            ta._viewedArrayBuffer._arrayBufferData = result.Bytes;
            return  ta;
        }

        internal static FromEncodingResult FromHex(Engine engine, string s, uint maxLength = int.MaxValue)
        {
            var length = s.Length;
            var bytes = new byte[length / 2];
            var read = 0;

            if (length % 2 != 0)
            {
                return new FromEncodingResult(bytes, ExceptionHelper.CreateSyntaxError(engine.Realm, "Invalid hex string"), read);
            }

            const string Allowed = "0123456789abcdefABCDEF";
            while (read < length && bytes.Length < maxLength)
            {
                var hexits = s.AsSpan(read, 2);
                if (!Allowed.Contains(hexits[0]) || !Allowed.Contains(hexits[1]))
                {
                    return new FromEncodingResult(bytes, ExceptionHelper.CreateSyntaxError(engine.Realm, "Invalid hex value"), read);
                }

#if SUPPORTS_SPAN_PARSE
                var b  = byte.Parse(hexits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#else
                var b  = byte.Parse(hexits.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#endif
                bytes[read / 2] = b;
                read += 2;
            }
            return new FromEncodingResult(bytes, Error: null, read);
        }

        internal static ObjectInstance GetOptionsObject(Engine engine, JsValue options)
        {
            if (options.IsUndefined())
            {
                return new JsObject(engine);
            }

            if (options.IsObject())
            {
                return options.AsObject();
            }

            ExceptionHelper.ThrowTypeError(engine.Realm, "Invalid options");
            return default;
        }
    }

    public sealed class Uint8ClampedArrayConstructor : TypedArrayConstructor
    {
        internal Uint8ClampedArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint8C)
        {
        }

        public JsTypedArray Construct(byte[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Int16ArrayConstructor : TypedArrayConstructor
    {
        internal Int16ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Int16)
        {
        }

        public JsTypedArray Construct(short[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint16ArrayConstructor : TypedArrayConstructor
    {
        internal Uint16ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint16)
        {
        }

        public JsTypedArray Construct(ushort[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Int32ArrayConstructor : TypedArrayConstructor
    {
        internal Int32ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Int32)
        {
        }

        public JsTypedArray Construct(int[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint32ArrayConstructor : TypedArrayConstructor
    {
        internal Uint32ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint32)
        {
        }

        public JsTypedArray Construct(uint[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Float16ArrayConstructor : TypedArrayConstructor
    {
        internal Float16ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Float16)
        {
        }

#if SUPPORTS_HALF
        public JsTypedArray Construct(Half[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            for (var i = 0; i < values.Length; ++i)
            {
                array.DoIntegerIndexedElementSet(i, values[i]);
            }
            return array;
        }
#endif
    }

    public sealed class Float32ArrayConstructor : TypedArrayConstructor
    {
        internal Float32ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Float32)
        {
        }

        public JsTypedArray Construct(float[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Float64ArrayConstructor : TypedArrayConstructor
    {
        internal Float64ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Float64)
        {
        }

        public JsTypedArray Construct(double[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class BigInt64ArrayConstructor : TypedArrayConstructor
    {
        internal BigInt64ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.BigInt64)
        {
        }

        public JsTypedArray Construct(long[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class BigUint64ArrayConstructor : TypedArrayConstructor
    {
        internal BigUint64ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.BigUint64)
        {
        }

        public JsTypedArray Construct(ulong[] values)
        {
            var array = (JsTypedArray) base.Construct([values.Length], this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }
}
