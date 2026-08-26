#nullable enable

using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using HostValueVocabularyReach;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="JsValue"/>'s own vocabulary — what a value is, what is in it, and whether reading it worked —
/// is declared on <see cref="JsValue"/>, so a host that imported the namespace the type lives in reaches it
/// by dotting the value. Through 4.16.x every one of these was an extension method in <c>Jint</c>, a
/// different namespace from the type it extended.
/// </summary>
public class HostValueVocabularyTests
{
    /// <summary>
    /// The names that moved onto the type, and what each answers for a value of that kind. The
    /// <c>using Jint;</c> spelling below and the <c>using Jint.Native;</c>-only spelling in
    /// <see cref="ValueDescriber"/> both go through these.
    /// </summary>
    private static readonly string[] _promoted =
    [
        "IsUndefined", "IsNull", "IsString", "IsNumber", "IsBoolean", "IsObject", "IsArray", "IsCallable",
        "IsPromise", "IsDate", "IsRegExp", "IsSymbol", "IsBigInt",
        "AsString", "AsNumber", "AsBoolean", "AsObject", "AsArray",
        "TryGetString", "TryGetNumber", "TryGetBoolean", "TryGetObject", "TryGetArray",
        "UnwrapIfPromise", "UnwrapIfPromiseAsync",
    ];

    [Test]
    public void AHostThatImportedOnlyJintNativeReachesTheVocabulary()
    {
        var engine = new Engine();

        // ValueDescriber lives in a namespace outside the Jint tree with `using Jint.Native;` as its only
        // Jint import, so that it compiling at all is the assertion. See the comment at the top of its file.
        ValueDescriber.Describe(JsValue.Undefined).Should().Be("undefined");
        ValueDescriber.Describe(JsValue.Null).Should().Be("null");
        ValueDescriber.Describe(engine.Evaluate("'text'")).Should().Be("string:text");
        ValueDescriber.Describe(engine.Evaluate("41 + 1")).Should().Be("number:42");
        ValueDescriber.Describe(engine.Evaluate("1 === 1")).Should().Be("boolean:true");
        ValueDescriber.Describe(engine.Evaluate("[1, 2, 3]")).Should().Be("array:3");
        ValueDescriber.Describe(engine.Evaluate("new Date()")).Should().Be("date");
        ValueDescriber.Describe(engine.Evaluate("/a/")).Should().Be("regexp");
        ValueDescriber.Describe(engine.Evaluate("Promise.resolve(1)")).Should().Be("promise");
        ValueDescriber.Describe(engine.Evaluate("Symbol('s')")).Should().Be("symbol");
        ValueDescriber.Describe(engine.Evaluate("1n")).Should().Be("bigint");
        ValueDescriber.Describe(engine.Evaluate("(function () {})")).Should().Be("callable");
        ValueDescriber.Describe(engine.Evaluate("({ a: 1 })")).Should().Be("object");
    }

    [Test]
    public void TheSameHostReachesTheAssertingHalfAndThePromiseUnwrap()
    {
        var engine = new Engine();

        ValueDescriber.DescribeByAsserting(engine.Evaluate("'text'")).Should().Be("string:text");
        ValueDescriber.DescribeByAsserting(engine.Evaluate("41 + 1")).Should().Be("number:42");
        ValueDescriber.DescribeByAsserting(engine.Evaluate("1 === 1")).Should().Be("boolean:true");
        ValueDescriber.DescribeByAsserting(engine.Evaluate("[1, 2, 3]")).Should().Be("array:3");
        ValueDescriber.DescribeByAsserting(engine.Evaluate("({ a: 1, b: 2 })")).Should().Be("object:2");

        ValueDescriber.Settle(engine.Evaluate("Promise.resolve('done')")).AsString().Should().Be("done");
    }

    [Test]
    public void PromotionLeftExactlyOneSpellingOfEachName()
    {
        // A host with `using Jint;` keeps compiling because an instance member wins over an extension
        // method - but only one of the two exists now, so nothing has to win. That is what makes the claim
        // "the call site binds to the same behaviour" checkable rather than asserted: there is nothing else
        // it could bind to.
        var extensionNames = typeof(JsValueExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in _promoted)
        {
            typeof(JsValue).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Should().Contain(method => method.Name == name, $"{name} is declared on JsValue");

            extensionNames.Should().NotContain(name, $"{name} is no longer a second spelling on JsValueExtensions");
        }
    }

    [Test]
    public void AUsingJintHostGetsTheSameAnswersItAlwaysDid()
    {
        // This file has `using Jint;` implicitly, its namespace being nested under it, so every call below
        // is written exactly as a 4.16.x host wrote it.
        var engine = new Engine();

        engine.Evaluate("'text'").IsString().Should().BeTrue();
        engine.Evaluate("new String('text')").IsString().Should().BeFalse();
        engine.Evaluate("42").IsNumber().Should().BeTrue();
        engine.Evaluate("new Number(42)").IsNumber().Should().BeFalse();
        engine.Evaluate("true").IsBoolean().Should().BeTrue();
        engine.Evaluate("({})").IsObject().Should().BeTrue();
        engine.Evaluate("'text'").IsObject().Should().BeFalse();
        engine.Evaluate("[]").IsArray().Should().BeTrue();
        engine.Evaluate("({})").IsArray().Should().BeFalse();
        engine.Evaluate("Symbol('s')").IsSymbol().Should().BeTrue();
        engine.Evaluate("1n").IsBigInt().Should().BeTrue();
        engine.Evaluate("new Date()").IsDate().Should().BeTrue();
        engine.Evaluate("/a/").IsRegExp().Should().BeTrue();
        engine.Evaluate("Promise.resolve()").IsPromise().Should().BeTrue();
        engine.Evaluate("(function () {})").IsCallable().Should().BeTrue();
        JsValue.Undefined.IsUndefined().Should().BeTrue();
        JsValue.Null.IsNull().Should().BeTrue();

        engine.Evaluate("'text'").AsString().Should().Be("text");
        engine.Evaluate("42").AsNumber().Should().Be(42d);
        engine.Evaluate("true").AsBoolean().Should().BeTrue();
        engine.Evaluate("[1]").AsArray().ToArray().Should().HaveCount(1);
        engine.Evaluate("({ a: 1 })").AsObject().Get("a").AsNumber().Should().Be(1d);
        engine.Evaluate("Promise.resolve(7)").UnwrapIfPromise().AsNumber().Should().Be(7d);
    }

    [Test]
    public void TryGetStringAnswersOnAHitAndDeclinesOnAMiss()
    {
        var engine = new Engine();

        engine.Evaluate("'text'").TryGetString(out var hit).Should().BeTrue();
        hit.Should().Be("text");

        // A String wrapper object is not a string primitive, the same distinction IsString() draws.
        engine.Evaluate("new String('text')").TryGetString(out var miss).Should().BeFalse();
        miss.Should().BeNull();

        JsValue.Undefined.TryGetString(out _).Should().BeFalse();
    }

    [Test]
    public void TryGetNumberAnswersOnAHitAndDeclinesOnAMiss()
    {
        var engine = new Engine();

        engine.Evaluate("40 + 2").TryGetNumber(out var hit).Should().BeTrue();
        hit.Should().Be(42d);

        engine.Evaluate("1.5").TryGetNumber(out var fractional).Should().BeTrue();
        fractional.Should().Be(1.5d);

        engine.Evaluate("'42'").TryGetNumber(out var miss).Should().BeFalse();
        miss.Should().Be(0d);
    }

    [Test]
    public void TryGetBooleanAnswersOnAHitAndDeclinesOnAMiss()
    {
        var engine = new Engine();

        engine.Evaluate("false").TryGetBoolean(out var hit).Should().BeTrue();
        hit.Should().BeFalse();

        engine.Evaluate("1 === 1").TryGetBoolean(out var alsoHit).Should().BeTrue();
        alsoHit.Should().BeTrue();

        // Truthiness is not the question - a non-boolean declines rather than being coerced.
        engine.Evaluate("1").TryGetBoolean(out var miss).Should().BeFalse();
        miss.Should().BeFalse();
    }

    [Test]
    public void TryGetObjectAnswersOnAHitAndDeclinesOnAMiss()
    {
        var engine = new Engine();

        engine.Evaluate("({ a: 1 })").TryGetObject(out var hit).Should().BeTrue();
        hit.Should().BeAssignableTo<ObjectInstance>();
        hit!.Get("a").AsNumber().Should().Be(1d);

        engine.Evaluate("'text'").TryGetObject(out var miss).Should().BeFalse();
        miss.Should().BeNull();
    }

    [Test]
    public void TryGetArrayAnswersOnAHitAndDeclinesOnAMiss()
    {
        var engine = new Engine();

        engine.Evaluate("[1, 2]").TryGetArray(out var hit).Should().BeTrue();
        hit!.ToArray().Should().HaveCount(2);

        engine.Evaluate("({ length: 2 })").TryGetArray(out var arrayLike).Should().BeFalse();
        arrayLike.Should().BeNull();

        // Array.isArray follows a Proxy; the concrete-type question this pair is phrased in does not, so
        // that TryGetArray can hand back a JsArray at all.
        engine.Evaluate("new Proxy([], {})").TryGetArray(out var proxied).Should().BeFalse();
        proxied.Should().BeNull();
    }

    [Test]
    public void TheArrayGuardAndTheArrayCastNowAgree()
    {
        var engine = new Engine();

        // Array.prototype is an array exotic object, so the specification's IsArray counts it - which is
        // what AsArray used to guard with, before failing its own cast with an InvalidCastException.
        // IsArray() and AsArray() are the same question now, and the miss is the documented one.
        engine.Evaluate("Array.prototype").IsArray().Should().BeFalse();
        Invoking(() => engine.Evaluate("Array.prototype").AsArray())
            .Should().Throw<ArgumentException>().WithMessage("The value is not an array");

        Invoking(() => engine.Evaluate("new Proxy([], {})").AsArray())
            .Should().Throw<ArgumentException>().WithMessage("The value is not an array");

        // Script's own answer is unchanged, and still the proxy-following one.
        engine.Evaluate("Array.isArray(Array.prototype)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Array.isArray(new Proxy([], {}))").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheSpecialisedAccessorsStayedExtensionsInTheSameNamespace()
    {
        var engine = new Engine();

        // One `using Jint.Native;` reaches both halves, so which half a member is in is a maintenance
        // question rather than something a host has to know.
        engine.Evaluate("new Int32Array([1, 2])").AsInt32Array().Should().Equal(1, 2);
        engine.Evaluate("new Date(0)").AsDate().ToObject().Should().BeOfType<DateTime>();
        engine.Evaluate("(function () { return 1; })").Call().AsNumber().Should().Be(1d);
        engine.Evaluate("'text'").IsPrimitive().Should().BeTrue();
    }
}
