#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

public sealed class SelectorHostTests
{
    [TestCase(":host", true)]
    [TestCase(":host(.x)", true)]
    [TestCase(":host(.missing)", false)]
    [TestCase(":host(section.x)", true)]
    [TestCase(":host(:is(.x, #absent))", true)]
    [TestCase(":host(:not(.missing))", true)]
    [TestCase(":host(:has(> .light))", true)]
    [TestCase(":host:has(> span)", true)]
    [TestCase(":is(:host)", true)]
    [TestCase(":where(:host(.x))", true)]
    [TestCase(":not(:not(:host))", true)]
    [TestCase(":host.x", false)]
    [TestCase("*:host", false)]
    [TestCase("section:host", false)]
    [TestCase(".x", false)]
    [TestCase(":scope", false)]
    [TestCase(":not(.missing)", false)]
    [TestCase(":is(:host, .x).x", false)]
    [TestCase("body :host", false)]
    [TestCase(":host + .light", false)]
    public void StylesheetHostIsFeaturelessExceptInsideItsArgument(string selector, bool expected)
    {
        var (host, shadow, _) = Tree();
        Match(selector, host, shadow).Should().Be(expected);
    }

    [TestCase(":host > span", true)]
    [TestCase(":host(.x) span", true)]
    [TestCase(":is(:host) > span", true)]
    [TestCase(":not(:not(:host)) > span", true)]
    [TestCase(":host(.missing) > span", false)]
    [TestCase(".x > span", false)]
    [TestCase("* > span", false)]
    [TestCase(":scope > span", false)]
    [TestCase(":host.x > span", false)]
    [TestCase("body span", false)]
    public void HostCombinatorsStayInsideTheStylesheetTree(string selector, bool expected)
    {
        var (_, shadow, child) = Tree();
        Match(selector, child, shadow).Should().Be(expected);
    }

    [Test]
    public void DomQueriesDoNotAcquireStylesheetHostContext()
    {
        var (host, shadow, _) = Tree();
        var selector = Parse(":host");
        SelectorMatcher.Matches(selector, host).Should().BeFalse();
        SelectorMatcher.QuerySelectorAll(selector, shadow).Should().BeEmpty();
        SelectorMatcher.QuerySelectorAll(Parse(":host > span"), shadow).Should().BeEmpty();
        SelectorMatcher.QuerySelectorAll(Parse(":scope > span"), shadow).Should().HaveCount(1);
        SelectorMatcher.QuerySelectorAll(Parse(":root, :host"), host.OwnerDocument!).Should().HaveCount(1);
        SelectorMatcher.Supports(Parse(":host(.x)"), new(default)).Should().BeTrue();
    }

    [Test]
    public void HostArgumentSpecificityIsStaticAndWorkDoesNotRetainTheShadowContext()
    {
        var (host, shadow, child) = Tree();
        var program = Parse(":host(:is(.x, #absent))");
        var work = new SelectorMatchWork(host, default);
        SelectorMatcher.TryMatch(program, host, out var specificity, null, default, ref work, shadow).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 1, 0));
        SelectorMatcher.TryMatch(program, host, out _, null, default, ref work).Should().BeFalse();
        SelectorMatcher.TryMatch(program, child, out _, null, default, ref work, shadow).Should().BeFalse();
    }

    [Test]
    public void ShadowReadsKeepCancellationAndMutationGuards()
    {
        var (host, shadow, _) = Tree();
        var program = Parse(":host(.x)");
        using var cancellation = new CancellationTokenSource();
        var cancelled = new SelectorMatchWork(host, cancellation.Token);
        cancellation.Cancel();
        Action read = () => SelectorMatcher.TryMatch(program, host, out _, null, default, ref cancelled, shadow);
        read.Should().Throw<OperationCanceledException>();
        program = Parse(":host(" + string.Concat(Enumerable.Repeat(":is(", 128)) + ".x" + new string(')', 128) + ")");
        var mutated = new SelectorMatchWork(host, default, () => host.SetAttribute("class", "changed"));
        read = () => SelectorMatcher.TryMatch(program, host, out _, null, default, ref mutated, shadow);
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        Match(":host(.changed)", host, shadow).Should().BeTrue();
    }

    private static bool Match(string selector, Element element, ShadowRoot shadow)
    {
        var work = new SelectorMatchWork(element, default);
        return SelectorMatcher.TryMatch(Parse(selector), element, out _, null, default, ref work, shadow);
    }

    private static CompiledSelector Parse(string selector) => SelectorCompiler.Compile(selector, null, default);

    private static (Element Host, ShadowRoot Shadow, Element Child) Tree()
    {
        var document = MarkupParser.ParseHtml("<section class=x><b class=light></b></section>");
        var host = SelectorMatcher.QuerySelector(Parse("section"), document)!;
        var shadow = host.AttachShadow(new ShadowRootInit(ShadowRootMode.Open));
        var child = document.CreateElement("span");
        shadow.AppendChild(child);
        return (host, shadow, child);
    }
}
