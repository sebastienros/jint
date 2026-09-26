#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class CheckedInvocationCheckpointTests
{
    private static Element Input(Document document, string type = "radio", string name = "g", bool check = false)
    {
        var input = document.CreateElement("input");
        var attributes = new List<ParserAttribute> { new(null, "type", null, type), new(null, "name", null, name) };
        if (check) attributes.Add(new(null, "checked", null, ""));
        input.InitializeParsedAttributes(attributes.ToArray(), default);
        return input;
    }

    [TestCase("get")]
    [TestCase("checked")]
    [TestCase("default")]
    [TestCase("unchecked")]
    [TestCase("indeterminate")]
    [TestCase("facts")]
    public void ColdMetadataUsesRealCallerWithoutInstallingDocumentProbe(string operation)
    {
        var document = Document.CreateHtml(); var input = document.CreateElement("input");
        input.InitializeParsedAttributes(Enumerable.Range(0, 1000)
            .Select(i => new ParserAttribute(null, $"data-{i}", null, "x"))
            .Append(new ParserAttribute(null, "type", null, "checkbox")).ToArray(), default);
        var stamp = document.MutationStamp; using var cancellation = new CancellationTokenSource();
        var counts = new List<int>();
        void Run(Action<int> checkpoint, CancellationToken token)
        {
            if (operation == "get") HtmlCheckableState.Get(input, checkpoint, token);
            else if (operation == "checked") HtmlCheckableState.MatchesChecked(input, checkpoint, token);
            else if (operation == "default") HtmlCheckableState.MatchesDefaultCheckable(input, checkpoint, token);
            else if (operation == "unchecked") HtmlCheckableState.MatchesUnchecked(input, checkpoint, token);
            else if (operation == "indeterminate") HtmlCheckableState.MatchesIndeterminate(input, checkpoint, token);
            else HtmlCheckableState.GetRadioGroupFacts(input, checkpoint, token);
        }
        var exception = Assert.Throws<OperationCanceledException>(() => Run(n => { counts.Add(n); cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Should().Equal(256);
        input.ExistingCheckedState.Should().BeNull(); document.CheckedWorkProbe.Should().BeNull();
        document.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => Run(_ => throw new InvalidOperationException("budget"), default));
        input.ExistingCheckedState.Should().BeNull(); document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ColdSidecarTailChecksBeforePublicationAndCountsActualAttributes()
    {
        var document = Document.CreateHtml(); var input = Input(document, "checkbox", check: true);
        var stamp = document.MutationStamp; using var cancellation = new CancellationTokenSource();
        var counts = new List<int>();
        Assert.Throws<OperationCanceledException>(() => input.GetHtmlState()!.GetCheckedState(n => { counts.Add(n); cancellation.Cancel(); }, cancellation.Token));
        counts.Should().Equal(3); input.ExistingCheckedState.Should().BeNull();
        counts.Clear(); var state = HtmlCheckableState.Get(input, counts.Add, default)!;
        counts.Should().Equal(3); state.Checked.Should().BeTrue(); state.DirtyCheckedness.Should().BeFalse();
        document.MutationStamp.Should().Be(stamp);
    }

    [TestCase("name")]
    [TestCase("tree")]
    public void ColdGroupBuildSharesActualTraversalAndNameWork(string stage)
    {
        var document = Document.CreateHtml(); var root = document.CreateElement("div");
        var input = Input(document, name: stage == "name" ? new string('x', 10000) : "g"); root.AppendParsedChild(input);
        if (stage == "tree") for (var i = 0; i < 1000; i++) root.AppendParsedChild(document.CreateComment("x"));
        var stamp = document.MutationStamp; using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        var exception = Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.GetRadioGroupFacts(input,
            n => { counts.Add(n); if (n >= 256) cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Last().Should().Be(256);
        root.RadioIndex.Should().BeNull(); input.ExistingCheckedState!.Index.Should().BeNull();
        input.ExistingCheckedState.Checked.Should().BeFalse(); document.MutationStamp.Should().Be(stamp);
        HtmlCheckableState.GetRadioGroupFacts(input, default).MemberCount.Should().Be(1);
    }

    [TestCase("snapshot")]
    [TestCase("first")]
    public void WarmGroupTraversalUsesInvocationCallback(string operation)
    {
        var document = Document.CreateHtml(); var root = document.CreateElement("div");
        var first = Input(document); root.AppendParsedChild(first);
        for (var i = 0; i < 1000; i++) root.AppendParsedChild(document.CreateComment("x"));
        var last = Input(document, check: true); root.AppendParsedChild(last);
        root.AppendParsedChild(Input(document, check: true));
        HtmlCheckableState.GetRadioGroupFacts(first, default);
        var stamp = document.MutationStamp; using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() =>
        {
            Action<int> checkpoint = n => { if (n >= 256) cancellation.Cancel(); };
            if (operation == "snapshot") HtmlCheckableState.SnapshotRadioGroup(first, checkpoint, cancellation.Token);
            else HtmlCheckableState.FirstCheckedRadio(last, checkpoint, cancellation.Token);
        });
        document.MutationStamp.Should().Be(stamp);
    }

    [TestCase(3)]
    [TestCase(600)]
    public void ExclusionChecksPeersAndFinalTailBeforeAnyFlagCommit(int size)
    {
        var document = Document.CreateHtml(); var root = document.CreateElement("div");
        for (var i = 0; i < size; i++) root.AppendParsedChild(Input(document, check: true));
        var first = (Element) root.FirstChild!;
        HtmlCheckableState.GetRadioGroupFacts(first, default).CheckedCount.Should().Be(size);
        var state = HtmlCheckableState.Get(first)!; var stamp = document.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        var exception = Assert.Throws<OperationCanceledException>(() => state.SetChecked(true, n => { counts.Add(n); if (n > 0) cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Should().Equal(0, Math.Min(size, 256));
        state.DirtyCheckedness.Should().BeFalse(); HtmlCheckableState.GetRadioGroupFacts(first, default).CheckedCount.Should().Be(size);
        document.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => state.SetChecked(true, _ => throw new InvalidOperationException("budget"), default));
        HtmlCheckableState.GetRadioGroupFacts(first, default).CheckedCount.Should().Be(size); document.MutationStamp.Should().Be(stamp);
    }

    [TestCase("snapshot")]
    [TestCase("exclusion")]
    public void WarmLargeGroupChecksBeforeKnownSizeAllocation(string operation)
    {
        var document = Document.CreateHtml(); var root = document.CreateElement("div");
        for (var i = 0; i < 20000; i++) root.AppendParsedChild(Input(document, check: true));
        var input = (Element) root.FirstChild!;
        HtmlCheckableState.GetRadioGroupFacts(input, default).CheckedCount.Should().Be(20000);
        var state = HtmlCheckableState.Get(input)!; var stamp = document.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            Action<int> checkpoint = n => { counts.Add(n); cancellation.Cancel(); };
            if (operation == "snapshot") HtmlCheckableState.SnapshotRadioGroup(input, checkpoint, cancellation.Token);
            else state.SetChecked(true, checkpoint, cancellation.Token);
        });
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().BeLessThan(65536);
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Should().Equal(0);
        state.DirtyCheckedness.Should().BeFalse();
        HtmlCheckableState.GetRadioGroupFacts(input, default).CheckedCount.Should().Be(20000);
        document.MutationStamp.Should().Be(stamp);
    }

    [TestCase("set")]
    [TestCase("reset")]
    [TestCase("copy")]
    [TestCase("indeterminate")]
    public void ConstantFlagOperationsStillCheckTheCallerBeforeCommit(string operation)
    {
        var document = Document.CreateHtml(); var source = Input(document, "checkbox", check: true);
        var input = Input(document, "checkbox"); var state = HtmlCheckableState.Get(input)!;
        HtmlCheckableState.Get(source); var stamp = document.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            Action<int> checkpoint = _ => cancellation.Cancel();
            if (operation == "set") HtmlCheckednessAlgorithms.Set(input, true, HtmlCheckedChangeOrigin.UserInteraction, checkpoint, cancellation.Token);
            else if (operation == "reset") HtmlCheckednessAlgorithms.ResetCheckedness(input, checkpoint, cancellation.Token);
            else if (operation == "copy") HtmlCheckednessAlgorithms.CopyCheckedness(source, input, checkpoint, cancellation.Token);
            else state.SetIndeterminate(true, checkpoint, cancellation.Token);
        });
        exception!.CancellationToken.Should().Be(cancellation.Token);
        state.Checked.Should().BeFalse(); state.DirtyCheckedness.Should().BeFalse(); state.Indeterminate.Should().BeFalse();
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void WarmCallbacksAllocateNothingAndLeaveDocumentProbeUnused()
    {
        var document = Document.CreateHtml(); var root = document.CreateElement("div"); var input = Input(document);
        root.AppendParsedChild(input); HtmlCheckableState.GetRadioGroupFacts(input, default);
        Action<int> checkpoint = static _ => { };
        HtmlCheckableState.GetRadioGroupFacts(input, checkpoint, default);
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var stamp = document.MutationStamp;
        for (var i = 0; i < 1000; i++)
        {
            HtmlCheckableState.GetRadioGroupFacts(input, checkpoint, default);
            HtmlCheckableState.Get(input, checkpoint, default);
            HtmlCheckableState.MatchesChecked(input, checkpoint, default);
        }
        (GC.GetAllocatedBytesForCurrentThread() - bytes).Should().Be(0);
        document.CheckedWorkProbe.Should().BeNull(); document.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => HtmlCheckableState.GetRadioGroupFacts(input, _ => throw new InvalidOperationException("budget"), default));
    }
}
