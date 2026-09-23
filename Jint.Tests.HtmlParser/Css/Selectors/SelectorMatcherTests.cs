#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorMatcherTests
{
    private static CompiledSelector Parse(string source, SelectorParseContext? context = null)
        => SelectorCompiler.Compile(source, context, default);

    [Test]
    public void CombinatorsBacktrackAcrossNonmatchingAncestorsAndMixedSiblingNodes()
    {
        var document = Document.CreateHtml();
        var outer = document.CreateElement("main");
        var middle = document.CreateElement("section");
        var inner = document.CreateElement("div");
        var first = document.CreateElement("p");
        var second = document.CreateElement("p");
        var last = document.CreateElement("p");
        first.SetAttribute("class", "first");
        second.SetAttribute("class", "second");
        last.SetAttribute("class", "last");
        document.AppendChild(outer);
        outer.AppendChild(middle);
        middle.AppendChild(inner);
        inner.AppendChild(first);
        inner.AppendChild(document.CreateTextNode("gap"));
        inner.AppendChild(second);
        inner.AppendChild(document.CreateComment("gap"));
        inner.AppendChild(last);

        SelectorMatcher.Matches(Parse("main section div > p.last"), last).Should().BeTrue();
        SelectorMatcher.Matches(Parse("main > p.last"), last).Should().BeFalse();
        SelectorMatcher.Matches(Parse("p.first + p.second"), second).Should().BeTrue();
        SelectorMatcher.Matches(Parse("p.first + p.last"), last).Should().BeFalse();
        SelectorMatcher.Matches(Parse("p.first ~ p.last"), last).Should().BeTrue();
        SelectorMatcher.Matches(Parse("main div p.second + p.last"), last).Should().BeTrue();
    }

    [Test]
    public void QueryRootFiltersSubjectsButDoesNotConstrainSelectorAncestors()
    {
        var document = Document.CreateHtml();
        var outside = document.CreateElement("main");
        var root = document.CreateElement("section");
        var child = document.CreateElement("p");
        var nested = document.CreateElement("p");
        document.AppendChild(outside);
        outside.AppendChild(root);
        root.AppendChild(child);
        child.AppendChild(nested);

        SelectorMatcher.QuerySelectorAll(Parse("main p"), root).Should().Equal(child, nested);
        SelectorMatcher.QuerySelectorAll(Parse("main, section, p"), root).Should().Equal(child, nested);
        SelectorMatcher.QuerySelector(Parse("main > section > p"), root).Should().BeSameAs(child);
        SelectorMatcher.QuerySelectorAll(Parse(":scope > p"), root).Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":scope"), root).Should().BeEmpty();
        SelectorMatcher.Matches(Parse(":scope"), root).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":scope"), child, root).Should().BeFalse();
        SelectorMatcher.Closest(Parse("main:scope, section, p"), nested).Should().BeSameAs(nested);
        SelectorMatcher.Closest(Parse(":scope"), nested).Should().BeSameAs(nested);
        SelectorMatcher.Closest(Parse("main:scope"), nested).Should().BeNull();
    }

    [Test]
    public void FragmentScopeIsVirtualAndTemplateContentDoesNotCrossToHost()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var template = document.CreateElement("template");
        document.AppendChild(root);
        root.AppendChild(template);
        var content = template.TemplateContent!;
        var child = content.OwnerDocument!.CreateElement("p");
        content.AppendChild(child);

        SelectorMatcher.QuerySelectorAll(Parse(":scope > p"), content).Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":scope:scope > p"), content).Should().Equal(child);
        var namespaced = new SelectorParseContext(new[]
        {
            new KeyValuePair<string, string>("", "urn:other")
        });
        SelectorMatcher.QuerySelectorAll(Parse(":scope > *|p", namespaced), content).Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse("template p"), content).Should().BeEmpty();
        SelectorMatcher.QuerySelectorAll(Parse("p"), root).Should().BeEmpty();
        SelectorMatcher.QuerySelectorAll(Parse(":scope"), content).Should().BeEmpty();
        SelectorMatcher.Matches(Parse(":scope > p"), child, content).Should().BeTrue();
        SelectorMatcher.Matches(Parse("template > p"), child, content).Should().BeFalse();
        SelectorMatcher.QuerySelectorAll(Parse("p"), child).Should().BeEmpty();
    }

    [Test]
    public void NamespacesAndHtmlCasingAreReadFromNativeNodeMetadata()
    {
        var context = new SelectorParseContext(new[]
        {
            new KeyValuePair<string, string>("h", Namespaces.Html),
            new KeyValuePair<string, string>("s", Namespaces.Svg),
            new KeyValuePair<string, string>("a", "urn:attribute")
        });
        var html = Document.CreateHtml();
        var root = html.CreateElement("DIV");
        root.SetAttribute("class", "x");
        root.SetAttribute("DATA-KEY", "x");
        root.SetAttributeNS("urn:attribute", "a:key", "y");
        html.AppendChild(root);
        var svg = html.CreateElementNS(Namespaces.Svg, "svg");
        var path = html.CreateElementNS(Namespaces.Svg, "linearGradient");
        root.AppendChild(svg);
        svg.AppendChild(path);

        SelectorMatcher.Matches(Parse("h|DiV[DaTa-KeY=x][a|key=y]", context), root).Should().BeTrue();
        SelectorMatcher.Matches(Parse("s|linearGradient", context), path).Should().BeTrue();
        SelectorMatcher.Matches(Parse("s|lineargradient", context), path).Should().BeFalse();
        SelectorMatcher.Matches(Parse("|div", context), root).Should().BeFalse();
        SelectorMatcher.Matches(Parse("*|div", context), root).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[key=y]", context), root).Should().BeFalse();
        SelectorMatcher.Matches(Parse(":root"), root).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":root"), path).Should().BeFalse();

        var defaultSvg = new SelectorParseContext(new[]
        {
            new KeyValuePair<string, string>("", Namespaces.Svg)
        });
        SelectorMatcher.Matches(Parse("linearGradient", defaultSvg), path).Should().BeTrue();
        SelectorMatcher.Matches(Parse(".x", defaultSvg), root).Should().BeFalse();
        SelectorMatcher.Matches(Parse("*|*.x", defaultSvg), root).Should().BeTrue();

        var unusual = html.CreateElementNS(Namespaces.Html, "DIV");
        var unusualAttribute = html.CreateAttributeNS(null, "DATA-UPPER");
        unusual.SetAttributeNode(unusualAttribute);
        SelectorMatcher.Matches(Parse("DIV"), unusual).Should().BeFalse();
        SelectorMatcher.Matches(Parse("[DATA-UPPER]"), unusual).Should().BeFalse();

        var xml = Document.CreateXml();
        var upper = xml.CreateElementNS(Namespaces.Html, "DIV");
        xml.AppendChild(upper);
        upper.SetAttribute("DATA-KEY", "x");
        SelectorMatcher.Matches(Parse("h|DIV[DATA-KEY=x]", context), upper).Should().BeTrue();
        SelectorMatcher.Matches(Parse("h|div[data-key=x]", context), upper).Should().BeFalse();
    }

    [Test]
    public void AttributeOperatorsAndModifiersUseLiveValues()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttribute("data-list", "one\tTWO three");
        element.SetAttribute("data-lang", "EN-us");
        element.SetAttribute("data-text", "PrefixMiddlesuffix");
        element.SetAttribute("data-empty", "");
        element.SetAttribute("type", "BuTtOn");
        document.AppendChild(element);

        SelectorMatcher.Matches(Parse("[data-list]"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-list~=two i]"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-list~=two s]"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("[data-list~='']"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("[data-empty|='']"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-lang|=en i]"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-text^=pre i][data-text$=FIX i][data-text*=Middle]"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-text*='']"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("[type=button]"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[type=button s]"), element).Should().BeFalse();
        element.SetAttribute("data-overlap", "ababacaba");
        SelectorMatcher.Matches(Parse("[data-overlap*=abac]"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-overlap*=ABAC i]"), element).Should().BeTrue();
        element.SetAttribute("data-text", "changed");
        SelectorMatcher.Matches(Parse("[data-text*=Middle]"), element).Should().BeFalse();
    }

    [Test]
    public void QuirksAffectIdAndClassOnlyAndFollowAdoption()
    {
        var quirks = Document.CreateHtml();
        quirks.SetParserMode(DocumentMode.Quirks);
        var element = quirks.CreateElement("div");
        element.SetAttribute("id", "MiXeD");
        element.SetAttribute("class", "One TWO");
        element.SetAttribute("data-key", "MiXeD");
        quirks.AppendChild(element);
        SelectorMatcher.Matches(Parse("#mixed.one.two"), element).Should().BeTrue();
        SelectorMatcher.Matches(Parse("[data-key=mixed]"), element).Should().BeFalse();

        var limited = Document.CreateHtml();
        limited.SetParserMode(DocumentMode.LimitedQuirks);
        limited.AppendChild(limited.AdoptNode(element));
        SelectorMatcher.Matches(Parse("#mixed.one.two"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("#MiXeD.One.TWO"), element).Should().BeTrue();
    }

    [Test]
    public void IdAndClassSelectorsIgnoreNamespacedAttributesRegardlessOfInsertionOrder()
    {
        var document = Document.CreateHtml();
        var first = document.CreateElement("div");
        first.SetAttributeNS("urn:test", "id", "wrong");
        first.SetAttributeNS("urn:test", "class", "wrong");
        SelectorMatcher.Matches(Parse("#wrong, .wrong"), first).Should().BeFalse();
        // setAttribute looks up qualified names across namespaces by DOM design;
        // attach distinct null-namespace attributes explicitly for this ordering.
        var id = document.CreateAttributeNS(null, "id");
        id.Value = "right";
        first.SetAttributeNode(id);
        var className = document.CreateAttributeNS(null, "class");
        className.Value = "right";
        first.SetAttributeNode(className);
        SelectorMatcher.Matches(Parse("#right.right"), first).Should().BeTrue();
        SelectorMatcher.Matches(Parse("#wrong, .wrong"), first).Should().BeFalse();

        var second = document.CreateElement("div");
        second.SetAttribute("id", "right");
        second.SetAttribute("class", "right");
        second.SetAttributeNS("urn:test", "id", "wrong");
        second.SetAttributeNS("urn:test", "class", "wrong");
        SelectorMatcher.Matches(Parse("#right.right"), second).Should().BeTrue();
        SelectorMatcher.Matches(Parse("#wrong, .wrong"), second).Should().BeFalse();
    }

    [Test]
    public void StructuralPredicatesAndExactNthArithmeticHandleMutations()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var first = document.CreateElement("a");
        var second = document.CreateElement("b");
        var third = document.CreateElement("a");
        document.AppendChild(root);
        root.AppendChild(first);
        root.AppendChild(document.CreateComment("ignored"));
        root.AppendChild(second);
        root.AppendChild(third);
        SelectorMatcher.Matches(Parse("a:first-child:nth-child(1):nth-of-type(1)"), first).Should().BeTrue();
        SelectorMatcher.Matches(Parse("a:last-child:nth-last-child(1):nth-last-of-type(1)"), third).Should().BeTrue();
        SelectorMatcher.Matches(Parse("b:nth-child(2):only-of-type"), second).Should().BeTrue();
        SelectorMatcher.Matches(Parse("a:nth-child(2n+1)"), third).Should().BeTrue();
        SelectorMatcher.Matches(Parse("a:nth-child(999999999999999999999999999n+1)"), third).Should().BeFalse();
        SelectorMatcher.Matches(Parse(":empty"), second).Should().BeTrue();
        var text = document.CreateTextNode(" \n\t");
        second.AppendChild(text);
        SelectorMatcher.Matches(Parse(":empty"), second).Should().BeTrue();
        text.Data = "\f";
        SelectorMatcher.Matches(Parse(":empty"), second).Should().BeFalse();
        text.Data = "\u00a0";
        SelectorMatcher.Matches(Parse(":empty"), second).Should().BeFalse();
        root.RemoveChild(first);
        SelectorMatcher.Matches(Parse("b:first-child:nth-child(1)"), second).Should().BeTrue();
        SelectorMatcher.Matches(Parse("a:only-of-type"), third).Should().BeTrue();
    }

    [Test]
    public void QueryResultIsImmutableStaticAndOrderedByIdentity()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var one = document.CreateElement("x");
        var two = document.CreateElement("x");
        document.AppendChild(root);
        root.AppendChild(one);
        root.AppendChild(two);
        var program = Parse("x, x:first-child, *");
        var snapshot = SelectorMatcher.QuerySelectorAll(program, root);
        snapshot.Should().Equal(one, two);
        snapshot.Should().BeAssignableTo<IReadOnlyList<Element>>();
        var mutable = snapshot as IList<Element>;
        mutable.Should().NotBeNull();
        NUnit.Framework.Assert.Throws<NotSupportedException>(() => mutable!.Add(one));
        root.RemoveChild(one);
        root.AppendChild(one);
        snapshot.Should().Equal(one, two);
        SelectorMatcher.QuerySelectorAll(program, root).Should().Equal(two, one);
        SelectorMatcher.QuerySelector(program, root).Should().BeSameAs(two);
    }

    [Test]
    public void PseudoElementBranchesNeverReturnElementSubjects()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttribute("id", "target");
        document.AppendChild(element);
        SelectorMatcher.Matches(Parse("#target::before"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("::-webkit-unknown"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("::picker(select)"), element).Should().BeFalse();
        SelectorMatcher.Matches(Parse("::slotted(.x)"), element).Should().BeFalse();
        SelectorMatcher.QuerySelectorAll(Parse("#target, ::before"), document).Should().Equal(element);
        SelectorMatcher.TryMatch(Parse("div, #target, ::before"), element, out var specificity)
            .Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 0));
    }

    [Test]
    public void UnsuccessfulBacktrackingChecksCancellationWithoutRecursion()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("a");
        document.AppendChild(root);
        var current = root;
        for (var index = 0; index < 400; index++)
        {
            var child = document.CreateElement("a");
            current.AppendChild(child);
            current = child;
        }
        var subject = document.CreateElement("b");
        current.AppendChild(subject);
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse("x a a b"), subject, null, () =>
            {
                checkpoints++;
                if (checkpoints == 2) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(2);
    }

    [Test]
    public void LongAttributeScanChecksCancellation()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttribute("data-long", new string('x', 4096));
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse("[data-long*=missing]"), element, null, () =>
            {
                checkpoints++;
                // The first two checkpoints bracket KMP setup; the fifth is
                // reached while scanning the long, unsuccessful attribute value.
                if (checkpoints == 5) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(5);
    }

    [Test]
    public void LargeSelectorListPreflightChecksCancellation()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var program = Parse(string.Join(",", Enumerable.Repeat("*", 1024)));
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(program, element, null, () =>
            {
                checkpoints++;
                source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(1);
    }

    [Test]
    public void DocumentScopeScanChecksCancellation()
    {
        var document = Document.CreateHtml();
        for (var index = 0; index < 300; index++) document.AppendChild(document.CreateComment("leading"));
        var element = document.CreateElement("div");
        document.AppendChild(element);
        using var source = new CancellationTokenSource();
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse(":scope"), element, document, source.Cancel, source.Token));
    }

    [Test]
    public void QuerySnapshotCopyChecksCancellationBeforePublication()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        for (var index = 0; index < 300; index++) root.AppendChild(document.CreateElement("item"));
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.QuerySelectorAll(Parse("*"), root, () =>
            {
                checkpoints++;
                // The first checkpoint is traversal; the second is the snapshot copy.
                if (checkpoints == 2) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(2);
    }

    [Test]
    public void ExistencePredicatesStopAtFirstRelevantSibling()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        var first = document.CreateElement("item");
        root.AppendChild(first);
        for (var index = 0; index < 4096; index++) root.AppendChild(document.CreateElement("item"));
        var last = document.CreateElement("item");
        root.AppendChild(last);
        var checkpoints = 0;
        Action checkpoint = () => checkpoints++;
        SelectorMatcher.Matches(Parse(":first-child"), last, null, checkpoint, default).Should().BeFalse();
        SelectorMatcher.Matches(Parse(":last-child"), first, null, checkpoint, default).Should().BeFalse();
        SelectorMatcher.Matches(Parse(":only-child"), first, null, checkpoint, default).Should().BeFalse();
        checkpoints.Should().Be(0);
    }

    [Test]
    public void SubstringMatcherHasLinearWorkOnRepeatedPrefixes()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttribute("data-long", new string('a', 8192));
        var needle = new string('a', 4096) + "b";
        var checkpoints = 0;
        SelectorMatcher.Matches(Parse($"[data-long*='{needle}']"), element, null,
            () => checkpoints++, default).Should().BeFalse();
        checkpoints.Should().BeLessThan(100);
    }

    [Test]
    public void NthArithmeticChecksCancellationBetweenBigIntegerOperations()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var number = new string('9', 4096);
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse($":nth-child({number}n+{number})"), element, null, () =>
            {
                checkpoints++;
                if (checkpoints == 2) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(2);
    }

    [TestCase(":lang(en)")]
    [TestCase(":checked")]
    [TestCase("div || col")]
    [TestCase("div, :hover")]
    public void UnimplementedFamiliesFailPreflightEvenWhenAnotherBranchMatches(string source)
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        document.AppendChild(element);
        var program = Parse(source);
        NUnit.Framework.Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(program, element));
        NUnit.Framework.Assert.Throws<InvalidOperationException>(() => SelectorMatcher.QuerySelector(program, document));
    }
}
