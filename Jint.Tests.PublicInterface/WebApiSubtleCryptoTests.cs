#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>crypto.subtle.digest</c> seen from outside the assembly: what a host has to write to get it, what it
/// gets when it writes nothing, and what a script can build on top of it.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party and every
/// assertion goes through script or the public options surface, exactly as an embedder's would.
/// </remarks>
public class WebApiSubtleCryptoTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Crypto));

    private static string DigestHex(Engine engine, string algorithm, string ascii)
    {
        return engine.Evaluate($$"""
            (async () => {
                const data = Uint8Array.from('{{ascii}}', c => c.charCodeAt(0));
                const digest = await crypto.subtle.digest('{{algorithm}}', data);
                return Array.from(new Uint8Array(digest)).map(b => b.toString(16).padStart(2, '0')).join('');
            })()
            """).UnwrapIfPromise().AsString();
    }

    [Fact]
    public void ADefaultEngineHasNoSubtleCrypto()
    {
        var engine = new Engine();

        engine.Evaluate("typeof crypto").AsString().Should().Be("undefined");
        engine.Evaluate("'crypto' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheCryptoFlagIsWhatInstallsIt()
    {
        // There is no flag of its own: `subtle` is a readonly attribute of the Crypto interface, so asking
        // for crypto is asking for it, and a host that did not ask for crypto has neither.
        WebEngine().Evaluate("typeof crypto.subtle").AsString().Should().Be("object");

        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof crypto").AsString().Should().Be("undefined");

        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Crypto);
    }

    [Fact]
    public void HashesTheWayAHostCanCheckAgainstItsOwnCode()
    {
        var engine = WebEngine();

        // The published FIPS-180-4 / RFC 3174 example vectors, so a host comparing a script's digest with
        // one computed CLR-side gets the same bytes.
        DigestHex(engine, "SHA-1", "abc").Should().Be("a9993e364706816aba3e25717850c26c9cd0d89d");
        DigestHex(engine, "SHA-256", "abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        DigestHex(engine, "SHA-384", "abc").Should().Be("cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7");
        DigestHex(engine, "SHA-512", "abc").Should().Be("ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f");
    }

    [Fact]
    public void AnsweredPromiseUnwrapsForAHostThatBlocks()
    {
        var engine = WebEngine();

        // The host-side spelling: Evaluate then UnwrapIfPromise, with no event loop for the host to pump
        // itself — the work is synchronous, so the promise is already settled when the drain reaches it.
        var value = engine.Evaluate("crypto.subtle.digest('SHA-256', new Uint8Array(0))").UnwrapIfPromise();

        value.IsObject().Should().BeTrue();
        engine.SetValue("hostHeldDigest", value);
        engine.Evaluate("hostHeldDigest instanceof ArrayBuffer && hostHeldDigest.byteLength === 32")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReportsItsFailuresAsRejectionsAScriptCanCatch()
    {
        var engine = WebEngine();

        // Nothing erupts into the host: an unregistered algorithm is a DOMException rejection and a bad
        // buffer is a TypeError rejection, both catchable in script.
        engine.Evaluate("""
            (async () => {
                const seen = [];
                try { await crypto.subtle.digest('MD5', new Uint8Array(0)); } catch (e) { seen.push(e.name); }
                try { await crypto.subtle.digest('SHA-256', 'not a buffer'); } catch (e) { seen.push(e.constructor.name); }
                return seen.join(',');
            })()
            """).UnwrapIfPromise().AsString().Should().Be("NotSupportedError,TypeError");
    }

    [Fact]
    public void NeverThrowsIntoTheHost()
    {
        var engine = WebEngine();

        // A promise-returning WebIDL operation converts every exception into a rejection, so a host calling
        // Evaluate on a line of nonsense gets a promise back rather than a JavaScriptException.
        var value = engine.Evaluate("crypto.subtle.digest('nonsense', 42)");

        value.IsObject().Should().BeTrue();
        engine.SetValue("hostHeldPromise", value);
        engine.Evaluate("hostHeldPromise.then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");
    }

    [Fact]
    public void IsOneObjectPerEngineAndNeverShared()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Crypto);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Evaluate("crypto.subtle === crypto.subtle").AsBoolean().Should().BeTrue();
        second.Evaluate("crypto.subtle === crypto.subtle").AsBoolean().Should().BeTrue();

        // Two engines built from one shared Options instance each get their own, which is the same promise
        // every other web-API object makes.
        DigestHex(first, "SHA-256", "abc").Should().Be(DigestHex(second, "SHA-256", "abc"));
    }

    [Fact]
    public void AHostRegisteredCryptoGlobalWinsAndBringsNoSubtle()
    {
        var marker = new JsString("the host's own crypto");

        var engine = new Engine(options => options
            .AddLazyGlobal("crypto", _ => marker)
            .UseWebApis());

        // The host's configuration runs first and the install is non-clobbering, so nothing of ours reaches
        // the global — including the subtle object, which has no other door.
        engine.Evaluate("crypto").Should().BeSameAs(marker);
        engine.Evaluate("typeof crypto.subtle").AsString().Should().Be("undefined");
    }

    [Fact]
    public void IsAReadOnlyEnumerableAttribute()
    {
        var engine = WebEngine();

        // On Crypto.prototype, where a browser's is: `crypto` itself carries no own property at all.
        engine.Evaluate("Object.getOwnPropertyNames(crypto).length").AsNumber().Should().Be(0);
        engine.Evaluate("crypto instanceof Crypto").AsBoolean().Should().BeTrue();
        engine.Evaluate("crypto.subtle instanceof SubtleCrypto").AsBoolean().Should().BeTrue();

        // A WebIDL attribute: an enumerable, configurable accessor with no setter,
        // https://webidl.spec.whatwg.org/#es-attributes. Node 24 reports the same triple.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(Crypto.prototype, 'subtle')").AsObject();
        descriptor.Get("get").IsCallable().Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        // The operation carries a WebIDL regular operation's attributes — enumerable included,
        // https://webidl.spec.whatwg.org/#es-operations — and WebIDL's length counts the required arguments
        // only.
        engine.Evaluate("Object.getOwnPropertyDescriptor(SubtleCrypto.prototype, 'digest').enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("crypto.subtle.digest.length").AsNumber().Should().Be(2);
        engine.Evaluate("crypto.subtle.digest.name").AsString().Should().Be("digest");
        engine.Evaluate("Object.prototype.toString.call(crypto.subtle)").AsString().Should().Be("[object SubtleCrypto]");
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof crypto')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof crypto.subtle").AsString().Should().Be("object");
    }
}
#endif
