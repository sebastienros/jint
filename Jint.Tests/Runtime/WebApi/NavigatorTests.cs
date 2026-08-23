#if NET8_0_OR_GREATER
#nullable enable

using System.Text.RegularExpressions;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>navigator</c> object and its one member, which WinterTC's Minimum Common API
/// (https://min-common-api.proposal.wintertc.org/) requires of a conforming runtime as
/// <c>globalThis.navigator.userAgent</c>.
/// </summary>
public class NavigatorTests
{
    private static Engine NavigatorEngine(WebApiFeatures features = WebApiFeatures.Navigator)
        => new(options => options.UseWebApis(features));

    [Fact]
    public void ReportsAJintProductToken()
    {
        var userAgent = NavigatorEngine().Evaluate("navigator.userAgent").AsString();

        // RFC 7231's product = token ["/" product-version]. No comment component, so nothing about the host
        // operating system or the embedding application leaks into it.
        Regex.IsMatch(userAgent, @"^Jint/\d+\.\d+\.\d+$").Should().BeTrue(userAgent);

        // The value the assembly actually carries, not a hard-coded one.
        var version = typeof(Engine).Assembly.GetName().Version!;
        userAgent.Should().Be($"Jint/{version.Major}.{version.Minor}.{version.Build}");
    }

    [Fact]
    public void IsOneStableObjectWithTheInterfacesToStringTag()
    {
        var engine = NavigatorEngine();

        engine.Evaluate("navigator === navigator").AsBoolean().Should().BeTrue();
        engine.Evaluate("navigator === globalThis.navigator").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(navigator)").AsString().Should().Be("[object Navigator]");
        engine.Evaluate("navigator[Symbol.toStringTag]").AsString().Should().Be("Navigator");
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-attributes — a <c>readonly attribute</c> is an accessor with no
    /// setter, on the interface prototype object, carrying
    /// <c>{ [[Enumerable]]: true, [[Configurable]]: true }</c>. Node 24 answers exactly that for
    /// <c>Object.getOwnPropertyDescriptor(Navigator.prototype, 'userAgent')</c>.
    /// </summary>
    [Fact]
    public void ExposesUserAgentAsAReadOnlyAccessorOnTheInterfacePrototypeObject()
    {
        var engine = NavigatorEngine();

        // A WebIDL readonly attribute is an accessor with no setter, so a script cannot assign a different
        // user agent and fool the next reader.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(Navigator.prototype, 'userAgent')").AsObject();
        descriptor.Get("get").IsCallable.Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();

        // The instance carries nothing of its own, which is why the enumerability above is invisible from it
        // — the answer Node 24 gives to both questions.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(navigator, 'userAgent')").AsBoolean().Should().BeFalse();
        engine.Evaluate("JSON.stringify(Object.keys(navigator))").AsString().Should().Be("[]");
        engine.Evaluate("Reflect.ownKeys(navigator).length").AsNumber().Should().Be(0);

        // And it is enumerable where a browser has it.
        engine.Evaluate("Object.keys(Navigator.prototype).indexOf('userAgent') >= 0").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void BrandChecksItsReceiver()
    {
        var engine = NavigatorEngine();

        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("Object.getOwnPropertyDescriptor(Navigator.prototype, 'userAgent').get.call({})"))
            .Message.Should().Contain("Navigator");

        engine.Evaluate("Object.getOwnPropertyDescriptor(Navigator.prototype, 'userAgent').get.call(navigator)")
            .AsString().Should().StartWith("Jint/");
    }

    [Fact]
    public void CarriesNothingABrowsersNavigatorWouldLieAbout()
    {
        var engine = NavigatorEngine();

        // Absent rather than faked: an embedded interpreter has no user, no document and no network stack, so
        // feature detection should see that rather than a plausible-looking constant.
        foreach (var member in new[] { "language", "languages", "onLine", "platform", "hardwareConcurrency", "clipboard", "geolocation", "storage", "locks" })
        {
            engine.Evaluate($"typeof navigator.{member}").AsString().Should().Be("undefined");
        }

        // The interface prototype object carries nothing of theirs either — the absence is the interface's,
        // not merely the instance's.
        foreach (var member in new[] { "language", "languages", "onLine", "platform", "hardwareConcurrency" })
        {
            engine.Evaluate($"Object.prototype.hasOwnProperty.call(Navigator.prototype, '{member}')")
                .AsBoolean().Should().BeFalse(member);
        }
    }

    /// <summary>
    /// The interface object and the interface prototype object, which <c>navigator</c> had neither of. Node 24
    /// is the oracle for every line: <c>typeof Navigator</c> is <c>"function"</c>, the instance's
    /// <c>[[Prototype]]</c> is <c>Navigator.prototype</c>, that object's is <c>%Object.prototype%</c>, and
    /// <c>Reflect.ownKeys(navigator)</c> is the empty array.
    /// </summary>
    [Fact]
    public void HasARealInterfaceObjectAndInterfacePrototypeObject()
    {
        var engine = NavigatorEngine();

        engine.Evaluate("typeof Navigator").AsString().Should().Be("function");
        engine.Evaluate("Navigator.name").AsString().Should().Be("Navigator");
        engine.Evaluate("Navigator.length").AsNumber().Should().Be(0);

        engine.Evaluate("navigator instanceof Navigator").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(navigator) === Navigator.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Navigator.prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Navigator.prototype.constructor === Navigator").AsBoolean().Should().BeTrue();

        // instanceof is answered by the chain, never by a shim.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(Navigator, Symbol.hasInstance)").AsBoolean().Should().BeFalse();

        // https://html.spec.whatwg.org/multipage/system-state.html#the-navigator-object declares no
        // constructor operation, so the interface object is a function that refuses to construct —
        // https://webidl.spec.whatwg.org/#es-interface-call. Node 24 answers 'TypeError: Illegal constructor'.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new Navigator()"))
            .Message.Should().Be("Illegal constructor");
    }

    /// <summary>
    /// The containment half of https://github.com/sebastienros/jint/issues/3257, which #3266 closed for
    /// <c>console</c> and left open here: a singleton sitting directly on <c>%Object.prototype%</c> makes
    /// <c>navigator.__proto__.foo = …</c> a realm-wide poisoning. An interface prototype object is the
    /// landing pad that contains it.
    /// </summary>
    [Fact]
    public void PatchingNavigatorsPrototypeDoesNotReachObjectPrototype()
    {
        var engine = NavigatorEngine();

        engine.Execute("navigator.__proto__.decorated = 42;");

        // It still works on navigator, which is what a patching library is after.
        engine.Evaluate("navigator.decorated").AsNumber().Should().Be(42);

        // And it reaches nothing else.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(Object.prototype, 'decorated')").AsBoolean().Should().BeFalse();
        engine.Evaluate("'decorated' in {}").AsBoolean().Should().BeFalse();
        engine.Evaluate("({}).decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("[].decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("(function () {}).decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("'s'.decorated").IsUndefined().Should().BeTrue();
    }

    /// <summary>
    /// The interface prototype object is per realm, so one engine's patch is not another's.
    /// </summary>
    [Fact]
    public void GivesEachEngineItsOwnNavigatorPrototype()
    {
        var options = new Options().UseWebApis();

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("navigator.__proto__.decorated = 42;");

        first.Evaluate("navigator.decorated").AsNumber().Should().Be(42);
        second.Evaluate("navigator.decorated").IsUndefined().Should().BeTrue();
        second.Evaluate("Object.prototype.hasOwnProperty.call(Object.prototype, 'decorated')").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = NavigatorEngine();

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("navigator");
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();

        // And still unmaterialized: an engine that never mentions `navigator` has built neither the object
        // nor the interface behind it. The interface object's own half is pinned by
        // InterfaceObjectExposureTests.CostsNothingUntilItIsRead.
        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull();
    }

    [Fact]
    public void IsPartOfTheDefaultSetButNotOfAnyOtherFeature()
    {
        new Engine(options => options.UseWebApis()).Evaluate("typeof navigator.userAgent").AsString().Should().Be("string");
        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Navigator);

        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof navigator").AsString().Should().Be("undefined");
    }

    [Fact]
    public void KeepsTheBitReservedForFetch()
    {
        // The layout is a promise to hosts that persist a feature set: 1 << 10 stays free for fetch, so
        // Navigator is 1 << 11 rather than the next unused bit.
        ((int) WebApiFeatures.Navigator).Should().Be(1 << 11);
        ((int) WebApiFeatures.Default & (1 << 10)).Should().Be(0);
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = NavigatorEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof navigator')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof navigator").AsString().Should().Be("object");
    }
}
#endif
