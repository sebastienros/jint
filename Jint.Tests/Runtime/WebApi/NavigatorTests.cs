#if NET8_0_OR_GREATER
#nullable enable

using System.Text.RegularExpressions;
using Jint.Runtime;

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

    [Fact]
    public void ExposesUserAgentAsAReadOnlyAccessor()
    {
        var engine = NavigatorEngine();

        // A WebIDL readonly attribute is an accessor with no setter, so a script cannot assign a different
        // user agent and fool the next reader.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(navigator, 'userAgent')").AsObject();
        descriptor.Get("get").IsCallable.Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();

        // Same observable answer a browser gives, where the attribute lives on Navigator.prototype.
        engine.Evaluate("JSON.stringify(Object.keys(navigator))").AsString().Should().Be("[]");
    }

    [Fact]
    public void BrandChecksItsReceiver()
    {
        var engine = NavigatorEngine();

        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("Object.getOwnPropertyDescriptor(navigator, 'userAgent').get.call({})"))
            .Message.Should().Contain("Navigator");

        engine.Evaluate("Object.getOwnPropertyDescriptor(navigator, 'userAgent').get.call(navigator)")
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

        // There is no Navigator interface object either, which is the same simplification console, crypto and
        // performance carry.
        engine.Evaluate("typeof Navigator").AsString().Should().Be("undefined");
    }

    [Fact]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = NavigatorEngine();

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("navigator");
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
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
