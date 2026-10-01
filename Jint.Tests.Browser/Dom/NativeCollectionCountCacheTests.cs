#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCollectionCountCacheTests
{
    [Test]
    public void AnUnknownCollectionSourceIsNeverCached()
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        var root = fixture.Document.CreateElement("div");
        root.AppendChild(fixture.Document.CreateElement("i"));
        var source = new ObservedCollection(DomChildHtmlCollection.Of(root), witnessed: false);
        var wrapper = Wrap(fixture.Engine, source);
        wrapper.Length.Should().Be(1);
        wrapper.Length.Should().Be(1);
        root.AppendChild(fixture.Document.CreateElement("b"));
        wrapper.Length.Should().Be(2);
        source.Counts.Should().Be(3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HostChecksCanMutateAndReenterDuringACountFillOrHit(bool warm)
    {
        var checks = new MutationCheck();
        using var engine = new Engine(options => options.AddConstraint(checks));
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 3; i++) root.AppendChild(document.CreateElement("i"));
        var source = new ObservedCollection(DomChildHtmlCollection.Of(root), witnessed: true);
        var wrapper = Wrap(engine, source);
        if (warm) wrapper.Length.Should().Be(3);
        uint nested = 0;
        checks.Arm(warm ? 2 : 3, () =>
        {
            root.RemoveChild(root.LastChild!);
            nested = wrapper.Length;
        });
        wrapper.Length.Should().Be(2);
        nested.Should().Be(2);
        source.Counts.Should().Be(2);
        wrapper.Length.Should().Be(2);
        source.Counts.Should().Be(2);
    }

    [Test]
    public void DetachedWritesAdoptionAndSaturationInvalidateCachedCounts()
    {
        using var fixture = DomTestFixture.Create("");
        var sourceDocument = fixture.Document;
        var root = sourceDocument.CreateElement("div");
        root.AppendChild(sourceDocument.CreateElement("i"));
        var source = new ObservedCollection(DomChildHtmlCollection.Of(root), witnessed: true);
        var wrapper = Wrap(fixture.Engine, source);
        wrapper.Length.Should().Be(1);
        root.AppendChild(sourceDocument.CreateElement("b"));
        wrapper.Length.Should().Be(2);
        var oldStamp = sourceDocument.MutationStamp;

        var destination = Document.CreateHtml();
        destination.AdoptNode(root);
        root.RemoveChild(root.LastChild!);
        var stamp = typeof(Document).GetField("_mutationStamp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        stamp.SetValue(destination, oldStamp);
        wrapper.Length.Should().Be(1);
        source.Counts.Should().Be(3);

        stamp.SetValue(destination, ulong.MaxValue);
        wrapper.Length.Should().Be(1);
        root.AppendChild(destination.CreateElement("b"));
        wrapper.Length.Should().Be(2);
        wrapper.Length.Should().Be(2);
        source.Counts.Should().Be(6);
    }

    [Test]
    public void AWarmCountStillRunsHostChecks()
    {
        var checks = new MutationCheck();
        using var engine = new Engine(options => options.AddConstraint(checks));
        DomBindings.Install(engine);
        var root = Document.CreateHtml().CreateElement("div");
        var source = new ObservedCollection(DomChildHtmlCollection.Of(root), witnessed: true);
        var wrapper = Wrap(engine, source);
        wrapper.Length.Should().Be(0);
        checks.Arm(2, () => throw new OperationCanceledException());
        Caught.Exception(() => { _ = wrapper.Length; }).Should().BeOfType<OperationCanceledException>();
        source.Counts.Should().Be(1);
    }

    [Test]
    public void AnUncachedCountChecksTheCurrentTokenAfterTheFinalHostCallback()
    {
        using var cancellation = new CancellationTokenSource();
        var constraint = new Jint.Constraints.CancellationConstraint(default);
        var checks = new MutationCheck();
        // Check the cancellation constraint before the callback changes its token. The collection
        // must then observe the new token itself after GetLength returns from its final host check.
        using var engine = new Engine(options => options.AddConstraint(constraint).AddConstraint(checks));
        DomBindings.Install(engine);
        var root = Document.CreateHtml().CreateElement("div");
        var source = new ObservedCollection(DomChildHtmlCollection.Of(root), witnessed: false);
        var wrapper = Wrap(engine, source);
        checks.Arm(3, () =>
        {
            cancellation.Cancel();
            constraint.Reset(cancellation.Token);
        });
        Caught.Exception(() => { _ = wrapper.Length; }).Should().BeOfType<OperationCanceledException>();
        source.Counts.Should().Be(1);
        constraint.Reset(default);
        wrapper.Length.Should().Be(0);
        source.Counts.Should().Be(2);
    }

    private static DomHtmlCollectionObject<Element> Wrap(Engine engine, ObservedCollection source)
        => (DomHtmlCollectionObject<Element>) DomRealm.Of(engine).WrapCollection<Element>(source);

    private sealed class ObservedCollection(DomHtmlCollection<Element> source, bool witnessed) : DomHtmlCollection<Element>
    {
        internal int Counts { get; private set; }
        internal override int Length
        {
            get { Counts++; return source.Count(); }
        }
        internal override bool TryGetCountWitness(out Document? document, out ulong stamp)
        {
            if (witnessed) return source.TryGetCountWitness(out document, out stamp);
            document = null;
            stamp = 0;
            return false;
        }
        public override IEnumerator<Element> GetEnumerator() => source.GetEnumerator();
    }

    private sealed class MutationCheck : Constraint
    {
        private int _remaining;
        private Action? _callback;
        internal void Arm(int checks, Action callback) { _remaining = checks; _callback = callback; }
        public override void Check()
        {
            if (_remaining == 0 || --_remaining != 0) return;
            var callback = _callback;
            _callback = null;
            callback!();
        }
        public override void Reset() { }
    }
}
