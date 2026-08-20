#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>crypto</c> object seen from outside the assembly: what a host has to write to get it, what it gets
/// when it writes nothing, and the promises it can build on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party and every
/// assertion goes through script or the public options surface, exactly as an embedder's would.
/// </remarks>
public class WebApiCryptoTests
{
    [Fact]
    public void ADefaultEngineHasNoCrypto()
    {
        var engine = new Engine();

        engine.Evaluate("typeof crypto").AsString().Should().Be("undefined");
        engine.Evaluate("'crypto' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void UseWebApisInstallsCrypto()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof crypto").AsString().Should().Be("object");
        engine.Evaluate("typeof crypto.getRandomValues").AsString().Should().Be("function");
        engine.Evaluate("typeof crypto.randomUUID").AsString().Should().Be("function");

        // The default set is what UseWebApis() means, and it now names crypto.
        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Crypto);
    }

    [Fact]
    public void AskingForConsoleAloneDoesNotBringCrypto()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof crypto").AsString().Should().Be("undefined");
    }

    [Fact]
    public void FillsAHostSuppliedTypedArrayWithRandomBytes()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // Sixteen bytes coming back all zero would be a 2^-128 event, so this is a fair test that the host's
        // array really was filled and that the same object came back.
        engine.Evaluate("""
            (() => {
                const array = new Uint8Array(16);
                const returned = crypto.getRandomValues(array);
                return returned === array && array.some(b => b !== 0);
            })()
            """).AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ProducesRandomUuidsAHostCanParse()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        var first = engine.Evaluate("crypto.randomUUID()").AsString();
        var second = engine.Evaluate("crypto.randomUUID()").AsString();

        first.Should().NotBe(second);
        Guid.TryParseExact(first, "D", out var parsed).Should().BeTrue();
        parsed.ToString("D").Should().Be(first, "the string is already the lowercase hyphenated form");

        // Version 4 and the RFC 4122 variant, which is what the algorithm's bit-setting steps produce.
        first[14].Should().Be('4');
        "89ab".Contains(first[19]).Should().BeTrue();
    }

    [Fact]
    public void ReportsItsFailuresAsTheStandardExceptions()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // A DOMException a script can catch and branch on, not a CLR exception erupting through the host.
        engine.Evaluate("""
            (() => {
                const seen = [];
                try { crypto.getRandomValues(new Float64Array(4)); } catch (e) { seen.push(e.name); }
                try { crypto.getRandomValues(new Uint8Array(65537)); } catch (e) { seen.push(e.name); }
                try { crypto.getRandomValues([1, 2, 3]); } catch (e) { seen.push(e.constructor.name); }
                return seen.join(',');
            })()
            """).AsString().Should().Be("TypeMismatchError,QuotaExceededError,TypeError");
    }

    [Fact]
    public void HasNoSubtleCryptoForFeatureDetectionToFind()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof crypto.subtle").AsString().Should().Be("undefined");
    }

    [Fact]
    public void AHostRegisteredCryptoGlobalWins()
    {
        var marker = new JsString("the host's own crypto");

        var engine = new Engine(options => options
            .AddLazyGlobal("crypto", _ => marker)
            .UseWebApis());

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("crypto").Should().BeSameAs(marker);
    }

    [Fact]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));

        // A documented simplification of the WebIDL [Replaceable] accessor pair, and the same shape console
        // is installed with.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'crypto')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof crypto')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof crypto").AsString().Should().Be("object");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEngines()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Crypto);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Evaluate("crypto === crypto").AsBoolean().Should().BeTrue();
        first.Evaluate("crypto.randomUUID()").AsString().Should().NotBe(second.Evaluate("crypto.randomUUID()").AsString());
    }
}
#endif
