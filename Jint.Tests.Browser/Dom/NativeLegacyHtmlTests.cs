#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeLegacyHtmlTests
{
    [TestCase(null, -1)]
    [TestCase("", -1)]
    [TestCase("0", -1)]
    [TestCase("-1", -1)]
    [TestCase("-2", -1)]
    [TestCase("  +12tail", 12)]
    [TestCase("+0002suffix", 2)]
    [TestCase("1.5", 1)]
    [TestCase("\u00a01", -1)]
    [TestCase("2147483647", int.MaxValue)]
    [TestCase("2147483648", -1)]
    [TestCase("-2147483648", -1)]
    [TestCase("+", -1)]
    public void MarqueeLoopReadsCurrentAttributeIntegerPrefix(string? raw, int expected)
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var marquee = Document.CreateHtml().CreateElement("marquee");
        if (raw is not null) marquee.SetAttributeNS(null, "loop", raw);
        BrowserLegacyHtmlMembers.GetMarqueeLoop(realm, marquee).Should().Be(expected);
    }

    [Test]
    public void MarqueeLoopSetterIgnoresInvalidValuesAndDoesNotRewriteEquivalentRawValue()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var marquee = Document.CreateHtml().CreateElement("marquee");
        marquee.SetAttributeNS(null, "loop", " +0012tail");
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, 12);
        marquee.GetAttributeNS(null, "loop").Should().Be(" +0012tail");
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, 0);
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, -2);
        marquee.GetAttributeNS(null, "loop").Should().Be(" +0012tail");
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, -1);
        marquee.GetAttributeNS(null, "loop").Should().Be("-1");
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, 3);
        marquee.GetAttributeNS(null, "loop").Should().Be("3");
        BrowserLegacyHtmlMembers.GetMarqueeLoop(realm, marquee).Should().Be(3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MarqueeLoopParsingChecksLongDigitPrefixBeforeSetterMutation(bool overflow)
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var marquee = Document.CreateHtml().CreateElement("marquee");
        var raw = overflow ? new string('9', 8192) : new string('0', 8192) + "1";
        marquee.SetAttributeNS(null, "loop", raw);
        probe.Remaining = 2;
        Caught.Exception(() => BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, 3)).Should().BeOfType<OperationCanceledException>();
        marquee.GetAttributeNS(null, "loop").Should().Be(raw);
        probe.Remaining = 0;
        BrowserLegacyHtmlMembers.GetMarqueeLoop(realm, marquee).Should().Be(overflow ? -1 : 1);
        probe.Remaining = 2;
        Caught.Exception(() => BrowserLegacyHtmlMembers.GetMarqueeLoop(realm, Document.CreateHtml().CreateElement("marquee"))).Should().BeOfType<OperationCanceledException>();
    }

    [TestCase(null)]
    [TestCase("invalid")]
    [TestCase("0")]
    [TestCase("-12junk")]
    public void EquivalentMinusOnePreservesMissingOrInvalidRawLoop(string? raw)
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var marquee = Document.CreateHtml().CreateElement("marquee");
        if (raw is not null) marquee.SetAttributeNS(null, "loop", raw);
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, -1);
        marquee.GetAttributeNS(null, "loop").Should().Be(raw);
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, 0);
        BrowserLegacyHtmlMembers.SetMarqueeLoop(realm, marquee, -2);
        marquee.GetAttributeNS(null, "loop").Should().Be(raw);
    }

    [Test]
    public void KeygenLabelsPreservesStableEmptyPinnedCapabilityWithoutCopyingIdentity()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var keygen = document.CreateElement("keygen");
        var label = document.CreateElement("label"); label.AppendChild(keygen);
        var labels = BrowserLegacyHtmlMembers.KeygenLabels(keygen);
        labels.Should().BeSameAs(BrowserLegacyHtmlMembers.KeygenLabels(keygen));
        realm.Wrap(labels, DomInterfaces.NodeList).Should().BeSameAs(realm.Wrap(labels, DomInterfaces.NodeList));
        labels.ReadLength(null, default).Should().Be(0);
        labels.ReadItem(0, null, default).Should().BeNull();
        Document.CreateHtml().AdoptNode(keygen);
        BrowserLegacyHtmlMembers.KeygenLabels(keygen).Should().BeSameAs(labels);
        BrowserLegacyHtmlMembers.KeygenLabels((Element) keygen.CloneNode()).Should().NotBeSameAs(labels);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Caught.Exception(() => labels.ReadLength(null, cancellation.Token)).Should().BeOfType<OperationCanceledException>();
    }

    [Test]
    public void CommandUsesFirstDocumentIdHtmlBrandAndCurrentOwnerAfterAdoption()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var command = document.CreateElement("menuitem"); root.AppendChild(command);
        var foreign = document.CreateElementNS(Namespaces.Svg, "g"); foreign.SetAttributeNS(null, "id", "x"); root.AppendChild(foreign);
        var target = document.CreateElement("button"); target.SetAttributeNS(null, "id", "x"); root.AppendChild(target);
        command.SetAttributeNS(null, "command", "x");
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeNull();
        root.RemoveChild(foreign);
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeSameAs(target);
        command.SetAttributeNS(null, "command", " x");
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeNull();
        command.SetAttributeNS(null, "command", "");
        target.SetAttributeNS(null, "id", "");
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeNull();
        target.SetAttributeNS(null, "id", "x"); command.SetAttributeNS(null, "command", "x");
        var other = Document.CreateHtml();
        var otherRoot = other.CreateElement("html"); other.AppendChild(otherRoot);
        otherRoot.AppendChild(other.AdoptNode(command));
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeNull();
        var newTarget = other.CreateElement("span"); newTarget.SetAttributeNS(null, "id", "x"); otherRoot.AppendChild(newTarget);
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeSameAs(newTarget);
    }

    [Test]
    public void CommandTraversalChecksCommentLinksAndFinalResult()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var command = document.CreateElement("menuitem"); root.AppendChild(command);
        command.SetAttributeNS(null, "command", "x");
        for (var i = 0; i < 2048; i++) root.AppendChild(document.CreateComment(""));
        var target = document.CreateElement("button"); target.SetAttributeNS(null, "id", "x"); root.AppendChild(target);
        probe.Remaining = 3;
        Caught.Exception(() => BrowserLegacyHtmlMembers.Command(realm, command)).Should().BeOfType<OperationCanceledException>();
        probe.Remaining = 0;
        BrowserLegacyHtmlMembers.Command(realm, command).Should().BeSameAs(target);
        root.RemoveChild(command);
        command.RemoveAttributeNS(null, "command");
        probe.Remaining = 2;
        Caught.Exception(() => BrowserLegacyHtmlMembers.Command(realm, command)).Should().BeOfType<OperationCanceledException>();
    }

    private sealed class ReadProbe : Constraint
    {
        internal int Remaining;
        public override void Check()
        {
            if (Remaining > 0 && --Remaining == 0) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }
}
