#nullable enable
using System.Collections;
using System.Reflection;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorControlFactsTests
{
    private static CompiledSelector Parse(string text) => SelectorCompiler.Compile(text, null, default);
    private delegate SelectorControlFacts ReadFacts(Element element, SelectorControlFactMask mask, ref SelectorMatchWork work);

    private sealed class Factory : ISelectorControlFactsFactory, ISelectorControlFacts
    {
        internal ulong Revision;
        internal int Creates;
        internal int RevisionReads;
        internal readonly List<(Element Element, SelectorControlFactMask Mask)> Reads = [];
        internal SelectorControlFacts Facts;
        internal ReadFacts? OnRead;
        internal Action? OnCreate;
        internal readonly object Context = new();
        internal SelectorEnvironment Seed(Document document) => new(document, null, null, null, this, Context, Revision);
        public ulong ReadRevision(object context, Document document)
        {
            RevisionReads++;
            return Revision;
        }
        public ISelectorControlFacts Create(object context, Document document, ulong capturedRevision)
        {
            context.Should().BeSameAs(Context);
            capturedRevision.Should().Be(Revision);
            Creates++;
            OnCreate?.Invoke();
            return this;
        }
        public SelectorControlFacts Read(Element element, SelectorControlFactMask requested, ref SelectorMatchWork work)
        {
            Reads.Add((element, requested));
            return OnRead is { } read ? read(element, requested, ref work) : Facts;
        }
    }

    [TestCase("div")]
    [TestCase(":is(div, :valid)")]
    [TestCase(":default")]
    public void UnreachedHostArmsDoNotCreateAProducer(string selector)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var factory = new Factory();
        var work = new SelectorMatchWork(element, default);
        SelectorMatcher.Matches(Parse(selector), element, null, factory.Seed(document), ref work)
            .Should().Be(selector != ":default");
        factory.Creates.Should().Be(0);
        factory.Reads.Should().BeEmpty();
    }

    [TestCase(":default")]
    [TestCase(":placeholder-shown")]
    [TestCase(":read-only")]
    [TestCase(":read-write")]
    [TestCase(":valid")]
    [TestCase(":invalid")]
    [TestCase(":in-range")]
    [TestCase(":out-of-range")]
    [TestCase(":dir(ltr)")]
    public void HostlessPreflightRefusesEveryControlFamilyIncludingUnreachedBranches(string selector)
    {
        var element = Document.CreateHtml().CreateElement("div");
        var program = Parse(":is(div," + selector + ")");
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(program, element))!
            .Message.Should().Contain("no matching evaluator");
        SelectorMatcher.Supports(Parse(selector), new CssValueWork(default)).Should().BeFalse();
        SelectorMatcher.Supports(Parse(selector), new CssValueWork(default), true).Should().BeTrue();
    }

    [Test]
    public void RequestedFamiliesMergeAndShareOneProducerAcrossElements()
    {
        var document = Document.CreateHtml();
        var first = document.CreateElement("input");
        var second = document.CreateElement("input");
        var factory = new Factory
        {
            Facts = new(ReadWrite: true, Validity: SelectorControlValidity.Valid, Range: SelectorControlRange.InRange, RightToLeft: true)
        };
        var work = new SelectorMatchWork(first, default);
        var seed = factory.Seed(document);
        SelectorMatcher.Matches(Parse(":valid:read-write:in-range:dir(rtl)"), first, null, seed, ref work).Should().BeTrue();
        factory.Facts = default; // Unrequested fields must not overwrite earlier family answers.
        SelectorMatcher.Matches(Parse(":valid:read-write:in-range:dir(rtl):not(:invalid):not(:out-of-range):not(:placeholder-shown)"),
            first, null, seed, ref work).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":invalid"), second, null, seed, ref work).Should().BeFalse();
        factory.Creates.Should().Be(1);
        factory.Reads.Select(x => x.Mask).Should().Equal(SelectorControlFactMask.Validity,
            SelectorControlFactMask.ReadWrite, SelectorControlFactMask.Range, SelectorControlFactMask.Directionality, SelectorControlFactMask.PlaceholderShown,
            SelectorControlFactMask.Validity);
        factory.Reads[^1].Element.Should().BeSameAs(second);
    }

    [Test]
    public void FilteredNthSharesControlReadsAndRejectsRevisionChanges()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("form");
        document.AppendChild(root);
        var items = new Element[512];
        for (var i = 0; i < items.Length; i++) root.AppendChild(items[i] = document.CreateElement("input"));
        var factory = new Factory { Facts = new(Validity: SelectorControlValidity.Valid) };
        var seed = factory.Seed(document);
        var work = new SelectorMatchWork(document, default);
        var program = Parse(":nth-child(odd of :valid)");
        for (var i = 0; i < items.Length; i++)
            SelectorMatcher.Matches(program, items[i], null, seed, ref work).Should().Be(i % 2 == 0);
        factory.Creates.Should().Be(1);
        factory.Reads.Should().HaveCount(items.Length);
        Witnesses(ref work).Should().Be(0);
        // A control revision is independent of the document mutation stamp.
        factory.Revision++;
        Action stale = () => SelectorMatcher.Matches(program, items[0], null, seed, ref work);
        stale.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        var fresh = new SelectorMatchWork(document, default);
        factory.Facts = new(Validity: SelectorControlValidity.Invalid);
        SelectorMatcher.Matches(program, items[0], null, factory.Seed(document), ref fresh).Should().BeFalse();
    }

    private static int Witnesses(ref SelectorMatchWork work)
        => (typeof(SelectorMatchWork.Cell).GetField("_observations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(work.EnsureCell()) as ICollection)?.Count ?? 0;

    [Test]
    public void CandidateReadsKeepObservationStorageConstantAndVerificationLinear()
    {
        static int Count(int candidates)
        {
            var document = Document.CreateHtml();
            var root = document.CreateElement("main");
            document.AppendChild(root);
            for (var i = 0; i < candidates; i++) root.AppendChild(document.CreateElement("input"));
            var factory = new Factory { Facts = new(Validity: SelectorControlValidity.Valid) };
            var work = new SelectorMatchWork(root, default);
            SelectorMatcher.QuerySelectorAll(Parse(":valid"), root, factory.Seed(document), ref work)
                .Should().HaveCount(candidates);
            // Each Verify scans these witnesses. Candidate count must not grow that scan;
            // the document stamp also covers adoption of any detached/ordinary candidate.
            Witnesses(ref work).Should().Be(0);
            factory.Reads.Should().HaveCount(candidates);
            return factory.RevisionReads;
        }
        var small = Count(128);
        small.Should().BeGreaterThan(128);
        Count(256).Should().BeLessThan(small * 3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AdditionalDocumentsRemainGuardedWithoutOneWitnessPerCandidate(bool adopt)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var other = Document.CreateHtml();
        var candidates = Enumerable.Range(0, 128).Select(_ => other.CreateElement("input")).ToArray();
        var factory = new Factory { Facts = new(Validity: SelectorControlValidity.Valid) };
        var work = new SelectorMatchWork(root, default);
        work.Enter(root, null, factory.Seed(document));
        try
        {
            foreach (var candidate in candidates)
                work.ReadControlFacts(candidate, SelectorControlFactMask.Validity).Validity.Should().Be(SelectorControlValidity.Valid);
            Witnesses(ref work).Should().Be(1); // One extra document, not 128 element identities.
            if (adopt) Document.CreateHtml().AdoptNode(candidates[0]);
            else candidates[0].SetAttribute("id", "changed");
            Assert.Throws<InvalidOperationException>(() => work.VerifyRead())!.Message.Should().Be(SelectorMatchWork.Invalidated);
        }
        finally { work.Exit(); }
    }

    [TestCase((int) SelectorControlValidity.NotApplicable, false, false)]
    [TestCase((int) SelectorControlValidity.Valid, true, false)]
    [TestCase((int) SelectorControlValidity.Invalid, false, true)]
    public void ValidityPreservesApplicability(int validity, bool valid, bool invalid)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory { Facts = new(Validity: (SelectorControlValidity) validity) };
        var work = new SelectorMatchWork(element, default);
        var seed = factory.Seed(document);
        SelectorMatcher.Matches(Parse(":valid"), element, null, seed, ref work).Should().Be(valid);
        SelectorMatcher.Matches(Parse(":invalid"), element, null, seed, ref work).Should().Be(invalid);
        factory.Reads.Should().HaveCount(1);
    }

    [TestCase((int) SelectorControlRange.NotApplicable, false, false)]
    [TestCase((int) SelectorControlRange.InRange, true, false)]
    [TestCase((int) SelectorControlRange.OutOfRange, false, true)]
    public void RangePreservesApplicability(int range, bool inside, bool outside)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory { Facts = new(Range: (SelectorControlRange) range) };
        var work = new SelectorMatchWork(element, default);
        var seed = factory.Seed(document);
        SelectorMatcher.Matches(Parse(":in-range"), element, null, seed, ref work).Should().Be(inside);
        SelectorMatcher.Matches(Parse(":out-of-range"), element, null, seed, ref work).Should().Be(outside);
        factory.Reads.Should().HaveCount(1);
    }

    [Test]
    public void NativeDefaultArmsUseContentAttributesDespiteDirtyStateAndKeepHostCold()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.InitializeParsedAttributes(new ParserAttribute[]
        {
            new(null, "type", null, "checkbox"), new(null, "checked", null, "")
        }, default);
        HtmlCheckableState.Get(input)!.SetChecked(false, default);
        var option = document.CreateElement("option");
        option.InitializeParsedAttributes(new ParserAttribute[] { new(null, "selected", null, "") }, default);
        var core = option.ExistingOptionCore!;
        core.SetSelected(false, default);
        var factory = new Factory();
        var seed = factory.Seed(document);
        var work = new SelectorMatchWork(input, default);
        SelectorMatcher.Matches(Parse(":default"), input, null, seed, ref work).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":default"), option, null, seed, ref work).Should().BeTrue();
        input.ExistingInputValueState.Should().BeNull();
        option.ExistingOptionState.Should().BeNull();
        factory.Creates.Should().Be(0);
        var button = document.CreateElement("button");
        factory.Facts = new(DefaultSubmit: true);
        SelectorMatcher.Matches(Parse(":default"), button, null, seed, ref work).Should().BeTrue();
        factory.Reads.Select(x => x.Mask).Should().Equal(SelectorControlFactMask.DefaultSubmit);
    }

    [Test]
    public void ReadOnlyIsHtmlScopedWhileForeignEditingCanBeReadWrite()
    {
        var document = Document.CreateHtml();
        var foreign = document.CreateElementNS(Namespaces.Svg, "text");
        var html = document.CreateElement("div");
        var factory = new Factory();
        var seed = factory.Seed(document);
        var work = new SelectorMatchWork(foreign, default);
        SelectorMatcher.Matches(Parse(":read-only"), foreign, null, seed, ref work).Should().BeFalse();
        factory.Creates.Should().Be(0);
        SelectorMatcher.Matches(Parse(":read-only"), html, null, seed, ref work).Should().BeTrue();
        factory.Facts = new(ReadWrite: true);
        SelectorMatcher.Matches(Parse(":read-write"), foreign, null, seed, ref work).Should().BeTrue();
    }

    [Test]
    public void CachedFactsRejectAChangedSemanticRevisionAndFreshWorkRetries()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory { Facts = new(Validity: SelectorControlValidity.Valid) };
        var seed = factory.Seed(document);
        var work = new SelectorMatchWork(element, default);
        var program = Parse(":valid");
        SelectorMatcher.Matches(program, element, null, seed, ref work).Should().BeTrue();
        factory.Revision++;
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(program, element, null, seed, ref work))!
            .Message.Should().Be(SelectorMatchWork.Invalidated);
        factory.Reads.Should().HaveCount(1);
        work = new SelectorMatchWork(element, default);
        SelectorMatcher.Matches(program, element, null, factory.Seed(document), ref work).Should().BeTrue();
        factory.Creates.Should().Be(2);
    }

    [TestCase("factory")]
    [TestCase("context")]
    [TestCase("document")]
    [TestCase("revision")]
    public void WorkReuseRequiresTheExactSeedIdentity(string changed)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var factory = new Factory();
        var seed = factory.Seed(document);
        var work = new SelectorMatchWork(element, default);
        var program = Parse("div");
        SelectorMatcher.Matches(program, element, null, seed, ref work).Should().BeTrue();
        var replacement = changed switch
        {
            "factory" => seed with { ControlFactsFactory = new Factory() },
            "context" => seed with { ControlFactsContext = new object() },
            "document" => seed with { Document = Document.CreateHtml() },
            _ => seed with { ControlFactsRevision = 1 }
        };
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(program, element, null, replacement, ref work))!
            .Message.Should().Be(SelectorMatchWork.Invalidated);
        factory.Creates.Should().Be(0);
    }

    [Test]
    public void CopiesSharingACellCannotReplaceItsSeed()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var factory = new Factory();
        var work = new SelectorMatchWork(element, default, () => { });
        var copy = work; // Cell exists, but neither copy has bound an environment yet.
        SelectorMatcher.Matches(Parse("div"), element, null, factory.Seed(document), ref work).Should().BeTrue();
        var replacement = factory.Seed(document) with { ControlFactsContext = new object() };
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(Parse("div"), element,
            null, replacement, ref copy))!.Message.Should().Be(SelectorMatchWork.Invalidated);
        factory.Creates.Should().Be(0);
    }

    [Test]
    public void WarmedCacheChecksRejectRevisionChangesOnBothSidesOfTheCallback()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory { Facts = new(Validity: SelectorControlValidity.Valid) };
        var checks = 0;
        var work = new SelectorMatchWork(element, default, () => { checks++; factory.Revision++; });
        SelectorMatcher.Matches(Parse(":valid"), element, null, factory.Seed(document), ref work).Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => work.Check())!.Message.Should().Be(SelectorMatchWork.Invalidated);
        checks.Should().Be(1);
        Assert.Throws<InvalidOperationException>(() => work.Check())!.Message.Should().Be(SelectorMatchWork.Invalidated);
        checks.Should().Be(1);
        factory.Reads.Should().HaveCount(1);
    }

    [Test]
    public void SaturatedSemanticRevisionCannotEstablishFreshness()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var factory = new Factory { Revision = ulong.MaxValue };
        var work = new SelectorMatchWork(element, default);
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(Parse("div"), element,
            null, factory.Seed(document), ref work))!.Message.Should().Be(SelectorMatchWork.Invalidated);
        factory.Creates.Should().Be(0);
    }

    [TestCase("create")]
    [TestCase("read")]
    [TestCase("mutation")]
    [TestCase("adoption")]
    public void ProducerChangesRejectPublication(string change)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory();
        if (change == "create") factory.OnCreate = () => factory.Revision++;
        else factory.OnRead = (Element node, SelectorControlFactMask _, ref SelectorMatchWork _) =>
        {
            if (change == "mutation") node.SetAttribute("id", "changed");
            else if (change == "adoption") Document.CreateHtml().AdoptNode(node);
            else factory.Revision++;
            return new(Validity: SelectorControlValidity.Valid);
        };
        var seed = factory.Seed(document);
        var work = new SelectorMatchWork(element, default);
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(Parse(":valid"), element,
            null, seed, ref work))!.Message.Should().Be(SelectorMatchWork.Invalidated);
        factory.Reads.Should().HaveCount(change == "create" ? 0 : 1);
    }

    [Test]
    public void EveryActualCheckpointGuardsSemanticRevisionBeforeTheNextCallback()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory
        {
            OnRead = (Element _, SelectorControlFactMask _, ref SelectorMatchWork work) =>
            {
                work.BeginControlProducerRead()(600); // Cadence polls plus the producer boundary.
                return new(Validity: SelectorControlValidity.Valid);
            }
        };
        var checks = 0;
        var work = new SelectorMatchWork(element, default, () => { checks++; factory.Revision++; });
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(Parse(":valid"), element,
            null, factory.Seed(document), ref work))!.Message.Should().Be(SelectorMatchWork.Invalidated);
        checks.Should().Be(1);
    }

    [Test]
    public void FreshHelperCursorsPreserveAggregateRemainderAndCheckpointGuards()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory
        {
            OnRead = (Element _, SelectorControlFactMask _, ref SelectorMatchWork work) =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var checkpoint = work.BeginControlProducerRead();
                    checkpoint(60);
                    checkpoint(80);
                }
                return new(PlaceholderShown: true);
            }
        };
        var checks = 0;
        var work = new SelectorMatchWork(element, default, () => checks++);
        SelectorMatcher.Matches(Parse(":placeholder-shown"), element, null, factory.Seed(document), ref work).Should().BeTrue();
        checks.Should().Be(23); // Twenty boundaries and floor((800 + selector steps) / 256).
    }

    [Test]
    public void CancellationWinsOverSemanticMutationAtProducerCheckpoints()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory
        {
            OnRead = (Element _, SelectorControlFactMask _, ref SelectorMatchWork work) =>
            {
                work.BeginControlProducerRead()(1);
                return default;
            }
        };
        using var cancellation = new CancellationTokenSource();
        var work = new SelectorMatchWork(element, cancellation.Token, () =>
        {
            factory.Revision++;
            cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => SelectorMatcher.Matches(Parse(":valid"), element,
            null, factory.Seed(document), ref work));
    }

    [Test]
    public void ProducerReentrancyIsRejectedAndInvocationFlagsClear()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        var factory = new Factory();
        var seed = factory.Seed(document);
        var program = Parse(":valid");
        factory.OnRead = (Element node, SelectorControlFactMask _, ref SelectorMatchWork work) =>
        {
            SelectorMatcher.Matches(program, node, null, seed, ref work);
            return default;
        };
        var work = new SelectorMatchWork(element, default);
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(program, element, null, seed, ref work))!
            .Message.Should().Be(SelectorMatchWork.AlreadyActive);
        factory.OnRead = null;
        factory.Facts = new(Validity: SelectorControlValidity.Valid);
        SelectorMatcher.Matches(program, element, null, seed, ref work).Should().BeTrue();
    }
}
