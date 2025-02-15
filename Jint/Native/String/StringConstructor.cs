#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using System.Text;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.String;

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
            ["fromCharCode"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "fromCharCode", FromCharCode, 1), PropertyFlag.NonEnumerable)),
            ["fromCodePoint"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "fromCodePoint", FromCodePoint, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)),
            ["raw"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "raw", Raw, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable))
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.fromcharcode
    /// </summary>
    private static JsValue FromCharCode(JsValue? thisObj, JsCallArguments arguments)
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.fromcodepoint
    /// </summary>
    private JsValue FromCodePoint(JsValue thisObject, JsCallArguments arguments)
    {
        JsNumber codePoint;
        using var result = new ValueStringBuilder(stackalloc char[128]);
        foreach (var a in arguments)
        {
            int point;
            codePoint = TypeConverter.ToJsNumber(a);
            if (codePoint.IsInteger())
            {
                point = (int) codePoint._value;
                if (point is < 0 or > 0x10FFFF)
                {
                    goto rangeError;
                }
            }
            else
            {
                var pointTemp = codePoint._value;
                if (pointTemp < 0 || pointTemp > 0x10FFFF || double.IsInfinity(pointTemp) || double.IsNaN(pointTemp) || TypeConverter.ToInt32(pointTemp) != pointTemp)
                {
                    goto rangeError;
                }

                point = (int) pointTemp;
            }

            if (point <= 0xFFFF)
            {
                // BMP code point
                result.Append((char) point);
            }
            else
            {
                // Astral code point; split in surrogate halves
                // https://mathiasbynens.be/notes/javascript-encoding#surrogate-formulae
                point -= 0x10000;
                result.Append((char) ((point >> 10) + 0xD800)); // highSurrogate
                result.Append((char) (point % 0x400 + 0xDC00)); // lowSurrogate
            }
        }

        return JsString.Create(result.ToString());

        rangeError:
        _engine.SignalError(ExceptionHelper.CreateRangeError(_realm, "Invalid code point " + codePoint));
        return JsEmpty.Instance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.raw
    /// </summary>
    private JsValue Raw(JsValue thisObject, JsCallArguments arguments)
    {
        var cooked = TypeConverter.ToObject(_realm, arguments.At(0));
        var raw = cooked.Get(JintTaggedTemplateExpression.PropertyRaw);
        var operations = ArrayOperations.For(_realm, raw, forWrite: false);
        var length = operations.GetLength();

        if (length <= 0)
        {
            return JsString.Empty;
        }

        using var result = new ValueStringBuilder();
        for (var i = 0; i < length; i++)
        {
            if (i > 0)
            {
                if (i < arguments.Length && !arguments[i].IsUndefined())
                {
                    result.Append(TypeConverter.ToString(arguments[i]));
                }
            }

            result.Append(TypeConverter.ToString(operations.Get((ulong) i)));
        }

        return result.ToString();
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
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
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
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
