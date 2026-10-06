#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.IndexedDb;

/// <summary>IndexedDB errors and WebIDL conversions: https://w3c.github.io/IndexedDB/#exceptions.</summary>
internal static class IndexedDbErrors
{
    [DoesNotReturn]
    internal static T Throw<T>(Realm realm, string name, string message)
        => throw new JavaScriptException(realm.Intrinsics.DomException.CreateException(name, message));

    [DoesNotReturn]
    internal static void Throw(Realm realm, string name, string message) => Throw<JsValue>(realm, name, message);

    internal static T Brand<T>(Realm realm, JsValue value) where T : ObjectInstance
    {
        if (value is T typed) return typed;
        Runtime.Throw.TypeError(realm, "Illegal invocation");
        return null!;
    }

    internal static void Arity(Realm realm, JsCallArguments args, int count)
    {
        if (args.Length < count) Runtime.Throw.TypeError(realm, $"At least {count} argument(s) required.");
    }

    internal static ObjectInstance? Dictionary(Realm realm, JsValue value)
    {
        if (value.IsNullOrUndefined()) return null;
        if (value is ObjectInstance result) return result;
        Runtime.Throw.TypeError(realm, "The options must be a dictionary.");
        return null;
    }

    internal static double EnforceRange(Realm realm, JsValue value, double maximum)
    {
        var number = TypeConverter.ToNumber(value);
        var integer = System.Math.Truncate(number);
        if (!double.IsFinite(number) || integer < 0 || integer > maximum)
        {
            Runtime.Throw.TypeError(realm, "The argument is outside the permitted integer range.");
        }
        return integer;
    }

    internal static string Direction(Realm realm, JsValue value)
    {
        var direction = value.IsUndefined() ? "next" : TypeConverter.ToString(value);
        if (direction is not ("next" or "nextunique" or "prev" or "prevunique"))
        {
            Runtime.Throw.TypeError(realm, "Invalid cursor direction.");
        }
        return direction;
    }
}
#endif
