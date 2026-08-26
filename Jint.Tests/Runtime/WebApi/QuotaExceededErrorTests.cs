#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>QuotaExceededError</c> as WebIDL specifies it — https://webidl.spec.whatwg.org/#quotaexceedederror, the
/// one <c>DOMException</c>-derived interface the standard defines.
/// </summary>
/// <remarks>
/// Like <c>DOMException</c>, it is installed whenever <i>any</i> web API is enabled rather than behind a flag
/// of its own: it is how several of them report a refusal, so it exists wherever one can be thrown.
/// <see cref="WebApiFeatures.Console"/> is simply the cheapest way to ask for an engine here.
/// </remarks>
public class QuotaExceededErrorTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Console));

    private static string Err(Engine engine, string body)
    {
        engine.Execute("function __err(f) { try { f(); return 'no error'; } catch (e) { return e.name; } }");
        return engine.Evaluate("__err(function() { " + body + " })").AsString();
    }

    // ---------------------------------------------------------------- the interface object

    [Test]
    public void HasTheIdlArity()
    {
        var engine = WebEngine();

        // constructor(optional DOMString message = "", optional QuotaExceededErrorOptions options = {}) —
        // `length` counts required arguments only, and neither of the two is one.
        engine.Evaluate("QuotaExceededError.length").AsNumber().Should().Be(0);
        engine.Evaluate("QuotaExceededError.name").AsString().Should().Be("QuotaExceededError");
    }

    [Test]
    public void RequiresNew()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("QuotaExceededError('x')"))!
            .Message.Should().Contain("requires 'new'");
    }

    [Test]
    public void InheritsTheDomExceptionInterfaceObjectAndItsPrototype()
    {
        var engine = WebEngine();

        // https://webidl.spec.whatwg.org/#interface-object — an interface that inherits has the inherited
        // interface object as its [[Prototype]], and the inherited prototype object as its prototype's.
        engine.Evaluate("Object.getPrototypeOf(QuotaExceededError) === DOMException").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(QuotaExceededError.prototype) === DOMException.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("QuotaExceededError.prototype.constructor === QuotaExceededError").AsBoolean().Should().BeTrue();

        engine.Evaluate("new QuotaExceededError() instanceof QuotaExceededError").AsBoolean().Should().BeTrue();
        engine.Evaluate("new QuotaExceededError() instanceof DOMException").AsBoolean().Should().BeTrue();
        engine.Evaluate("new QuotaExceededError() instanceof Error").AsBoolean().Should().BeTrue();

        // ... and not the other way round: a DOMException is a base instance, not one of these.
        engine.Evaluate("new DOMException('x', 'QuotaExceededError') instanceof QuotaExceededError").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ReachesTheInterfaceObjectHoweverTheTwoWereFirstTouched()
    {
        // Reaching QuotaExceededError builds DOMException, since it is its [[Prototype]] — so the identity
        // holds whichever global the script mentions first.
        WebEngine().Evaluate("Object.getPrototypeOf(QuotaExceededError) === DOMException").AsBoolean().Should().BeTrue();

        var other = WebEngine();
        other.Evaluate("DOMException");
        other.Evaluate("Object.getPrototypeOf(QuotaExceededError) === DOMException").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DeclaresNoConstantsOfItsOwnButInheritsAllTwentyFive()
    {
        var engine = WebEngine();

        // The 25 legacy constants belong to DOMException's IDL; a derived interface object reaches them
        // through its [[Prototype]] rather than redeclaring them.
        engine.Evaluate("Object.getOwnPropertyNames(QuotaExceededError).filter(function (n) { return /_ERR$/.test(n); }).length")
            .AsNumber().Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyNames(QuotaExceededError.prototype).filter(function (n) { return /_ERR$/.test(n); }).length")
            .AsNumber().Should().Be(0);

        engine.Evaluate("QuotaExceededError.QUOTA_EXCEEDED_ERR").AsNumber().Should().Be(22);
        engine.Evaluate("QuotaExceededError.prototype.INDEX_SIZE_ERR").AsNumber().Should().Be(1);
        engine.Evaluate("new QuotaExceededError().DATA_CLONE_ERR").AsNumber().Should().Be(25);
    }

    [Test]
    public void IsAWebIdlInterfaceObjectOnTheGlobal()
    {
        var engine = WebEngine();
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("QuotaExceededError");

        // Lazy and unmaterialized until something names it, exactly like every other web-API global.
        descriptor.Should().BeOfType<Jint.Runtime.Descriptors.Specialized.LazyPropertyDescriptor<Engine>>();
        descriptor._value.Should().BeNull();

        // https://webidl.spec.whatwg.org/#es-interfaces — writable and configurable, not enumerable.
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeFalse();
        descriptor.Configurable.Should().BeTrue();
    }

    [Test]
    public void ExistsWhereverAnyWebApiDoes()
    {
        // No flag of its own: DOMException and this are how the rest report a failure.
        new Engine(options => options.UseWebApis(WebApiFeatures.Base64)).Evaluate("typeof QuotaExceededError")
            .AsString().Should().Be("function");

        // ... and on a default engine there are no web APIs at all.
        new Engine().Evaluate("typeof QuotaExceededError").AsString().Should().Be("undefined");
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = WebEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof QuotaExceededError')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof QuotaExceededError").AsString().Should().Be("function");
    }

    [Test]
    public void DoesNotClobberAGlobalTheHostAlreadyOwns()
    {
        var marker = new Jint.Native.JsString("host's own QuotaExceededError");

        var engine = new Engine(options => options
            .AddLazyGlobal("QuotaExceededError", _ => marker)
            .UseWebApis(WebApiFeatures.Console));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("QuotaExceededError").Should().BeSameAs(marker);

        // DOMException beside it is still installed, so the two names are independent.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    // ---------------------------------------------------------------- name, message and code

    [Test]
    public void CarriesTheInterfaceIdentifierAsItsName()
    {
        var engine = WebEngine();

        // Constructor step 1: "Set this's name to QuotaExceededError" — the name is not an argument, unlike
        // DOMException's.
        engine.Evaluate("new QuotaExceededError().name").AsString().Should().Be("QuotaExceededError");
        engine.Evaluate("new QuotaExceededError('boom').message").AsString().Should().Be("boom");
        engine.Evaluate("new QuotaExceededError().message").AsString().Should().Be("");

        // An explicitly passed undefined takes the IDL default too.
        engine.Evaluate("new QuotaExceededError(undefined).message").AsString().Should().Be("");
        engine.Evaluate("new QuotaExceededError(42).message").AsString().Should().Be("42");
    }

    [Test]
    public void ReportsTheLegacyCodeTwentyTwo()
    {
        var engine = WebEngine();

        // "The QuotaExceededError interface inherits the DOMException interface's code getter, which will
        // always return 22" — the one place a DOMException-derived interface does not report 0, because the
        // name is still in the legacy table (marked deprecated, pointing here).
        engine.Evaluate("new QuotaExceededError().code").AsNumber().Should().Be(22);
        engine.Evaluate("Object.getOwnPropertyDescriptor(DOMException.prototype, 'code').get.call(new QuotaExceededError())")
            .AsNumber().Should().Be(22);
    }

    [Test]
    public void InheritsErrorPrototypeToStringAndTheErrorDataSlot()
    {
        var engine = WebEngine();

        engine.Evaluate("String(new QuotaExceededError('boom'))").AsString().Should().Be("QuotaExceededError: boom");
        engine.Evaluate("Error.isError(new QuotaExceededError())").AsBoolean().Should().BeTrue();

        // A prototype object is not an instance, in this hierarchy as in every other.
        engine.Evaluate("Error.isError(QuotaExceededError.prototype)").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void TagsItselfForObjectPrototypeToString()
    {
        var engine = WebEngine();

        engine.Evaluate("Object.prototype.toString.call(new QuotaExceededError())").AsString()
            .Should().Be("[object QuotaExceededError]");
        engine.Evaluate("QuotaExceededError.prototype[Symbol.toStringTag]").AsString().Should().Be("QuotaExceededError");
    }

    [Test]
    public void CarriesAStackAndOwnsNothingElse()
    {
        var engine = WebEngine();

        var stack = engine.Evaluate("function make() { return new QuotaExceededError('x'); } make().stack").AsString();
        stack.Should().NotBeNullOrEmpty();
        stack.Should().Contain("at make");

        engine.Evaluate("new QuotaExceededError().hasOwnProperty('stack')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyNames(new QuotaExceededError()).join(',')").AsString().Should().Be("stack");
    }

    // ---------------------------------------------------------------- quota and requested

    [Test]
    public void HasBothMembersNullUntilSomethingSuppliesThem()
    {
        var engine = WebEngine();

        // "Every QuotaExceededError instance has a requested and a quota, both numbers or null. They are both
        // initially null."
        engine.Evaluate("new QuotaExceededError().quota").IsNull().Should().BeTrue();
        engine.Evaluate("new QuotaExceededError().requested").IsNull().Should().BeTrue();
        engine.Evaluate("new QuotaExceededError('x', {}).quota").IsNull().Should().BeTrue();

        // An absent member and an explicit undefined are the same thing to a dictionary conversion.
        engine.Evaluate("new QuotaExceededError('x', { quota: undefined, requested: undefined }).requested")
            .IsNull().Should().BeTrue();
    }

    [Test]
    public void ReadsBackWhatTheOptionsDictionaryGaveIt()
    {
        var engine = WebEngine();

        engine.Evaluate("new QuotaExceededError('x', { quota: 8, requested: 9 }).quota").AsNumber().Should().Be(8);
        engine.Evaluate("new QuotaExceededError('x', { quota: 8, requested: 9 }).requested").AsNumber().Should().Be(9);

        // Either alone is fine — the pair rule only bites when both are present.
        engine.Evaluate("new QuotaExceededError('x', { quota: 8 }).quota").AsNumber().Should().Be(8);
        engine.Evaluate("new QuotaExceededError('x', { quota: 8 }).requested").IsNull().Should().BeTrue();
        engine.Evaluate("new QuotaExceededError('x', { requested: 1 }).requested").AsNumber().Should().Be(1);
        engine.Evaluate("new QuotaExceededError('x', { requested: 1 }).quota").IsNull().Should().BeTrue();

        // A `double` member takes ToNumber, so a numeric string and a valueOf both convert.
        engine.Evaluate("new QuotaExceededError('x', { quota: '8' }).quota").AsNumber().Should().Be(8);
        engine.Evaluate("new QuotaExceededError('x', { quota: 1.5, requested: 2.5 }).quota").AsNumber().Should().Be(1.5);

        // Equal is not less, so a request that exactly fills the quota is well-formed.
        engine.Evaluate("new QuotaExceededError('x', { quota: 8, requested: 8 }).requested").AsNumber().Should().Be(8);

        // -0 is not less than 0, so it is accepted and read back as itself.
        engine.Evaluate("Object.is(new QuotaExceededError('x', { quota: -0 }).quota, -0)").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ExposesTheTwoMembersAsPrototypeAccessors()
    {
        var engine = WebEngine();

        // WebIDL attributes live on the interface prototype object, so the instance owns neither.
        engine.Evaluate("new QuotaExceededError('x', { quota: 1 }).hasOwnProperty('quota')").AsBoolean().Should().BeFalse();

        foreach (var member in new[] { "quota", "requested" })
        {
            var descriptor = $"Object.getOwnPropertyDescriptor(QuotaExceededError.prototype, '{member}')";
            engine.Evaluate($"typeof {descriptor}.get").AsString().Should().Be("function");
            engine.Evaluate($"{descriptor}.set").IsUndefined().Should().BeTrue();
            engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeTrue();
            engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeTrue();
        }
    }

    [Test]
    public void DeclaresItsOwnMembersInIdlOrderAndInheritsTheRest()
    {
        var engine = WebEngine();

        // The prototype object carries the interface's own members and nothing else — `name`, `message` and
        // `code` are DOMException's and are reached one level up.
        engine.Evaluate("Object.getOwnPropertyNames(QuotaExceededError.prototype).join(',')").AsString()
            .Should().Be("constructor,quota,requested");
    }

    [TestCase("QuotaExceededError.prototype")]
    [TestCase("new DOMException('x', 'QuotaExceededError')")]
    [TestCase("Object.create(QuotaExceededError.prototype)")]
    [TestCase("{}")]
    public void RefusesAnAccessorReceiverThatIsNotOne(string receiver)
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(
                () => engine.Evaluate($"Object.getOwnPropertyDescriptor(QuotaExceededError.prototype, 'quota').get.call({receiver})"))!
            .Message.Should().Contain("QuotaExceededError");

        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate($"Object.getOwnPropertyDescriptor(QuotaExceededError.prototype, 'requested').get.call({receiver})"));
    }

    // ---------------------------------------------------------------- constructor validation

    // Steps 3.1 and 4.1: a negative amount is not a quantity either member can describe.
    [TestCase("new QuotaExceededError('x', { quota: -1 })")]
    [TestCase("new QuotaExceededError('x', { requested: -1 })")]
    [TestCase("new QuotaExceededError('x', { quota: -1, requested: 5 })")]
    // Step 5: a request smaller than the quota it exceeded cannot be what happened.
    [TestCase("new QuotaExceededError('x', { quota: 9, requested: 8 })")]
    [TestCase("new QuotaExceededError('x', { quota: 1, requested: 0 })")]
    public void RefusesAnIllFormedPairWithARangeError(string expression)
    {
        Err(WebEngine(), expression).Should().Be("RangeError");
    }

    // https://webidl.spec.whatwg.org/#js-double — a `double` member refuses NaN and the infinities, which is
    // a TypeError from the conversion rather than a RangeError from a numbered step.
    [TestCase("new QuotaExceededError('x', { quota: NaN })")]
    [TestCase("new QuotaExceededError('x', { requested: Infinity })")]
    [TestCase("new QuotaExceededError('x', { quota: -Infinity })")]
    [TestCase("new QuotaExceededError('x', { quota: 'nonsense' })")]
    // A dictionary argument that is neither an object nor undefined nor null is refused outright, step 1.
    [TestCase("new QuotaExceededError('x', 5)")]
    [TestCase("new QuotaExceededError('x', 'quota')")]
    [TestCase("new QuotaExceededError('x', true)")]
    // ToNumber on a symbol is a TypeError of ECMAScript's own.
    [TestCase("new QuotaExceededError('x', { quota: Symbol() })")]
    public void RefusesAValueTheConversionCannotTakeWithATypeError(string expression)
    {
        Err(WebEngine(), expression).Should().Be("TypeError");
    }

    [Test]
    public void TakesUndefinedAndNullAsTheEmptyDictionary()
    {
        var engine = WebEngine();

        engine.Evaluate("new QuotaExceededError('x', undefined).quota").IsNull().Should().BeTrue();
        engine.Evaluate("new QuotaExceededError('x', null).requested").IsNull().Should().BeTrue();

        // A function is an object, so it is a perfectly good — if empty — dictionary.
        engine.Evaluate("new QuotaExceededError('x', function () {}).quota").IsNull().Should().BeTrue();
    }

    [Test]
    public void ConvertsTheWholeDictionaryBeforeAnyNumberedStepRuns()
    {
        var engine = WebEngine();

        // The binding converts the argument first, so every TypeError the conversion can raise precedes the
        // RangeError step 3.1 would have raised for the quota it already read.
        Err(engine, "new QuotaExceededError('x', { quota: -1, requested: NaN })").Should().Be("TypeError");

        // And the members are read in *lexicographical* order — quota, then requested — which is what
        // https://webidl.spec.whatwg.org/#js-dictionary specifies and what a pair of getters can observe.
        engine.Evaluate("""
            var seen = [];
            try {
                new QuotaExceededError('x', {
                    get requested() { seen.push('requested'); return 1; },
                    get quota() { seen.push('quota'); return 1; }
                });
            } catch (e) { seen.push(e.name); }
            seen.join(',')
            """).AsString().Should().Be("quota,requested");

        // A getter that throws stops the conversion where it stands, so the later member is never read.
        engine.Evaluate("""
            var reached = false;
            try {
                new QuotaExceededError('x', {
                    get quota() { throw new RangeError('from the getter'); },
                    get requested() { reached = true; return 1; }
                });
            } catch (e) { }
            reached
            """).AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ReadsTheMembersThroughThePrototypeChainLikeAnyDictionary()
    {
        var engine = WebEngine();

        // "Properties on the object (or its prototype chain) correspond to dictionary members."
        engine.Evaluate("new QuotaExceededError('x', Object.create({ quota: 4, requested: 7 })).quota")
            .AsNumber().Should().Be(4);
    }

    [Test]
    public void SubclassesThroughNewTarget()
    {
        var engine = WebEngine();

        // OrdinaryCreateFromConstructor, so a subclass gets its own prototype and keeps the internal state.
        engine.Evaluate("""
            class TooBig extends QuotaExceededError {}
            var e = new TooBig('x', { quota: 1, requested: 2 });
            [e instanceof TooBig, e instanceof QuotaExceededError, e.name, e.code, e.quota, e.requested].join('|')
            """).AsString().Should().Be("true|true|QuotaExceededError|22|1|2");
    }

    // ---------------------------------------------------------------- structured clone

    [Test]
    public void IsSerializableAndKeepsBothMembers()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        // "QuotaExceededError objects are serializable objects", and their steps run DOMException's and then
        // carry [[Quota]] and [[Requested]] — https://webidl.spec.whatwg.org/#quotaexceedederror.
        engine.Evaluate("""
            var clone = structuredClone(new QuotaExceededError('boom', { quota: 8, requested: 9 }));
            [
                clone instanceof QuotaExceededError,
                clone.constructor === QuotaExceededError,
                clone.name,
                clone.message,
                clone.code,
                clone.quota,
                clone.requested
            ].join('|')
            """).AsString().Should().Be("true|true|QuotaExceededError|boom|22|8|9");

        // Both null survives as both null rather than becoming undefined.
        engine.Evaluate("""
            var bare = structuredClone(new QuotaExceededError('x'));
            [bare.quota === null, bare.requested === null].join('|')
            """).AsString().Should().Be("true|true");
    }

    [Test]
    public void DoesNotFlattenIntoADomExceptionAndIsNotConfusedWithOne()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        // The other direction: a DOMException that merely wears the name is *not* the interface, and cloning
        // one must not promote it.
        engine.Evaluate("""
            var clone = structuredClone(new DOMException('x', 'QuotaExceededError'));
            [
                clone instanceof QuotaExceededError,
                clone.constructor === DOMException,
                clone.name,
                clone.code,
                'quota' in clone
            ].join('|')
            """).AsString().Should().Be("false|true|QuotaExceededError|22|false");
    }

    [Test]
    public void KeepsTheStackOfTheExceptionItCloned()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));

        engine.Evaluate("""
            function raise() { return new QuotaExceededError('x'); }
            var clone = structuredClone(raise());
            clone.stack.indexOf('at raise') >= 0
            """).AsBoolean().Should().BeTrue();
    }
}
#endif
