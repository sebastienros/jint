#nullable enable
using System.Reflection;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

// Authored deterministic tests: counts and outcomes, never elapsed-time assertions.
[TestFixture]
public sealed class SelectorInteractionWorkTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void RepeatedMatchesRetainDocumentWitnessesRatherThanEveryCandidate(bool foreign, bool adopt)
    {
        var document = Document.CreateHtml();
        var owner = foreign ? Document.CreateHtml() : document;
        var candidates = Enumerable.Range(0, 1024).Select(_ => owner.CreateElement("div")).ToArray();
        var work = new SelectorMatchWork(document, default, () => { });
        var selector = Parse("div");
        foreach (var candidate in candidates)
            SelectorMatcher.Matches(selector, candidate, null, default, ref work).Should().BeTrue();
        var witnesses = typeof(SelectorMatchWork.Cell).GetField("_observations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(work.EnsureCell()) as System.Collections.ICollection;
        (witnesses?.Count ?? 0).Should().Be(foreign ? 1 : 0);
        if (adopt) Document.CreateHtml().AdoptNode(candidates[0]);
        else candidates[0].SetAttribute("id", "changed");
        Action verify = () => work.VerifyRead();
        verify.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
    }

    private static CompiledSelector Parse(string source) => SelectorCompiler.Compile(source, null, default);
    private static Element Add(Node parent, string name)
    {
        var element = (parent as Document ?? parent.OwnerDocument!).CreateElement(name);
        parent.AppendChild(element);
        return element;
    }

    [Test]
    public void AggregateSelectorAndSlotStepsKeepTheSub256Remainder()
    {
        var document = Document.CreateHtml();
        var host = Add(document, "section");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        Add(root, "slot");
        var input = Add(host, "input");
        input.SetAttribute("slot", new string('x', 100));
        var calls = 0;
        var work = new SelectorMatchWork(host, default, () => calls++);
        for (var i = 0; i < 200; i++) work.Step();
        calls.Should().Be(0);
        SlotAssignment.FindSlot(input, false, work.EnsureCell());
        calls.Should().BeGreaterThanOrEqualTo(3); // entry, aggregate 256, finish
        var fresh = new SelectorMatchWork(host, default, () => calls++);
        var cell = fresh.EnsureCell();
        var before = calls;
        for (var i = 0; i < 80; i++) SlotAssignment.FindSlot(input, false, cell);
        calls.Should().BeGreaterThan(before + 160); // cadence survives short lookup boundaries
    }

    [Test]
    public void MutationAndAdoptionInvalidateBeforePublicationAndFreshWorkRetries()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "main");
        var input = Add(root, "input");
        var mutation = new SelectorMatchWork(root, default, () => input.SetAttribute("id", "changed"));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.QuerySelectorAll(Parse("input"), root, default, ref mutation));
        exception!.Message.Should().Be(SelectorMatchWork.Invalidated);
        var other = Document.CreateHtml();
        var adoption = new SelectorMatchWork(input, default, () => other.AdoptNode(input));
        exception = Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.Matches(Parse("input:nth-child(1)"), input, null, default, ref adoption));
        exception!.Message.Should().Be(SelectorMatchWork.Invalidated);
        var fresh = new SelectorMatchWork(input, default);
        SelectorMatcher.Matches(Parse("input"), input, null, default, ref fresh).Should().BeTrue();
    }

    [Test]
    public void ScopeAndEnvironmentDocumentsAreObservedBeforeCheckpointReads()
    {
        var document = Document.CreateHtml();
        var input = Add(document, "input");
        var other = Document.CreateHtml();
        var scope = Add(other, "section");
        var work = new SelectorMatchWork(input, default, () => scope.SetAttribute("id", "mutation"));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.Matches(Parse("input:nth-child(1)"), input, scope, default, ref work));
        exception!.Message.Should().Be(SelectorMatchWork.Invalidated);
        // An environment is observed only when an environment predicate is read.
        var counter = 0;
        work = new SelectorMatchWork(input, default, () =>
        {
            if (++counter == 1) scope.SetAttribute("id", "next");
        });
        var environment = new SelectorEnvironment(other, scope, null, null);
        exception = Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.Matches(Parse(":focus"), input, null, environment, ref work));
        exception!.Message.Should().Be(SelectorMatchWork.Invalidated);
    }

    [Test]
    public void CheckpointExceptionEscapesUnchangedAndActiveFlagsClear()
    {
        var document = Document.CreateHtml();
        var input = Add(document, "input");
        var sentinel = new ApplicationException("sentinel");
        var once = true;
        var work = new SelectorMatchWork(input, default, () =>
        {
            if (!once) return;
            once = false;
            input.SetAttribute("id", "mutation");
            throw sentinel;
        });
        Assert.Throws<ApplicationException>(() => SelectorMatcher.Matches(Parse("input:nth-child(1)"), input,
            null, default, ref work)).Should().BeSameAs(sentinel);
        // A fresh invocation retries normally; the invalidated work cannot hide mutation.
        var fresh = new SelectorMatchWork(input, default);
        SelectorMatcher.Matches(Parse("input"), input, null, default, ref fresh).Should().BeTrue();
    }

    [Test]
    public void SameWorkReentrancyIsRejectedAndClearsTheInvocationFlag()
    {
        var document = Document.CreateHtml();
        var input = Add(document, "input");
        var program = Parse("input:nth-child(1)");
        var recurse = true;
        var work = default(SelectorMatchWork);
        work = new SelectorMatchWork(input, default, () =>
        {
            if (recurse) SelectorMatcher.Matches(program, input, null, default, ref work);
        });
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.Matches(program, input, null, default, ref work));
        exception!.Message.Should().Be(SelectorMatchWork.AlreadyActive);
        recurse = false;
        SelectorMatcher.Matches(program, input, null, default, ref work).Should().BeTrue();
    }

    [Test]
    public void CancellationWinsOverMutationAtEntryAndExit()
    {
        var document = Document.CreateHtml();
        var input = Add(document, "input");
        using var cancellation = new CancellationTokenSource();
        var work = new SelectorMatchWork(input, cancellation.Token, () =>
        {
            input.SetAttribute("id", "changed");
            cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => SelectorMatcher.Matches(Parse("input:nth-child(1)"), input,
            null, default, ref work));
        Assert.Throws<OperationCanceledException>(() => new SelectorMatchWork(input, cancellation.Token));
        using var exitCancellation = new CancellationTokenSource();
        var checkpoints = 0;
        work = new SelectorMatchWork(input, exitCancellation.Token, () =>
        {
            if (++checkpoints == 2) exitCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => SelectorMatcher.Matches(Parse("input:nth-child(1)"), input,
            null, default, ref work));
    }

    [Test]
    public void SaturatedMutationStampCannotProveFreshness()
    {
        var document = Document.CreateHtml();
        var input = Add(document, "input");
        typeof(Document).GetField("_mutationStamp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(document, ulong.MaxValue);
        var exception = Assert.Throws<InvalidOperationException>(() => new SelectorMatchWork(input, default));
        exception!.Message.Should().Be(SelectorMatchWork.Invalidated);
    }

    [Test]
    public void FinalAncestorAscentAndHugeLabelSpellingPollCancellation()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "main");
        var current = root;
        for (var i = 0; i < 600; i++) current = Add(current, "div");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new SelectorMatchWork(root, cancellation.Token, () =>
        {
            if (++checks == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => SelectorMatcher.QuerySelectorAll(Parse("#absent"),
            root, default, ref work));
        var label = Add(root, "label");
        label.SetAttribute("for", new string('x', 5000));
        var pressed = Add(label, "span");
        using var labelCancellation = new CancellationTokenSource();
        checks = 0;
        work = new SelectorMatchWork(root, labelCancellation.Token, () =>
        {
            if (++checks == 4) labelCancellation.Cancel();
        });
        var environment = new SelectorEnvironment(document, null, pressed, null);
        Assert.Throws<OperationCanceledException>(() => SelectorMatcher.QuerySelectorAll(Parse(":active"),
            root, environment, ref work));
    }

    [Test]
    public void PlainIdIgnoresDeepEnvironmentSeedsWithoutAllocatingState()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "main");
        root.SetAttribute("id", "hit");
        var leaf = root;
        for (var i = 0; i < 1000; i++) leaf = Add(leaf, "div");
        var environment = new SelectorEnvironment(document, leaf, leaf, leaf);
        var program = Parse("#hit");
        var work = new SelectorMatchWork(root, default);
        SelectorMatcher.Matches(program, root, null, environment, ref work).Should().BeTrue();
        // Warm all runtime paths before measuring thread-local allocation, never timing.
        for (var i = 0; i < 10; i++) SelectorMatcher.Matches(program, root);
        var beforeBaseline = GC.GetAllocatedBytesForCurrentThread();
        SelectorMatcher.Matches(program, root);
        var baseline = GC.GetAllocatedBytesForCurrentThread() - beforeBaseline;
        var before = GC.GetAllocatedBytesForCurrentThread();
        work = new SelectorMatchWork(root, default);
        var result = SelectorMatcher.Matches(program, root, null, environment, ref work);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        result.Should().BeTrue();
        allocated.Should().Be(baseline);
    }

    [Test]
    public void ActiveLabelResolutionIsBatchedAcrossCandidatesAndNestedLabels()
    {
        static int Count(int labels, int width)
        {
            var document = Document.CreateHtml();
            var root = Add(document, "main");
            var label = root;
            for (var i = 0; i < labels; i++)
            {
                label = Add(label, "label");
                label.SetAttribute("for", "control" + i);
            }
            var press = Add(label, "span");
            for (var i = 0; i < width; i++)
            {
                var input = Add(root, "input");
                input.SetAttribute("id", "control" + i);
            }
            var checks = 0;
            var work = new SelectorMatchWork(root, default, () => checks++);
            var environment = new SelectorEnvironment(document, null, press, null);
            SelectorMatcher.QuerySelectorAll(Parse(":active"), root, environment, ref work);
            return checks;
        }
        var small = Count(20, 400);
        var doubled = Count(40, 800);
        doubled.Should().BeLessThan(small * 3);
    }
    [Test]
    public void DepthDominantNestedLabelsResolveRootsWithLinearWork()
    {
        static int Count(int depth)
        {
            var document = Document.CreateHtml();
            var root = Add(document, "main");
            var current = root;
            for (var i = 0; i < depth; i++) current = Add(current, "label");
            var pressed = Add(current, "button");
            var checks = 0;
            var work = new SelectorMatchWork(root, default, () => checks++);
            var environment = new SelectorEnvironment(document, null, pressed, null);
            SelectorMatcher.Matches(Parse(":active"), root, null, environment, ref work).Should().BeTrue();
            return checks;
        }
        Count(800).Should().BeLessThan(Count(400) * 3);
    }

}
