#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Views;

public sealed class NativeCssNestingWorkTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void DeleteAdaptersForwardEngineWorkAndKeepTheOldTreeOnCancellation(bool media)
    {
        var source = "main {" + string.Concat(Enumerable.Repeat("& {", 5000)) + new string('}', 5001);
        var sheet = CssStyleSheet.Parse(media ? "@media all {" + source + "}" : source);
        var group = media ? (CssMediaRule) sheet.Rules[0] : null;
        var root = (CssStyleRule) (group is null ? sheet.Rules[0] : group.Rules[0]);
        var child = root.Rules[0];
        var stamp = sheet.Stamp;
        var probe = new DeleteProbe();
        using var cancellation = new CancellationTokenSource();
        using var engine = new Engine(options => options.AddConstraint(probe).ObserveCancellation(cancellation.Token));
        var realm = DomRealm.Of(engine);
        probe.Checks = 0;
        probe.OnCheck = () =>
        {
            if (probe.Checks == 2) cancellation.Cancel();
        };
        Action delete = () =>
        {
            if (group is null) NativeCssBindings.DeleteRule(realm, sheet, 0);
            else NativeCssBindings.DeleteRule(realm, group, 0);
        };
        Assert.Throws<OperationCanceledException>(() => delete());
        probe.Checks.Should().Be(2);
        (group is null ? sheet.Rules[0] : group.Rules[0]).Should().BeSameAs(root);
        root.ParentStyleSheet.Should().BeSameAs(sheet);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
    }

    private sealed class DeleteProbe : Constraint
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
