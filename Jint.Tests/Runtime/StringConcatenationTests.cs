using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>s += x</c> and <c>s = s + x</c> mean the same thing, and used to differ by an order of magnitude:
/// the compound assignment builds into a <see cref="JsString.ConcatenatedString"/>, which is
/// <see cref="System.Text.StringBuilder"/>-backed and amortised linear, while a plain <c>+</c> produced a
/// flat string per operation and so copied the whole accumulated left operand on every iteration.
/// Prepending (<c>s = x + s</c>) had no fast path at all, because the compound form cannot express it.
/// <para>
/// A plain <c>+</c> now produces <c>JsString.RopeString</c> once the result is long enough to be worth
/// deferring — an <em>immutable</em> node holding the two operands, which is what makes it safe where the
/// mutable builder is not: a <c>+</c> leaves both operands reachable from wherever they were read, so a
/// representation it can share has to be one nobody can append into. These tests pin the two halves of
/// that: the value is indistinguishable from the flat string it stands for, and building it is linear.
/// </para>
/// </summary>
public class StringConcatenationTests
{
    private const string Chunk = "var chunk = '0123456789';";

    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(Chunk);
        return engine;
    }

    /// <summary>
    /// The premise of everything below: an accumulator built with <c>+</c> is left as a deferred node
    /// rather than copied. If the interpreter ever starts flattening this shape, these tests would keep
    /// passing while covering nothing.
    /// </summary>
    [Fact]
    public void AccumulatingWithPlusLeavesTheValueDeferred()
    {
        var value = CreateEngine().Evaluate("var s = ''; for (var i = 0; i < 200; i++) { s = s + chunk; } s;");

        value.Should().BeOfType<JsString.RopeString>();
        value.ToString().Length.Should().Be(2000);
    }

    [Fact]
    public void PrependingWithPlusLeavesTheValueDeferred()
    {
        var value = CreateEngine().Evaluate("var s = ''; for (var i = 0; i < 200; i++) { s = chunk + s; } s;");

        value.Should().BeOfType<JsString.RopeString>();
        value.ToString().Length.Should().Be(2000);
    }

    /// <summary>
    /// A short result is still allocated flat: below the deferral threshold a node costs more than the
    /// copy it saves, and every consumer of the result would pay the flattening indirection for nothing.
    /// </summary>
    [Fact]
    public void AShortConcatenationIsStillFlat()
    {
        var engine = CreateEngine();

        engine.Evaluate("'a' + 'b'").Should().BeOfType<JsString>();
        engine.Evaluate("'a' + 'b' + 'c'").Should().BeOfType<JsString>();
        engine.Evaluate($"'x'.repeat({JsString.MinDeferredConcatenationLength - 2}) + 'y'").Should().BeOfType<JsString>();
    }

    /// <summary>
    /// Every accumulation shape has to produce exactly what the compound assignment produces — including
    /// the chain forms, which are evaluated as one flattened node rather than nested pairwise additions.
    /// </summary>
    [Theory]
    [InlineData("s += chunk", 1000, 10000)]
    [InlineData("s = s + chunk", 1000, 10000)]
    [InlineData("s = chunk + s", 1000, 10000)]
    [InlineData("s = s + chunk + chunk", 500, 10000)]
    [InlineData("s = s + chunk + chunk + chunk", 400, 12000)]
    [InlineData("s = s + chunk + chunk + chunk + chunk + chunk", 200, 10000)]
    [InlineData("s = chunk + chunk + s", 500, 10000)]
    public void EveryAccumulationShapeProducesTheSameCharacters(string body, int iterations, int expectedLength)
    {
        var engine = CreateEngine();

        var built = engine
            .Evaluate($"(function () {{ var s = ''; for (var i = 0; i < {iterations}; i++) {{ {body}; }} return s; }})()")
            .AsString();

        built.Length.Should().Be(expectedLength);
        built.Should().Be(string.Concat(Enumerable.Repeat("0123456789", expectedLength / 10)));
    }

    /// <summary>
    /// The row above uses one repeated piece, so it cannot see an operand swapped for its sibling.
    /// These build from a piece that changes every iteration, and assert against the two orders
    /// spelled out — the one thing a concatenation node can get wrong that a length check will not show.
    /// </summary>
    [Fact]
    public void AppendAndPrependPutTheOperandsInTheRightOrder()
    {
        var engine = CreateEngine();

        var appended = engine
            .Evaluate("(function () { var s = ''; for (var i = 0; i < 400; i++) { s = s + ('[' + i + ']'); } return s; })()")
            .AsString();
        var prepended = engine
            .Evaluate("(function () { var s = ''; for (var i = 0; i < 400; i++) { s = ('[' + i + ']') + s; } return s; })()")
            .AsString();

        var pieces = Enumerable.Range(0, 400).Select(i => "[" + i + "]").ToArray();
        appended.Should().Be(string.Concat(pieces));
        prepended.Should().Be(string.Concat(Enumerable.Reverse(pieces)));
    }

    /// <summary>
    /// Every <c>String.prototype</c> method, and every engine path that needs characters, has to see the
    /// deferred node as the string it stands for. Only the length is answered without flattening.
    /// </summary>
    [Theory]
    [InlineData("s.length", "2000")]
    [InlineData("s.charCodeAt(0)", "48")]
    [InlineData("s.charCodeAt(1999)", "57")]
    [InlineData("s.charAt(11)", "1")]
    [InlineData("s[12]", "2")]
    [InlineData("s.indexOf('789012')", "7")]
    [InlineData("s.lastIndexOf('0123')", "1990")]
    [InlineData("s.slice(5, 15)", "5678901234")]
    [InlineData("s.substring(0, 4)", "0123")]
    [InlineData("s.startsWith('01234')", "true")]
    [InlineData("s.endsWith('56789')", "true")]
    [InlineData("s.includes('456')", "true")]
    [InlineData("s.split('0').length", "201")]
    [InlineData("s.toUpperCase().length", "2000")]
    [InlineData("JSON.stringify(s).length", "2002")]
    [InlineData("(s === s + '')", "true")]
    [InlineData("`${s}`.length", "2000")]
    [InlineData("(+s.slice(0, 3))", "12")]
    [InlineData("s.replace('012', 'zzz').slice(0, 5)", "zzz34")]
    [InlineData("[...s].length", "2000")]
    [InlineData("s.match(/9(0)/)[1]", "0")]
    [InlineData("(s == s.valueOf())", "true")]
    public void EveryStringOperationSeesTheFlatValue(string expression, string expected)
    {
        var engine = CreateEngine();
        engine.Execute("var s = ''; for (var i = 0; i < 200; i++) { s = s + chunk; }");

        engine.Evaluate("s").Should().BeOfType<JsString.RopeString>();
        engine.Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The hazard the node's immutability exists to avoid, from the other side: an operand that is itself
    /// a <see cref="JsString.ConcatenatedString"/> is still growable, and <c>+=</c> appends into it in
    /// place. A node that simply held the reference would change content behind whoever read the
    /// concatenation, so the operand is snapshotted on the way in.
    /// </summary>
    [Fact]
    public void AppendingToAnOperandAfterwardsDoesNotChangeTheResult()
    {
        var engine = CreateEngine();
        engine.Execute("""
            var a = ['']; a[0] += chunk.repeat(60); a[0] += chunk.repeat(60);
            """);
        engine.Evaluate("a[0]").Should().BeOfType<JsString.ConcatenatedString>();

        engine.Execute("var b = a[0] + '!';");
        engine.Evaluate("b").Should().BeOfType<JsString.RopeString>();

        engine.Execute("a[0] += 'MUTATED';");

        engine.Evaluate("b.length").AsNumber().Should().Be(1201);
        engine.Evaluate("b.indexOf('MUTATED')").AsNumber().Should().Be(-1);
        engine.Evaluate("b === chunk.repeat(120) + '!'").AsBoolean().Should().BeTrue();
        engine.Evaluate("a[0].length").AsNumber().Should().Be(1207);
    }

    /// <summary>
    /// A deferred node has to hash and compare like the flat string it stands for, or it cannot be used
    /// interchangeably as a property key or a collection key. Content, not representation, is the
    /// identity of a string value.
    /// </summary>
    [Fact]
    public void ADeferredNodeIsIndistinguishableFromTheFlatString()
    {
        var engine = CreateEngine();
        var deferred = engine.Evaluate("var s = ''; for (var i = 0; i < 60; i++) { s = s + chunk; } s;");
        deferred.Should().BeOfType<JsString.RopeString>();

        var flat = new JsString(string.Concat(Enumerable.Repeat("0123456789", 60)));

        deferred.Equals(flat).Should().BeTrue();
        flat.Equals(deferred).Should().BeTrue();
        deferred.GetHashCode().Should().Be(flat.GetHashCode());
        deferred.ToString().Should().Be(flat.ToString());

        // and the same after it has flattened, which replaces what the base bodies read
        deferred.ToString();
        deferred.Equals(flat).Should().BeTrue();
        deferred.GetHashCode().Should().Be(flat.GetHashCode());
    }

    [Fact]
    public void ADeferredNodeWorksAsAKeyOnBothSidesOfALookup()
    {
        var engine = CreateEngine();
        engine.Execute("""
            var deferred = ''; for (var i = 0; i < 60; i++) { deferred = deferred + chunk; }
            var flat = ''; for (var i = 0; i < 60; i++) { flat += chunk; }
            flat = flat.slice(0);
            """);
        engine.Evaluate("deferred").Should().BeOfType<JsString.RopeString>();

        engine.Evaluate("new Map([[deferred, 1]]).get(flat)").AsNumber().Should().Be(1);
        engine.Evaluate("new Map([[flat, 1]]).get(deferred)").AsNumber().Should().Be(1);
        engine.Evaluate("new Set([deferred]).has(flat)").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Set([deferred, flat]).size").AsNumber().Should().Be(1);
        engine.Evaluate("var o = {}; o[deferred] = 7; o[flat]").AsNumber().Should().Be(7);
        engine.Evaluate("Object.keys(o)[0].length").AsNumber().Should().Be(600);
    }

    /// <summary>
    /// A long loop builds a completely unbalanced tree — one node per iteration — and flattening it must
    /// not put a frame on the CLR stack per node. Both leanings are covered because they are mirror
    /// images: appending leans left, prepending leans right, and a walk that is cheap on one is the one
    /// that has to keep an explicit stack for the other.
    /// </summary>
    [Theory]
    [InlineData("s = s + 'ab'")]
    [InlineData("s = 'ab' + s")]
    public void AVeryDeepTreeFlattensWithoutRecursing(string body)
    {
        var engine = CreateEngine();

        engine.Execute($"var s = ''; for (var i = 0; i < 200000; i++) {{ {body}; }}");

        engine.Evaluate("s.length").AsNumber().Should().Be(400000);
        engine.Evaluate("s.charCodeAt(0)").AsNumber().Should().Be('a');
        engine.Evaluate("s.charCodeAt(399999)").AsNumber().Should().Be('b');
        engine.Evaluate("s === 'ab'.repeat(200000)").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A tree whose operands are shared — <c>s = s + s</c> doubles by referencing the same node twice, so
    /// it is a DAG rather than a tree — flattens to the whole value, not to one copy per distinct node.
    /// </summary>
    [Fact]
    public void SharedOperandsFlattenToTheWholeValue()
    {
        var engine = CreateEngine();

        var value = engine.Evaluate("var s = chunk; for (var i = 0; i < 8; i++) { s = s + s; } s;");

        value.Should().BeOfType<JsString.RopeString>();
        value.AsString().Should().Be(string.Concat(Enumerable.Repeat("0123456789", 256)));
    }

    /// <summary>
    /// The asymptotic claim, measured deterministically. Wall-clock is the obvious way to show a
    /// quadratic is gone and the worst way to assert it in a test suite; allocated bytes say the same
    /// thing without depending on what else the machine is doing, because the quadratic <em>is</em> the
    /// copying — every iteration allocated a fresh string holding the whole accumulator.
    /// <para>
    /// Doubling the iteration count doubles the work, so linear building doubles the allocation and the
    /// quadratic shape quadrupled it. The bound is deliberately loose: anything under 4 would have
    /// failed before, and the absolute ceiling is 40× under what the quadratic shape allocated at this
    /// size, so neither assertion is close enough to the truth to be fragile.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("s = s + chunk")]
    [InlineData("s = chunk + s")]
    [InlineData("s = s + chunk + chunk")]
    public void AccumulatingWithPlusIsNoLongerQuadratic(string body)
    {
        if (!GCPolyfills.AllocatedBytesForCurrentThreadIsSupported)
        {
            // true on every runtime this suite executes on (.NET Framework 4.6+, .NET Core 2.0+); the
            // guard is here so a trimmed or exotic host running the suite reports no result rather than
            // a failure it cannot act on
            return;
        }

        var small = AllocatedBytesAccumulating(body, 4_000);
        var large = AllocatedBytesAccumulating(body, 8_000);

        // 8,000 iterations of 10 characters copied in full every time is ~640 MB
        large.Should().BeLessThan(16 * 1024 * 1024);
        ((double) large / small).Should().BeLessThan(3.0);
    }

    private static long AllocatedBytesAccumulating(string body, int iterations)
    {
        var engine = CreateEngine();
        var script = Engine.PrepareScript($"(function (n) {{ var s = ''; for (var i = 0; i < n; i++) {{ {body}; }} return s.length; }})(n)");

        // warm the handler tree and the call-site caches, so what is measured is the loop and not the
        // one-off cost of reaching it
        engine.SetValue("n", 16);
        engine.Evaluate(script);

        engine.SetValue("n", iterations);
        GC.Collect();
        GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var before);
        engine.Evaluate(script);
        GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var after);

        return after - before;
    }
}
