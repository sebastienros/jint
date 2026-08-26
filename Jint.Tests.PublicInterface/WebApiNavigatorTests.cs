#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>navigator</c> object seen from outside the assembly: what a host has to write to get it, and what
/// it gets when it writes nothing.
/// </summary>
/// <remarks>
/// The member exists because WinterTC's Minimum Common API requires
/// <c>globalThis.navigator.userAgent</c> of a conforming runtime, so the pin that matters is that a script
/// written against that requirement finds it — and that an engine which did not ask for it does not.
/// </remarks>
public class WebApiNavigatorTests
{
    [Test]
    public void ADefaultEngineHasNoNavigator()
    {
        var engine = new Engine();

        engine.Evaluate("typeof navigator").AsString().Should().Be("undefined");
        engine.Evaluate("'navigator' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void UseWebApisInstallsNavigator()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof navigator").AsString().Should().Be("object");
        engine.Evaluate("typeof navigator.userAgent").AsString().Should().Be("string");

        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Navigator);
    }

    [Test]
    public void AskingForConsoleAloneDoesNotBringNavigator()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof navigator").AsString().Should().Be("undefined");
    }

    [Test]
    public void ReportsJintAndItsVersion()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Navigator));

        var version = typeof(Engine).Assembly.GetName().Version!;
        engine.Evaluate("navigator.userAgent").AsString()
            .Should().Be($"Jint/{version.Major}.{version.Minor}.{version.Build}");
    }

    [Test]
    public void AHostRegisteredNavigatorGlobalWins()
    {
        var marker = new JsString("the host's own navigator");

        var engine = new Engine(options => options
            .AddLazyGlobal("navigator", _ => marker)
            .UseWebApis());

        engine.Evaluate("navigator").Should().BeSameAs(marker);
    }

    [Test]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Navigator));

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'navigator')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof navigator')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof navigator").AsString().Should().Be("object");
    }
}
#endif
