using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Streams;

namespace Jint.Browser.SystemState;

/// <summary>
/// <c>navigator.userAgentData</c>'s members, read from the <see cref="UserAgentClientHints"/> the page's
/// emulation state describes on every call, so an override reaches the document that is already loaded.
/// </summary>
internal static class NavigatorUserAgentData
{
    /// <summary>
    /// https://wicg.github.io/ua-client-hints/#dom-navigatoruadata-brands — a <c>FrozenArray</c>, so the same
    /// array on every read until what it was built from changes.
    /// </summary>
    internal static JsValue Brands(JsSystemObject data)
    {
        var hints = UserAgentClientHints.For(data.Runtime.Emulation);
        if (!ReferenceEquals(data.CacheKey, hints) || data.CacheValue is null)
        {
            var array = BrandList(data.Runtime, hints.Brands);
            array.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);
            data.CacheKey = hints;
            data.CacheValue = array;
        }

        return data.CacheValue;
    }

    internal static JsValue Mobile(JsSystemObject data)
        => UserAgentClientHints.For(data.Runtime.Emulation).Mobile ? JsBoolean.True : JsBoolean.False;

    internal static JsValue Platform(JsSystemObject data)
        => JsString.Create(UserAgentClientHints.For(data.Runtime.Emulation).Platform);

    /// <summary>https://wicg.github.io/ua-client-hints/#dom-navigatoruadata-tojson</summary>
    internal static JsValue ToJson(JsSystemObject data)
    {
        var runtime = data.Runtime;
        var hints = UserAgentClientHints.For(runtime.Emulation);
        var result = new JsObject(runtime.Engine);
        result.CreateDataPropertyOrThrow("brands", BrandList(runtime, hints.Brands));
        result.CreateDataPropertyOrThrow("mobile", hints.Mobile);
        result.CreateDataPropertyOrThrow("platform", hints.Platform);
        return result;
    }

    /// <summary>
    /// https://wicg.github.io/ua-client-hints/#dom-navigatoruadata-gethighentropyvalues — the low-entropy
    /// three and each hint asked for, as a <c>UADataValues</c> dictionary, whose members a script sees in
    /// lexicographic order.
    /// </summary>
    internal static JsValue GetHighEntropyValues(JsSystemObject data, JsValue argument)
    {
        var runtime = data.Runtime;
        var engine = runtime.Engine;
        var realm = engine._mainRealm;

        HashSet<string> requested;
        try
        {
            requested = Hints(realm, argument);
        }
        catch (JavaScriptException exception)
        {
            return StreamPromises.RejectedWith(engine, realm, exception.Error);
        }

        var hints = UserAgentClientHints.For(runtime.Emulation);
        var result = new JsObject(engine);

        if (requested.Contains("architecture"))
        {
            result.CreateDataPropertyOrThrow("architecture", hints.Architecture);
        }

        if (requested.Contains("bitness"))
        {
            result.CreateDataPropertyOrThrow("bitness", hints.Bitness);
        }

        result.CreateDataPropertyOrThrow("brands", BrandList(runtime, hints.Brands));

        if (requested.Contains("formFactors"))
        {
            result.CreateDataPropertyOrThrow(
                "formFactors",
                realm.Intrinsics.Array.ConstructFast(hints.FormFactors.Select(static f => (JsValue) JsString.Create(f)).ToArray()));
        }

        if (requested.Contains("fullVersionList"))
        {
            result.CreateDataPropertyOrThrow("fullVersionList", BrandList(runtime, hints.FullVersionList));
        }

        result.CreateDataPropertyOrThrow("mobile", hints.Mobile);

        if (requested.Contains("model"))
        {
            result.CreateDataPropertyOrThrow("model", hints.Model);
        }

        result.CreateDataPropertyOrThrow("platform", hints.Platform);

        if (requested.Contains("platformVersion"))
        {
            result.CreateDataPropertyOrThrow("platformVersion", hints.PlatformVersion);
        }

        if (requested.Contains("uaFullVersion"))
        {
            result.CreateDataPropertyOrThrow("uaFullVersion", hints.FullVersion);
        }

        if (requested.Contains("wow64"))
        {
            result.CreateDataPropertyOrThrow("wow64", hints.Wow64);
        }

        return StreamPromises.ResolvedWith(engine, realm, result);
    }

    /// <summary>The <c>sequence&lt;DOMString&gt;</c> of hints, converted as WebIDL converts one.</summary>
    private static HashSet<string> Hints(Realm realm, JsValue value)
    {
        if (value is not ObjectInstance instance || instance.GetMethod(Native.Symbol.GlobalSymbolRegistry.Iterator) is null)
        {
            Throw.TypeError(
                realm,
                "Failed to execute 'getHighEntropyValues' on 'NavigatorUAData': The provided value cannot be converted to a sequence.");
            return [];
        }

        var hints = new HashSet<string>(StringComparer.Ordinal);
        var iterator = value.GetIterator(realm);
        while (iterator.TryIteratorStepValue(out var item))
        {
            hints.Add(TypeConverter.ToString(item));
        }

        return hints;
    }

    /// <summary>A fresh array of <c>NavigatorUABrandVersion</c> dictionaries.</summary>
    private static JsArray BrandList(PageRuntime runtime, IReadOnlyList<(string Brand, string Version)> brands)
    {
        var engine = runtime.Engine;
        var values = new JsValue[brands.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var entry = new JsObject(engine);
            entry.CreateDataPropertyOrThrow("brand", brands[i].Brand);
            entry.CreateDataPropertyOrThrow("version", brands[i].Version);
            values[i] = entry;
        }

        return engine._mainRealm.Intrinsics.Array.ConstructFast(values);
    }
}
