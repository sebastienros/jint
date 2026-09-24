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
/// the guard's own script reads back, so the suite needs no heap cap to run a broken build safely.
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
}
