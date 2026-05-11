#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-function-prototype-object
/// </summary>
[JsObject]
[JsThrowerAccessor("arguments")]
[JsThrowerAccessor("caller")]
internal sealed partial class FunctionPrototype : Function
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

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private FunctionConstructor Constructor => _realm.Intrinsics.Function;

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype-@@hasinstance
    /// </summary>
    [JsSymbolFunction("HasInstance", Length = 1, Flags = PropertyFlag.AllForbidden)]
    private static JsValue HasInstance(JsValue thisObject, JsValue v)
    {
        return thisObject.OrdinaryHasInstance(v);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype.bind
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Bind(ICallable thisObject, JsCallArguments arguments)
    {
        // BoundFunctionCreate needs ObjectInstance for the prototype walk — separate from the
        // generator-emitted IsCallable check that already ran on entry.
        if (thisObject is not ObjectInstance oi)
        {
            Throw.TypeError(_realm, "Bind must be called on a function");
            return default;
        }

        var thisArg = arguments.At(0);
        var f = BoundFunctionCreate(oi, thisArg, arguments.Skip(1));

        JsNumber l;
        var targetHasLength = oi.HasOwnProperty(CommonProperties.Length);
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
    private BindFunction BoundFunctionCreate(ObjectInstance targetFunction, JsValue boundThis, JsValue[] boundArgs)
    {
        var proto = targetFunction.GetPrototypeOf();
        var obj = new BindFunction(_engine, _realm, proto, targetFunction, boundThis, boundArgs);
        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype.tostring
    /// </summary>
    [JsFunction]
    private JsValue ToString(JsValue thisObject)
    {
        if (thisObject.IsObject() && thisObject.IsCallable)
        {
            return thisObject.ToString();
        }

        Throw.TypeError(_realm, "Function.prototype.toString requires that 'this' be a Function");
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype.apply
    /// </summary>
    [JsFunction]
    private JsValue Apply(ICallable thisObject, JsValue thisArg, JsValue argArray)
    {
        if (argArray.IsNullOrUndefined())
        {
            return thisObject.Call(thisArg, Arguments.Empty);
        }

        var argList = CreateListFromArrayLike(_realm, argArray);

        var result = thisObject.Call(thisArg, argList);

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
            Throw.TypeError(realm, "CreateListFromArrayLike called on non-object");
        }
        var operations = ArrayOperations.For(argArrayObj, forWrite: false);
        var argList = elementTypes is null
            ? operations.GetAll(errorRealm: realm)
            : operations.GetAll(elementTypes.Value, errorRealm: realm);
        return argList;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype.call
    /// </summary>
    [JsFunction(Length = 1, Name = "call")]
    private static JsValue CallImpl(ICallable thisObject, JsCallArguments arguments)
    {
        JsValue[] values = [];
        if (arguments.Length > 1)
        {
            values = new JsValue[arguments.Length - 1];
            System.Array.Copy(arguments, 1, values, 0, arguments.Length - 1);
        }

        var result = thisObject.Call(arguments.At(0), values);

        return result;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Undefined;
    }
}
