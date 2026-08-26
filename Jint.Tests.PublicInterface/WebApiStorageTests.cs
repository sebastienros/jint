#if NET8_0_OR_GREATER
#nullable enable

using Jint;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>localStorage</c> and <c>sessionStorage</c> seen from outside the assembly: what a host has to write to
/// get them, what it has to write to decide where the data lives, and what it gets when it writes nothing.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party and every
/// assertion goes through script or the public options surface, exactly as an embedder's would.
/// </remarks>
public class WebApiStorageTests
{
    [Test]
    public void ADefaultEngineHasNoStorage()
    {
        var engine = new Engine();

        engine.Evaluate("typeof localStorage").AsString().Should().Be("undefined");
        engine.Evaluate("typeof sessionStorage").AsString().Should().Be("undefined");
        engine.Evaluate("typeof Storage").AsString().Should().Be("undefined");
        engine.Evaluate("'localStorage' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void UseWebApisDoesNotBringStorage()
    {
        // Storage is the one non-network feature that is not in the default set: a host asking for "the web
        // APIs" is asking for globals, not for somewhere a script can leave data behind.
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof localStorage").AsString().Should().Be("undefined");

        new Options().UseWebApis().WebApi.Features.Should().NotHaveFlag(WebApiFeatures.Storage);
    }

    [Test]
    public void UseStorageInstallsBothGlobalsAndTheInterfaceObject()
    {
        var engine = new Engine(options => options.UseStorage());

        engine.Evaluate("typeof localStorage").AsString().Should().Be("object");
        engine.Evaluate("typeof sessionStorage").AsString().Should().Be("object");
        engine.Evaluate("typeof Storage").AsString().Should().Be("function");
        engine.Evaluate("localStorage instanceof Storage").AsBoolean().Should().BeTrue();

        new Options().UseStorage().WebApi.Features.Should().HaveFlag(WebApiFeatures.Storage);
    }

    [Test]
    public void NamingTheFlagIsEnough()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Storage));

        engine.Evaluate("localStorage.setItem('a', '1'); localStorage.a").AsString().Should().Be("1");

        // ... and it brings nothing else with it.
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
    }

    [Test]
    public void TheDefaultStoreIsPerEngineAndDiesWithIt()
    {
        var options = new Options().UseStorage();

        var first = new Engine(options);
        first.Execute("localStorage.setItem('who', 'first')");

        // Nothing is shared and nothing persists: a second engine built from the very same options starts
        // empty, which is what makes enabling this safe by default.
        var second = new Engine(options);
        second.Evaluate("localStorage.length").AsNumber().Should().Be(0);
        second.Evaluate("String(localStorage.getItem('who'))").AsString().Should().Be("null");
    }

    [Test]
    public void AHostProviderIsWhatMakesStoragePersist()
    {
        // The provider outlives the engine, so this is how a host gives a pooled or per-request engine a
        // store that was there before it and is there after it.
        var provider = new InMemoryStorageProvider();

        var first = new Engine(options => options.UseStorage(provider));
        first.Execute("localStorage.setItem('visits', '1')");

        var second = new Engine(options => options.UseStorage(provider));
        second.Execute("localStorage.setItem('visits', String(Number(localStorage.getItem('visits')) + 1))");

        second.Evaluate("localStorage.getItem('visits')").AsString().Should().Be("2");
        provider.GetItem("visits").Should().Be("2");
        provider.Count.Should().Be(1);
        provider.Keys.Should().ContainSingle().Which.Should().Be("visits");
    }

    [Test]
    public void AHostCanImplementTheProviderItself()
    {
        var provider = new DictionaryStorageProvider();
        provider.Set("seeded", "by the host");

        var engine = new Engine(options => options.UseStorage(provider));

        engine.Evaluate("""
            (() => {
                const seeded = localStorage.seeded;
                localStorage.fromScript = 'hello';
                localStorage.setItem('another', 'value');
                localStorage.removeItem('another');
                return [seeded, localStorage.length, Object.keys(localStorage).join(',')].join('|');
            })()
            """).AsString().Should().Be("by the host|2|seeded,fromScript");

        provider.Read("fromScript").Should().Be("hello");
    }

    [Test]
    public void AProviderThatRefusesAWriteRaisesACatchableQuotaExceededError()
    {
        var engine = new Engine(options => options.UseStorage(new RefusingStorageProvider()));

        // The host's message reaches the script, which sees a QuotaExceededError it can branch on rather than
        // a CLR exception erupting through the embedder. A provider that named no figures leaves both members
        // null, which is what https://webidl.spec.whatwg.org/#quotaexceedederror says an unstated quota is.
        engine.Evaluate("""
            (() => {
                try { localStorage.setItem('a', '1'); }
                catch (e) {
                    return [
                        e.name,
                        e.message,
                        e instanceof QuotaExceededError,
                        e instanceof DOMException,
                        e.code,
                        String(e.quota),
                        String(e.requested)
                    ].join('|');
                }
                return 'no throw';
            })()
            """).AsString().Should().Be("QuotaExceededError|this store is full|true|true|22|null|null");
    }

    [Test]
    public void AProviderCanReportHowMuchRoomThereWasAndHowMuchWasWanted()
    {
        var engine = new Engine(options => options.UseStorage(new BudgetedStorageProvider()));

        // The constructor overload that takes the two numbers is the host's channel into
        // https://webidl.spec.whatwg.org/#quotaexceedederror's `quota` and `requested`.
        engine.Evaluate("""
            (() => {
                try { localStorage.setItem('a', '1'); }
                catch (e) { return [e.name, e.quota, e.requested].join('|'); }
                return 'no throw';
            })()
            """).AsString().Should().Be("QuotaExceededError|100|140");
    }

    [TestCase(0L)]
    [TestCase(-1L)]
    public void ABudgetOfZeroOrLessReportsAQuotaOfZeroRatherThanANegativeOne(long maxTotalBytes)
    {
        var engine = new Engine(options =>
        {
            options.UseStorage();
            options.WebApi.Storage.MaxTotalBytes = maxTotalBytes;
        });

        // https://webidl.spec.whatwg.org/#quotaexceedederror refuses a negative `quota` outright, so a budget
        // of −1 is reported as the nothing it actually admits. `requested` is the size the store would have
        // reached — 2 * (1 + 1) for this pair — not the increment.
        engine.Evaluate("""
            (() => {
                try { localStorage.setItem('a', '1'); }
                catch (e) { return [e.name, e.quota, e.requested].join('|'); }
                return 'no throw';
            })()
            """).AsString().Should().Be("QuotaExceededError|0|4");
    }

    // The three things the interface forbids, refused where the host makes the mistake rather than reaching
    // the script as a pair of numbers that cannot both be true.
    [TestCase(-1d, 5d)]
    [TestCase(5d, -1d)]
    [TestCase(9d, 8d)]
    [TestCase(double.NaN, 1d)]
    [TestCase(1d, double.PositiveInfinity)]
    public void AnIllFormedQuotaPairIsRefusedAtTheExceptionsOwnConstructor(double quota, double requested)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StorageQuotaExceededException("x", quota, requested));
    }

    [Test]
    public void AWellFormedQuotaPairIsKeptOnTheException()
    {
        var exception = new StorageQuotaExceededException("x", quota: 8, requested: 8);

        exception.Quota.Should().Be(8);
        exception.Requested.Should().Be(8);

        // The constructors that do not take them leave both unset, which is what null means here.
        new StorageQuotaExceededException("x").Quota.Should().BeNull();
        new StorageQuotaExceededException().Requested.Should().BeNull();
    }

    [Test]
    public void TheInBoxQuotaIsConfigurable()
    {
        var engine = new Engine(options =>
        {
            options.UseStorage();
            options.WebApi.Storage.MaxTotalBytes = 64;
        });

        // Counted as 2 bytes per UTF-16 code unit of key and value together, so 24 characters is 48 bytes.
        engine.Evaluate("""
            (() => {
                localStorage.setItem('fits', 'x'.repeat(20));
                try { localStorage.setItem('over', 'y'.repeat(20)); }
                catch (e) { return e.name + '|' + localStorage.length; }
                return 'no throw';
            })()
            """).AsString().Should().Be("QuotaExceededError|1");
    }

    [Test]
    public void TheHostDecidesWhetherTheTwoGlobalsAreOneStore()
    {
        var shared = new InMemoryStorageProvider();
        var engine = new Engine(options => options.UseStorage(shared, shared));

        engine.Execute("localStorage.setItem('a', '1')");

        // Two objects, one map — something a browser can never do, and occasionally what a host wants.
        engine.Evaluate("sessionStorage.getItem('a')").AsString().Should().Be("1");
        engine.Evaluate("localStorage === sessionStorage").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void SessionStorageCanBeGivenItsOwnProviderAlone()
    {
        var session = new InMemoryStorageProvider();
        var engine = new Engine(options =>
        {
            options.UseStorage();
            options.WebApi.Storage.SessionStorageProvider = session;
        });

        engine.Execute("sessionStorage.setItem('a', '1'); localStorage.setItem('b', '2')");

        session.GetItem("a").Should().Be("1");
        session.GetItem("b").Should().BeNull();
        engine.Evaluate("localStorage.getItem('b')").AsString().Should().Be("2");
    }

    [Test]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("the host's own storage");

        var engine = new Engine(options => options
            .AddLazyGlobal("localStorage", _ => marker)
            .UseStorage());

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("localStorage").Should().BeSameAs(marker);
        engine.Evaluate("typeof sessionStorage").AsString().Should().Be("object");
    }

    [Test]
    public void HasTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseStorage());

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'localStorage')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        // An interface object is writable and configurable but not enumerable.
        var interfaceObject = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'Storage')").AsObject();
        interfaceObject.Get("writable").AsBoolean().Should().BeTrue();
        interfaceObject.Get("enumerable").AsBoolean().Should().BeFalse();
        interfaceObject.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseStorage());

        engine.Evaluate("new ShadowRealm().evaluate('typeof localStorage')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof Storage')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof localStorage").AsString().Should().Be("object");
    }

    [Test]
    public void KeepsWhatTheProviderHoldsAcrossAGlobalSnapshotRestore()
    {
        var provider = new InMemoryStorageProvider();
        var engine = new Engine(options => options.UseStorage(provider));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("localStorage.setItem('a', '1'); var leaked = true;");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // A restore reverts the global binding table, not the world behind it: the script's own global is
        // gone and the host's store is not. A host pooling an engine per request and wanting the storage
        // gone too clears the provider itself — it is host state, like Engine.HostDefined.
        engine.Evaluate("typeof leaked").AsString().Should().Be("undefined");
        engine.Evaluate("localStorage.getItem('a')").AsString().Should().Be("1");

        provider.Clear();
        engine.Evaluate("localStorage.length").AsNumber().Should().Be(0);
    }

    [Test]
    public void TheInBoxProviderIsUsableOnItsOwn()
    {
        var provider = new InMemoryStorageProvider(maxTotalBytes: 32);

        provider.SetItem("a", "1");
        provider.SetItem("b", "2");
        provider.SetItem("a", "3");

        provider.Count.Should().Be(2);
        provider.Keys.Should().Equal("a", "b");
        provider.GetItem("a").Should().Be("3");
        provider.GetItem("missing").Should().BeNull();
        provider.UsedBytes.Should().Be(8);

        // The quota is enforced against the host too, and by the same exception it hands the engine.
        var quota = Assert.Throws<StorageQuotaExceededException>(() => provider.SetItem("c", new string('x', 32)))!;
        quota.Message.Should().Contain("quota");
        provider.Count.Should().Be(2);

        provider.RemoveItem("a");
        provider.Keys.Should().Equal("b");
        provider.UsedBytes.Should().Be(4);

        provider.Clear();
        provider.Count.Should().Be(0);
        provider.UsedBytes.Should().Be(0);
    }

    /// <summary>
    /// The shape a host writes: a store of its own, reachable from the host as well as from script.
    /// </summary>
    private sealed class DictionaryStorageProvider : StorageProvider
    {
        private readonly List<string> _order = [];
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        internal void Set(string key, string value) => SetItem(key, value);

        internal string? Read(string key) => GetItem(key);

        public override IReadOnlyList<string> Keys => _order;

        public override string? GetItem(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public override void SetItem(string key, string value)
        {
            if (!_values.ContainsKey(key))
            {
                _order.Add(key);
            }

            _values[key] = value;
        }

        public override void RemoveItem(string key)
        {
            if (_values.Remove(key))
            {
                _order.Remove(key);
            }
        }

        public override void Clear()
        {
            _values.Clear();
            _order.Clear();
        }
    }

    private sealed class RefusingStorageProvider : StorageProvider
    {
        public override IReadOnlyList<string> Keys => [];

        public override string? GetItem(string key) => null;

        public override void SetItem(string key, string value) => throw new StorageQuotaExceededException("this store is full");

        public override void RemoveItem(string key)
        {
        }

        public override void Clear()
        {
        }
    }

    /// <summary>
    /// A provider that refuses every write and says by how much, which is what the <c>quota</c>/<c>requested</c>
    /// constructor overload is for.
    /// </summary>
    private sealed class BudgetedStorageProvider : StorageProvider
    {
        public override IReadOnlyList<string> Keys => [];

        public override string? GetItem(string key) => null;

        public override void SetItem(string key, string value)
            => throw new StorageQuotaExceededException("over budget", quota: 100, requested: 140);

        public override void RemoveItem(string key)
        {
        }

        public override void Clear()
        {
        }
    }
}
#endif
