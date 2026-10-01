#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A long <c>a + b</c> produces a deferred representation whose characters are allocated only when
/// something flattens it, and <c>LimitMemory</c> measures allocations — so a deferred result has to be
/// charged when it is <em>built</em>, or a host read of it (<c>ToString()</c>, <c>ToObject()</c>,
/// <c>GetValue(...)</c> after the run) allocates what the script never paid for, outside every
/// constraint (sebastienros/jint#4162).
/// </summary>
/// <remarks>
/// <para>
/// No test here flattens the value it is about: the assertions read <see cref="JsString.Length"/>, which a
/// deferred value answers from the node, so a broken build fails them without allocating the 16 MB a script
/// here can reach. The largest real allocations are the 6 MB host string one test hands in and the 2 MB
/// strings a script reads back or a conversion copies, so the suite needs no heap cap to run a broken build
/// safely.
/// </para>
/// <para>
/// The shapes are the ones a deferred <c>+</c> makes cheap to build and expensive to read: a doubling,
/// whose node count is logarithmic in its length; an accumulator appending one shared operand; a value
/// grown across several entries, each with its own budget; and a host string with a few characters
/// appended. <see cref="AnAccumulatorWithinTheBudgetIsNotChargedQuadratically"/> is the other side of
/// the line: charging a deferred value is not a license to charge it as if every intermediate had been
/// copied, which is the quadratic cost the deferral exists to remove.
/// </para>
/// </remarks>
public class HostDeferredStringMemoryLimitTests
{
    private const long Budget = 4_000_000;

    /// <summary>
    /// 23 doublings of one character: 8,388,608 characters, 16 MB of UTF-16, four times the budget — reached
    /// in 23 statements that allocate a few kilobytes of nodes between them.
    /// </summary>
    private const string Doubling = "var s = 'x'; for (var i = 0; i < 23; i++) { s = s + s; }";

    private static Engine CreateEngine() => new(options => options.LimitMemory(Budget));

    private static long Utf16Bytes(JsValue value) => ((JsString) value).Length * (long) sizeof(char);

    /// <summary>
    /// Where the script leaves the value makes no difference to whether it was charged, and each of these is
    /// a way a host reads it back: the completion value, a global read after the run, a string nested in an
    /// object graph that <c>ToObject()</c> walks.
    /// </summary>
    [TestCase(Doubling + " s", TestName = "{m}(completion value)")]
    [TestCase(Doubling, TestName = "{m}(global binding)")]
    [TestCase(Doubling + " var holder = { a: [s] }; holder", TestName = "{m}(object graph)")]
    public void ADoubledConcatenationIsChargedWhileTheScriptBuildsIt(string script)
    {
        var engine = CreateEngine();

        var failure = Caught.Exception(() => engine.Evaluate(script));

        failure.Should().BeOfType<MemoryLimitExceededException>(
            "a value whose flat form is four times the budget must not leave the engine uncharged");

        // What the failed run left behind is what a host can still read, and reading it costs its flat size.
        Utf16Bytes(engine.GetValue("s")).Should().BeLessThanOrEqualTo(Budget);
    }

    /// <summary>
    /// One 4,096-character operand appended 1,024 times: 4,194,304 characters, 8 MB of UTF-16, from 1,024 nodes
    /// over a single leaf. No sharing of the node itself is needed for the amplification, only of the operand.
    /// </summary>
    [Test]
    public void AnAccumulatorOverOneSharedOperandIsChargedForWhatItAppends()
    {
        var engine = CreateEngine();

        var failure = Caught.Exception(() => engine.Evaluate(
            "var x = 'y'.repeat(4096); var s = ''; for (var i = 0; i < 1024; i++) { s = s + x; } s"));

        failure.Should().BeOfType<MemoryLimitExceededException>();
        Utf16Bytes(engine.GetValue("s")).Should().BeLessThanOrEqualTo(Budget);
    }

    /// <summary>
    /// Eight independent doublings of 1,048,576 characters each: every one of them is half the budget on its
    /// own, and they share nothing, so a host reading all eight copies 16 MB. Refusing only a single value
    /// larger than the budget is not enough; what the values stand for has to be charged as they are built.
    /// </summary>
    [Test]
    public void IndependentConcatenationsShareOneBudget()
    {
        var engine = CreateEngine();

        var failure = Caught.Exception(() => engine.Evaluate("""
            var parts = [];
            for (var k = 0; k < 8; k++) {
                var s = String.fromCharCode(97 + k);
                for (var i = 0; i < 20; i++) { s = s + s; }
                parts.push(s);
            }
            parts
            """));

        failure.Should().BeOfType<MemoryLimitExceededException>(
            "eight unshared values of 2 MB each were built under a 4 MB budget");
    }

    /// <summary>
    /// Each entry has a budget of its own, and each appends only 2 MB — so an accounting that charges what an
    /// entry appends, and nothing more, lets the value grow without bound across entries while each of them
    /// stays within its budget, and the host that reads it at the end pays for all of them. Before deferral
    /// every entry copied the whole value, so an entry could never leave behind a string larger than it was
    /// allowed to allocate; that is the property pinned here.
    /// </summary>
    [Test]
    public void AValueGrownAcrossEntriesNeverOutgrowsOneEntrysBudget()
    {
        var engine = CreateEngine();
        engine.Execute("var piece = 'w'.repeat(1 << 20); var acc = '';");

        Exception? failure = null;
        for (var entry = 0; entry < 8 && failure is null; entry++)
        {
            failure = Caught.Exception(() => engine.Execute("acc = acc + piece;"));
        }

        failure.Should().BeOfType<MemoryLimitExceededException>(
            "eight entries of 2 MB each would otherwise leave a 16 MB value behind a 4 MB budget");
        Utf16Bytes(engine.GetValue("acc")).Should().BeLessThanOrEqualTo(Budget);
    }

    /// <summary>
    /// A host string is not the script's allocation, but a copy of it is: before deferral <c>big + 'x'</c>
    /// copied all of <c>big</c> and was charged for it. Deferred, the script pays for one character and hands
    /// back a value the host has to copy in full to read.
    /// </summary>
    [Test]
    public void AppendingToAHostStringLargerThanTheBudgetIsCharged()
    {
        var engine = CreateEngine();
        engine.SetValue("big", new string('h', 3_000_000));

        var failure = Caught.Exception(() => engine.Evaluate("var r = big + 'x'; r"));

        failure.Should().BeOfType<MemoryLimitExceededException>();
    }

    /// <summary>
    /// The refusal lands in the middle of an expression, and inside a generator or an async function that
    /// expression can be one the frame resumes into: the chain's first operand was evaluated before the
    /// suspension and the whole chain is folded after it. The three operands come to 6 MB flat, so the
    /// chain's second fold is refused. What the script sees is what any exceeded limit gives it, which is
    /// nothing: its <c>catch</c> never runs, the host gets the exception, and the engine takes the next entry.
    /// </summary>
    [TestCase("""
        function* build() {
            try { var r = big + (yield 0) + big; outcome = 'built ' + r.length; }
            catch (e) { outcome = 'caught'; }
        }
        var it = build();
        it.next();
        """, "it.next('x')", TestName = "{m}(generator)")]
    [TestCase("""
        let resolve;
        const gate = new Promise(r => resolve = r);
        (async () => {
            try { var r = big + await gate + big; outcome = 'built ' + r.length; }
            catch (e) { outcome = 'caught'; }
        })();
        """, "resolve('x')", TestName = "{m}(async function)")]
    public void ARefusalInsideAResumedChainReachesTheHostAndNotTheScript(string suspend, string resume)
    {
        var engine = CreateEngine();
        engine.SetValue("big", new string('h', 1_500_000));
        engine.Execute("var outcome = 'pending';\n" + suspend);

        var failure = Caught.Exception(() => engine.Execute(resume));

        failure.Should().BeOfType<MemoryLimitExceededException>(
            "a chain whose flat form is half as large again as the budget must be refused when it is folded");
        engine.GetValue("outcome").AsString().Should().Be("pending");
        engine.Evaluate("outcome + '!'").AsString().Should().Be("pending!");
    }

    /// <summary>
    /// The deferred characters are part of what the operation reports it has used: <c>a + a</c> over a
    /// 1,048,576-character <c>a</c> appends another 2 MB whether or not anything flattens the result.
    /// </summary>
    [Test]
    public void TheCharactersADeferredConcatenationAppendsAreReportedAsAllocated()
    {
        var engine = new Engine(options => options.LimitMemory(64_000_000));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;

        engine.Evaluate("var a = 'q'.repeat(1 << 20); var b = a + a; b.length").Should().Be(2 << 20);

        // 2 MB for a itself, allocated flat by repeat, and 2 MB for the half of b that is not a
        constraint.AllocatedBytes.Should().BeGreaterThanOrEqualTo(4L << 20);
    }

    /// <summary>
    /// The guard on the other side: 10,000 appends of 100 characters build a 1,000,000-character value, and
    /// charging it as if every intermediate had been copied would come to about 10 GB — which is what a plain
    /// <c>+</c> cost before it deferred. Charged for what each step appends it is about 2 MB, plus the 2 MB
    /// the script's own read of the text allocates.
    /// </summary>
    [Test]
    public void AnAccumulatorWithinTheBudgetIsNotChargedQuadratically()
    {
        var engine = new Engine(options => options.LimitMemory(16_000_000));

        var result = engine.Evaluate("""
            var chunk = '0123456789'.repeat(10);
            var s = '';
            for (var i = 0; i < 10000; i++) { s = s + chunk; }
            s.indexOf('z') + s.length
            """);

        result.Should().Be(999_999);
    }

    /// <summary>
    /// What stays open (sebastienros/jint#4175): 64 values over one 1,048,576-character string are each
    /// charged for what they add, so the script stays far inside its budget, and a host <c>ToObject()</c> of
    /// the array would copy 128 MB. The documented bounded read is <see cref="Engine.ConvertResult"/> under
    /// <see cref="ResultLimits"/>, which refuses the first value by its length before copying it — so the
    /// conversion entry itself allocates less than one copy would.
    /// </summary>
    [TestCase("big.slice(i + 1)", TestName = "{m}(slice views)")]
    [TestCase("rope + 'q'", TestName = "{m}(concatenations over one operand)")]
    public void ConvertResultRefusesValuesSharingOneStringBeforeCopyingThem(string element)
    {
        var engine = new Engine(options => options.LimitMemory(16_000_000));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        var value = engine.Evaluate($$"""
            var big = 'x'.repeat(1 << 20);
            var rope = big + 'y';
            var values = [];
            for (var i = 0; i < 64; i++) { values.push({{element}}); }
            values
            """);

        var failure = Caught.Exception(() => engine.ConvertResult(value, ResultLimits.Conservative));

        failure.Should().BeOfType<ResultLimitExceededException>()
            .Which.Limit.Should().Be(ResultLimit.StringLength);
        constraint.AllocatedBytes.Should().BeLessThan(1L << 20, "copying one value would allocate 2 MB");
    }

    /// <summary>
    /// <see cref="ResultLimits.MaxOutputCharacters"/> set on its own is a bound on what the conversion copies,
    /// not only on what it hands back: a slice view and a deferred concatenation answer their length without
    /// flattening, so a string that would take the running total past the limit is refused by that length
    /// before its characters are copied. Each value here is about 1,048,576 characters, a 2 MB copy, against a
    /// limit of 500,000, and the conversion refuses it having allocated next to nothing. The count reported is
    /// the one a copy would have made.
    /// </summary>
    [TestCase("big.slice(1)", (1 << 20) - 1, TestName = "{m}(slice view)")]
    [TestCase("big + 'y'", (1 << 20) + 1, TestName = "{m}(deferred concatenation)")]
    [TestCase("new String(big.slice(1))", (1 << 20) - 1, TestName = "{m}(String object over a slice view)")]
    [TestCase("new String(big + 'y')", (1 << 20) + 1, TestName = "{m}(String object over a deferred concatenation)")]
    public void OutputCharactersAloneRefusesAStringBeforeCopyingIt(string expression, int length)
    {
        var engine = new Engine(options => options.LimitMemory(16_000_000));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        var value = engine.Evaluate($"var big = 'x'.repeat(1 << 20); {expression}");

        var failure = Caught.Exception(() => engine.ConvertResult(value, new ResultLimits { MaxOutputCharacters = 500_000 }));

        var refusal = failure.Should().BeOfType<ResultLimitExceededException>().Which;
        refusal.Limit.Should().Be(ResultLimit.OutputCharacters);
        refusal.Maximum.Should().Be(500_000);
        refusal.Observed.Should().Be(length);
        constraint.AllocatedBytes.Should().BeLessThan(64 * 1024, "copying the value would allocate 2 MB");
    }

    /// <summary>
    /// The running total is checked the same way: 64 slice views of one 1,048,576-character string under a
    /// limit of 1,500,000 characters copy the first, about 2 MB, and refuse the second, which would take the
    /// total to 2,097,149, before copying it — so no conversion copies more characters than the limit, where
    /// counting after each copy let one more string through.
    /// </summary>
    [Test]
    public void OutputCharactersRefusesTheStringThatWouldCrossTheLimitBeforeCopyingIt()
    {
        var engine = new Engine(options => options.LimitMemory(16_000_000));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        var value = engine.Evaluate("""
            var big = 'x'.repeat(1 << 20);
            var values = [];
            for (var i = 0; i < 64; i++) { values.push(big.slice(i + 1)); }
            values
            """);

        var failure = Caught.Exception(() => engine.ConvertResult(value, new ResultLimits { MaxOutputCharacters = 1_500_000 }));

        var refusal = failure.Should().BeOfType<ResultLimitExceededException>().Which;
        refusal.Limit.Should().Be(ResultLimit.OutputCharacters);
        refusal.Observed.Should().Be(1_048_575 + 1_048_574);
        constraint.AllocatedBytes.Should().BeLessThan(
            3_000_000,
            "the value within the limit is copied, 2,097,150 bytes, and a copy of the second would add 2,097,148");
    }
}
