#nullable enable

// Deliberately no `using Jint;`, and deliberately a namespace outside the Jint tree.
//
// A file whose own namespace is nested under Jint - which every other file in this suite is, being
// Jint.Tests.PublicInterface - sees the Jint namespace implicitly, so it could never show what a host that
// imported only Jint.Native can reach. Through 4.16.x none of the calls below compiled from here: JsValue is
// in Jint.Native, and IsString/AsString/IsCallable and the rest were extension methods in Jint.
using System.Globalization;
using Jint.Native;

namespace HostValueVocabularyReach;

/// <summary>
/// A mapping helper of the shape a host writes when it maps values the engine handed it and never constructs
/// an engine itself, so <c>using Jint;</c> has no reason to be in the file.
/// </summary>
public static class ValueDescriber
{
    /// <summary>
    /// Describes a value through the <c>TryGet</c> half of the vocabulary: one type test per branch, and the
    /// value falls out of the one that matched.
    /// </summary>
    public static string Describe(JsValue value)
    {
        if (value.IsUndefined())
        {
            return "undefined";
        }

        if (value.IsNull())
        {
            return "null";
        }

        if (value.TryGetString(out var text))
        {
            return "string:" + text;
        }

        if (value.TryGetNumber(out var number))
        {
            return "number:" + number.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetBoolean(out var flag))
        {
            return "boolean:" + (flag ? "true" : "false");
        }

        if (value.TryGetArray(out var array))
        {
            return "array:" + array.ToArray().Length.ToString(CultureInfo.InvariantCulture);
        }

        if (value.IsDate())
        {
            return "date";
        }

        if (value.IsRegExp())
        {
            return "regexp";
        }

        if (value.IsPromise())
        {
            return "promise";
        }

        if (value.IsSymbol())
        {
            return "symbol";
        }

        if (value.IsBigInt())
        {
            return "bigint";
        }

        if (value.IsCallable())
        {
            return "callable";
        }

        if (value.TryGetObject(out _))
        {
            return "object";
        }

        return "?";
    }

    /// <summary>
    /// Describes a value through the <c>Is</c>/<c>As</c> half, which is what a host wrote before there was a
    /// <c>TryGet</c> one: two type tests per read, and an exception when the pair disagrees.
    /// </summary>
    public static string DescribeByAsserting(JsValue value)
    {
        if (value.IsString())
        {
            return "string:" + value.AsString();
        }

        if (value.IsNumber())
        {
            return "number:" + value.AsNumber().ToString(CultureInfo.InvariantCulture);
        }

        if (value.IsBoolean())
        {
            return "boolean:" + (value.AsBoolean() ? "true" : "false");
        }

        if (value.IsArray())
        {
            return "array:" + value.AsArray().ToArray().Length.ToString(CultureInfo.InvariantCulture);
        }

        if (value.IsObject())
        {
            return "object:" + value.AsObject().GetOwnPropertyKeys().Count.ToString(CultureInfo.InvariantCulture);
        }

        return "?";
    }

    /// <summary>
    /// Settles a promise without the file naming <c>Engine</c>, which is the whole point: the value knows
    /// which engine it belongs to.
    /// </summary>
    public static JsValue Settle(JsValue value) => value.UnwrapIfPromise();
}
