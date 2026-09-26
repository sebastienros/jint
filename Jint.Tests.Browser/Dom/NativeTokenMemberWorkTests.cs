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

    [Test]
    public void TokenWritesKeepForeignQualifiedNameMatchesSeparateFromTheNullNamespaceAttribute()
    {
        using var engine = new Engine();
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttributeNS("urn:foreign", "class", "foreign");
        var list = DomAttributeTokenList.Of(element, "class");
        DomTokenListMembers.SetValue(realm, list, [JsString.Create("local")]);
        element.GetAttributeNS(null, "class").Should().Be("local");
        element.GetAttributeNS("urn:foreign", "class").Should().Be("foreign");
        DomTokenListMembers.PutForwards(realm, element, "class", [JsString.Create("forwarded")]);
        element.GetAttributeNS(null, "class").Should().Be("forwarded");
        element.GetAttributeNS("urn:foreign", "class").Should().Be("foreign");
        DomTokenListMembers.Add(realm, list, [JsString.Create("added")]);
        element.GetAttributeNS(null, "class").Should().Be("forwarded added");
        element.GetAttributeNS("urn:foreign", "class").Should().Be("foreign");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ManyArgumentsUseLinearChargedIndexWork(bool remove)
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var small = Measure(128);
        var large = Measure(512);
        large.Should().BeLessThanOrEqualTo(small * 6);

        int Measure(int count)
        {
            var element = Document.CreateHtml().CreateElement("div");
            var existing = Enumerable.Range(0, count).Select(i => "existing" + i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            element.SetAttribute("class", string.Join(" ", existing));
            var arguments = (remove ? existing : Enumerable.Range(0, count).Select(i => "added" + i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture)))
                .Select(token => (JsValue) JsString.Create(token)).ToArray();
            var list = DomAttributeTokenList.Of(element, "class");
            probe.Count = 0;
            if (remove) DomTokenListMembers.Remove(realm, list, arguments);
            else DomTokenListMembers.Add(realm, list, arguments);
            var checks = probe.Count;
            if (remove) element.GetAttribute("class").Should().Be("");
            else list.ReadLength(realm.NativeReadCheckpoint, realm.CancellationToken).Should().Be(count * 2);
            return checks;
        }
    }

    [TestCase("add", "old", "host", "host new")]
    [TestCase("add", null, "host", "host new")]
    [TestCase("remove", "old new", "host old", "host")]
    [TestCase("toggle", "old", "host new", "host")]
    [TestCase("replace", "old tail", "host old", "host new")]
    [TestCase("force", "old", "host", "host old")]
    public void ALastCheckpointMutationRetriesBeforePublishing(string operation, string? initial, string changed, string expected)
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var baseline = NewElement();
        probe.Count = 0;
        Invoke(baseline);
        var lastCheck = probe.Count;
        var target = NewElement();
        probe.Count = 0;
        probe.OnCheck = () =>
        {
            if (probe.Count == lastCheck) target.SetAttributeNS(null, "class", changed);
        };
        Invoke(target);
        target.GetAttributeNS(null, "class").Should().Be(expected);
        probe.Count.Should().BeGreaterThan(lastCheck);

        Element NewElement()
        {
            var element = Document.CreateHtml().CreateElement("div");
            if (initial is not null) element.SetAttributeNS(null, "class", initial);
            return element;
        }
        void Invoke(Element element)
        {
            var list = DomAttributeTokenList.Of(element, "class");
            switch (operation)
            {
                case "add": DomTokenListMembers.Add(realm, list, [JsString.Create("new")]); break;
                case "remove": DomTokenListMembers.Remove(realm, list, [JsString.Create("old")]); break;
                case "toggle": DomTokenListMembers.Toggle(realm, list, [JsString.Create("new")]); break;
                case "replace": DomTokenListMembers.Replace(realm, list, [JsString.Create("old"), JsString.Create("new")]); break;
                case "force": DomTokenListMembers.Toggle(realm, list, [JsString.Create("old"), JsBoolean.True]); break;
            }
        }
    }

    private sealed class ReadProbe : Constraint
    {
        internal int Remaining;
        internal int Count;
        internal Action? OnCheck;
        public override void Check()
        {
            Count++;
            OnCheck?.Invoke();
            if (Remaining > 0 && --Remaining == 0) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }
}
