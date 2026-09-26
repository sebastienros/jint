using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeTokenMemberWorkTests
{
    [Test]
    public void LongArgumentValidationCanStopBeforeTheSingleAttributeWrite()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttribute("class", "original");
        var list = DomAttributeTokenList.Of(element, "class");
        var argument = JsString.Create(new string('x', 16384));
        probe.Remaining = 8;
        Caught.Exception(() => DomTokenListMembers.Add(realm, list, [argument]))
            .Should().BeOfType<OperationCanceledException>();
        element.GetAttribute("class").Should().Be("original");
        probe.Remaining = 0;
        DomTokenListMembers.Add(realm, list, [argument]);
        element.GetAttribute("class").Should().Be("original " + argument.AsString());
    }

    [Test]
    public void ColdSnapshotCancellationLeavesRawDuplicateSpellingForARetry()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        var raw = "keep keep " + new string('x', 16384);
        element.SetAttribute("class", raw);
        var list = DomAttributeTokenList.Of(element, "class");
        probe.Remaining = 8;
        Caught.Exception(() => DomTokenListMembers.Replace(realm, list, [JsString.Create("keep"), JsString.Create("new")]))
            .Should().BeOfType<OperationCanceledException>();
        element.GetAttribute("class").Should().Be(raw);
        probe.Remaining = 0;
        DomTokenListMembers.Replace(realm, list, [JsString.Create("keep"), JsString.Create("new")]).Should().Be(JsBoolean.True);
        element.GetAttribute("class").Should().Be("new " + new string('x', 16384));
        DomTokenListMembers.Item(realm, list, [JsNumber.Create(-1)]).Should().Be(JsValue.Null);
        DomTokenListMembers.Value(realm, list).AsString().Should().Be(element.GetAttribute("class"));
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
