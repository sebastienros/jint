#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCssKeyframesWorkTests
{
    [Test]
    public void FindRejectsRemovalDuringItsFinalCheckpoint()
    {
        var root = (CssKeyframesRule) CssStyleSheet.Parse("@keyframes x {from {}}").Rules[0];
        var probe = new CheckpointProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        probe.Checks = 0;
        // Discover the operation's final checkpoint without hard-coding parser cadence.
        NativeCssBindings.FindRule(realm, root, "from").Should().BeSameAs(root.Rules[0]);
        var final = probe.Checks;
        probe.Checks = 0;
        probe.OnCheck = () =>
        {
            if (probe.Checks != final) return;
            probe.OnCheck = null;
            root.DeleteRule("from");
        };
        Assert.Throws<InvalidOperationException>(() => NativeCssBindings.FindRule(realm, root, "from"))!
            .Message.Should().Be(NativeCssQuery.Invalidated);
        probe.Checks.Should().Be(final);
        root.Rules.Should().BeEmpty();
    }

    [Test]
    public void FindRejectsListShrinkageDuringAnUnmatchedScan()
    {
        var root = (CssKeyframesRule) CssStyleSheet.Parse("@keyframes x {" +
            string.Concat(Enumerable.Repeat("from {}", 5000)) + "}").Rules[0];
        var probe = new CheckpointProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        probe.OnCheck = () =>
        {
            if (probe.Checks != 5) return;
            probe.OnCheck = null;
            root.DeleteRule("from");
        };
        Assert.Throws<InvalidOperationException>(() => NativeCssBindings.FindRule(realm, root, "to"))!
            .Message.Should().Be(NativeCssQuery.Invalidated);
        root.Rules.Count.Should().Be(4999);
    }

    [Test]
    public void TextGettersReturnTheImmutableSnapshotWhoseOutputTheyCharged()
    {
        var keys = string.Join(',', Enumerable.Repeat("from", 2000));
        var root = (CssKeyframesRule) CssStyleSheet.Parse("@keyframes x {" + keys + " {}}").Rules[0];
        var child = (CssKeyframeRule) root.Rules[0];
        var keyText = child.KeyText;
        var name = new string('x', 5000);
        root.SetName(name);
        var probe = new CheckpointProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        probe.OnCheck = () => { probe.OnCheck = null; root.SetName(new string('y', 10000)); };
        NativeCssBindings.Name(realm, root).Should().Be(name);
        root.Name.Should().HaveLength(10000);
        probe.OnCheck = () => { probe.OnCheck = null; child.SetKeyText("to"); };
        NativeCssBindings.KeyText(realm, child).Should().Be(keyText);
        child.KeyText.Should().Be("100%");
    }

    private sealed class CheckpointProbe : Constraint
    {
        internal int Checks;
        internal Action? OnCheck;
        public override void Check()
        {
            Checks++;
            OnCheck?.Invoke();
        }
        public override void Reset() { }
    }
}
