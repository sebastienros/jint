#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

// Authored fixtures for the finite C3b contract; no corpus expectations are changed.
[TestFixture]
public sealed class SelectorInteractionStateTests
{
    private static CompiledSelector Parse(string source) => SelectorCompiler.Compile(source, null, default);
    private static bool Match(string source, Element element, in SelectorEnvironment environment)
    {
        var work = new SelectorMatchWork(element, default);
        return SelectorMatcher.Matches(Parse(source), element, null, environment, ref work);
    }
    private static Element Add(Node parent, string name, string? id = null)
    {
        var document = parent as Document ?? parent.OwnerDocument!;
        var element = document.CreateElement(name);
        if (id is not null) element.SetAttribute("id", id);
        parent.AppendChild(element);
        return element;
    }

    [Test]
    public void AllEntryPointsAndMatchingBranchSpecificityUseFreshEnvironment()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "main");
        var first = Add(root, "input", "first");
        var second = Add(root, "input", "second");
        var program = Parse("input, #first:focus");
        var environment = new SelectorEnvironment(document, first, first, second);
        var work = new SelectorMatchWork(first, default);
        SelectorMatcher.TryMatch(program, first, out var specificity, null, environment, ref work).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 1, 0));
        work = new SelectorMatchWork(first, default);
        SelectorMatcher.Closest(Parse(":focus-within"), first, environment, ref work).Should().BeSameAs(first);
        work = new SelectorMatchWork(root, default);
        SelectorMatcher.QuerySelector(Parse(":target"), root, environment, ref work).Should().BeSameAs(second);
        work = new SelectorMatchWork(root, default);
        var snapshot = SelectorMatcher.QuerySelectorAll(Parse(":focus"), root, environment, ref work);
        snapshot.Should().Equal(first);
        environment = environment with { FocusedElement = second, PointerPressTarget = null, TargetElement = first };
        work = new SelectorMatchWork(first, default);
        SelectorMatcher.TryMatch(program, first, out specificity, null, environment, ref work).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(0, 0, 1));
        Match(":focus", second, environment).Should().BeTrue();
        Match(":active", first, environment).Should().BeFalse();
        Match(":target", first, environment).Should().BeTrue();
        snapshot.Should().Equal(first);
        SelectorMatcher.Matches(Parse(":focus, :active, :target"), first).Should().BeFalse();
    }

    [TestCase(":is(:focus)")]
    [TestCase(":where(:focus)")]
    [TestCase(":not(:active)")]
    [TestCase("input:nth-child(1 of :focus)")]
    [TestCase("input:nth-last-child(1 of :focus)")]
    public void NestedPredicatesShareTheEnvironment(string selector)
    {
        var document = Document.CreateHtml();
        var root = Add(document, "main");
        var input = Add(root, "input");
        var environment = new SelectorEnvironment(document, input, null, null);
        Match(selector, input, environment).Should().BeTrue();
        Match("main:has(> :focus)", root, environment).Should().BeTrue();
    }

    [Test]
    public void ColumnsEvaluateNestedEnvironmentPredicates()
    {
        var document = Document.CreateHtml();
        var table = Add(document, "table");
        var group = Add(table, "colgroup");
        var col = Add(group, "col");
        var body = Add(table, "tbody");
        var cell = Add(Add(body, "tr"), "td");
        var input = Add(cell, "input");
        var environment = new SelectorEnvironment(document, input, input, null);
        Match("col || td:has(> :focus)", cell, environment).Should().BeTrue();
        Match("col:has(|| td > :active)", col, environment).Should().BeTrue();
    }

    [TestCase(":hover")]
    [TestCase(":focus-visible")]
    [TestCase(":autofill")]
    [TestCase(":-webkit-autofill")]
    public void ExplicitHeadlessPoliciesMatchNothing(string selector)
    {
        var document = Document.CreateHtml();
        var input = Add(document, "input");
        Match(selector, input, new SelectorEnvironment(document, input, input, input)).Should().BeFalse();
        SelectorMatcher.Matches(Parse(selector), input).Should().BeFalse();
        SelectorMatcher.Matches(Parse("input, " + selector), input).Should().BeTrue();
    }

    [Test]
    public void FocusHostsAndFlatAncestorsUseClosedRootsAndFreshNamedAssignment()
    {
        var document = Document.CreateHtml();
        var host = Add(document, "section");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed), default);
        var container = Add(shadow, "div");
        var slot = Add(container, "slot");
        slot.SetAttribute("name", "x");
        var light = Add(host, "input");
        light.SetAttribute("slot", "x");
        var environment = new SelectorEnvironment(document, light, light, null);
        Match(":focus-within", slot, environment).Should().BeTrue();
        Match(":active", container, environment).Should().BeTrue();
        Match(":focus", host, environment).Should().BeFalse();
        Match("section > input:focus", light, environment).Should().BeTrue();
        slot.SetAttribute("name", "y");
        light.StoredAssignedSlot.Should().BeSameAs(slot);
        Match(":focus-within", host, environment).Should().BeFalse();
        Match(":active", host, environment).Should().BeFalse();
        var inner = Add(container, "article");
        var innerRoot = ShadowTree.Attach(inner, new ShadowRootInit(ShadowRootMode.Open), default);
        var focused = Add(innerRoot, "input");
        environment = environment with { FocusedElement = focused };
        Match(":focus", host, environment).Should().BeTrue();
        Match(":focus", inner, environment).Should().BeTrue();
        Match(":focus", container, environment).Should().BeFalse();
    }

    [Test]
    public void ManualReassignmentSuppressedFallbackAndDetachedBoundaries()
    {
        var document = Document.CreateHtml();
        var host = Add(document, "section");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var first = Add(shadow, "slot");
        var fallback = Add(first, "input");
        var second = Add(shadow, "slot");
        var light = Add(host, "input");
        SlotAssignment.Assign(first, new Node[] { light });
        var environment = new SelectorEnvironment(document, fallback, light, null);
        Match(":focus", host, environment).Should().BeTrue();
        Match(":focus-within", first, environment).Should().BeFalse();
        Match(":active", first, environment).Should().BeTrue();
        SlotAssignment.Assign(second, new Node[] { light });
        Match(":active", first, environment).Should().BeFalse();
        Match(":active", second, environment).Should().BeTrue();
        host.RemoveChild(light);
        light.StoredAssignedSlot.Should().BeSameAs(second);
        Match(":active", second, environment).Should().BeFalse();
        Match(":focus-within", first, environment).Should().BeTrue();
        document.RemoveChild(host);
        Match(":focus", host, environment).Should().BeFalse();
    }

    [Test]
    public void TargetRequiresTheOrdinaryDocumentRootAndFramesAreNotFocus()
    {
        var document = Document.CreateHtml();
        var host = Add(document, "section");
        var frame = Add(host, "iframe");
        Match(":focus", frame, new SelectorEnvironment(document, frame, null, null)).Should().BeFalse();
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var target = Add(shadow, "div");
        Match(":target", target, new SelectorEnvironment(document, null, null, target)).Should().BeFalse();
        shadow.RemoveChild(target);
        Match(":target", target, new SelectorEnvironment(document, null, null, target)).Should().BeFalse();
        host.AppendChild(target);
        Match(":target", target, new SelectorEnvironment(document, null, null, target)).Should().BeTrue();
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(target);
        Match(":target", target, new SelectorEnvironment(document, null, null, target)).Should().BeFalse();
    }

    [Test]
    public void ActiveLabelsResolveExplicitImplicitNestedAndExternalControls()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "main", "root");
        var separate = Add(root, "aside");
        var control = Add(separate, "input", "control");
        var outer = Add(root, "label");
        outer.SetAttribute("for", "control");
        var inner = Add(outer, "label");
        var button = Add(inner, "button");
        var pressed = Add(button, "span");
        var environment = new SelectorEnvironment(document, null, pressed, null);
        Match(":active", control, environment).Should().BeTrue();
        Match(":active", separate, environment).Should().BeFalse();
        Match(":active", button, environment).Should().BeTrue();
        outer.SetAttribute("for", "");
        Match(":active", control, environment).Should().BeFalse();
        outer.RemoveAttribute("for");
        outer.SetAttributeNS("urn:foreign", "f:for", "control");
        Match(":active", control, environment).Should().BeFalse();
        Match(":active", button, environment).Should().BeTrue();
        var hidden = Add(root, "input", "hidden");
        hidden.SetAttribute("type", "HIDDEN");
        outer.SetAttribute("for", "hidden");
        Match(":active", hidden, environment).Should().BeFalse();
        hidden.SetAttribute("type", "text");
        Match(":active", hidden, environment).Should().BeTrue();
        var duplicate = Add(root, "div", "control");
        root.InsertBefore(duplicate, separate);
        outer.SetAttribute("for", "control");
        Match(":active", control, environment).Should().BeFalse();
    }

    [TestCase("#hit, :checked")]
    [TestCase(":is(#hit, :valid)")]
    [TestCase(":not(:lang(en))")]
    [TestCase(":has(:dir(rtl))")]
    public void UnsupportedPredicatesRejectTheWholeProgramAndEmptyQueries(string selector)
    {
        var document = Document.CreateHtml();
        var hit = Add(document, "input", "hit");
        var environment = new SelectorEnvironment(document, hit, hit, hit);
        Assert.Throws<InvalidOperationException>(() => Match(selector, hit, environment));
        var empty = document.CreateDocumentFragment();
        var work = new SelectorMatchWork(empty, default);
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.QuerySelectorAll(Parse(selector), empty,
            environment, ref work));
    }

    [Test]
    public void IdentityValidationIsConstantAndUninitializedWorkIsRejected()
    {
        var document = Document.CreateHtml();
        var element = Add(document, "input");
        var foreign = Document.CreateHtml().CreateElement("input");
        Assert.Throws<ArgumentException>(() => Match("input", element, new SelectorEnvironment(null, element, null, null)));
        Assert.Throws<ArgumentException>(() => Match("input", element, new SelectorEnvironment(document, foreign, null, null)));
        var work = default(SelectorMatchWork);
        Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(Parse("input"), element, null,
            default, ref work));
    }
}
