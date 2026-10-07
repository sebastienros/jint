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

    [TestCase("add")]
    [TestCase("remove")]
    [TestCase("toggle")]
    [TestCase("replace")]
    public void EveryWarmMutationCheckpointCanCancelWithoutPublishingAPartialSerialization(string operation)
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        // Cross several copy boundaries and preserve non-ASCII UTF-16, including a surrogate pair.
        var kept = new string('x', 1023) + "\U0001F642\u00a0tail";
        var raw = "old " + kept + " old";
        var list = DomAttributeTokenList.Of(element, "class");
        Reset();
        Invoke();
        var checks = probe.Count;
        var expected = operation switch
        {
            "add" => "old " + kept + " new",
            "remove" or "toggle" => kept,
            _ => "new " + kept
        };
        element.GetAttributeNS(null, "class").Should().Be(expected);
        checks.Should().BeGreaterThan(4);
        // Stop at every checkpoint, including those while constructing the final string and the
        // final source-proof check. None may leak a write; a successful retry must still normalize.
        for (var stop = 1; stop <= checks; stop++)
        {
            Reset();
            probe.Remaining = stop;
            Caught.Exception(Invoke).Should().BeOfType<OperationCanceledException>();
            element.GetAttributeNS(null, "class").Should().Be(raw);
            Reset();
            Invoke();
            element.GetAttributeNS(null, "class").Should().Be(expected);
        }

        void Reset()
        {
            probe.Remaining = 0;
            element.SetAttributeNS(null, "class", raw);
            list.ReadLength(null, default); // Isolate mutation work from a cold token-index build.
            probe.Count = 0;
        }
        void Invoke()
        {
            switch (operation)
            {
                case "add": DomTokenListMembers.Add(realm, list, [JsString.Create("new")]); break;
                case "remove": DomTokenListMembers.Remove(realm, list, [JsString.Create("old")]); break;
                case "toggle": DomTokenListMembers.Toggle(realm, list, [JsString.Create("old")]); break;
                case "replace": DomTokenListMembers.Replace(realm, list, [JsString.Create("old"), JsString.Create("new")]); break;
            }
        }
    }

    [Test]
    public void ACheckpointInsideLongSerializationRetriesAgainstTheCurrentAttribute()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        var list = DomAttributeTokenList.Of(element, "class");
        var raw = "old " + new string('x', 8192);
        element.SetAttributeNS(null, "class", raw);
        list.ReadLength(null, default);
        DomTokenListMembers.Add(realm, list, [JsString.Create("new")]);
        // The last two checks are Serialize's final check and TryWrite's source check.
        // The preceding check is in the bounded character copy, with unpublished output.
        var duringCopy = probe.Count - 2;
        element.SetAttributeNS(null, "class", raw);
        list.ReadLength(null, default);
        probe.Count = 0;
        probe.OnCheck = () =>
        {
            if (probe.Count == duringCopy) element.SetAttributeNS(null, "class", "host");
        };
        DomTokenListMembers.Add(realm, list, [JsString.Create("new")]);
        element.GetAttributeNS(null, "class").Should().Be("host new");
        probe.Count.Should().BeGreaterThan(duringCopy + 2);
    }

    [Test]
    public void WarmSingleTokenToggleChargesSerializationCharactersBeforeWriting()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        var raw = new string('x', 8192);
        element.SetAttributeNS(null, "class", raw);
        var list = DomAttributeTokenList.Of(element, "class");
        list.ReadLength(null, default);
        // The cached single slice materializes by returning the source string, and the absent
        // short toggle token fails by length. Only serialization needs to scan these characters.
        probe.Remaining = 8;
        Caught.Exception(() => DomTokenListMembers.Toggle(realm, list, [JsString.Create("new")]))
            .Should().BeOfType<OperationCanceledException>();
        element.GetAttributeNS(null, "class").Should().Be(raw);
        probe.Remaining = 0;
        probe.Count = 0;
        DomTokenListMembers.Toggle(realm, list, [JsString.Create("new")]).Should().Be(JsBoolean.True);
        probe.Count.Should().BeGreaterThanOrEqualTo(raw.Length / 256);
        element.GetAttributeNS(null, "class").Should().Be(raw + " new");
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
