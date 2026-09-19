namespace Jint.Tests.Browser.Dom;

public sealed class TemplateCloningTests
{
    [TestCase("template.cloneNode()", false)]
    [TestCase("template.cloneNode(false)", false)]
    [TestCase("target.importNode(template)", false)]
    [TestCase("target.importNode(template, false)", false)]
    [TestCase("template.cloneNode(true)", true)]
    [TestCase("target.importNode(template, true)", true)]
    public void OnlyDeepCopiesContainTemplateContents(string operation, bool deep)
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var template = document.createElement('template');
            template.innerHTML = '<div><span>outer</span><template><b>inner</b></template></div>';
            template.setAttribute('data-source', 'retained');
            var original = template.content.firstChild;
            var target = document.implementation.createHTMLDocument('target');
            """);
        fixture.Execute("var copy = " + operation + ";");
        fixture.Number("copy.content.childNodes.length").Should().Be(deep ? 1 : 0);
        fixture.Text("copy.getAttribute('data-source')").Should().Be("retained");
        fixture.Bool("copy !== template && copy.content !== template.content").Should().BeTrue();
        fixture.Bool(operation.StartsWith("target", StringComparison.Ordinal)
            ? "copy.ownerDocument === target" : "copy.ownerDocument === document").Should().BeTrue();
        fixture.Bool("template.content.firstChild === original").Should().BeTrue();
        fixture.Text("template.content.querySelector('template').content.textContent").Should().Be("inner");
        if (deep)
        {
            fixture.Text("copy.content.querySelector('template').content.textContent").Should().Be("inner");
            fixture.Bool("copy.content.firstChild !== original").Should().BeTrue();
        }
    }

    [TestCase("cloneContents", false)]
    [TestCase("extractContents", false)]
    [TestCase("cloneContents", true)]
    [TestCase("extractContents", true)]
    public void RangeCopiesOnlyClearPartiallyContainedTemplates(string operation, bool fromInside)
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var root = document.querySelector('main');
            var before = root.appendChild(document.createElement('template'));
            var partial = root.appendChild(document.createElement('template'));
            var after = root.appendChild(document.createElement('template'));
            for (var t of [before, partial, after])
              t.innerHTML = '<div><template><span>nested</span></template></div>';
            var original = partial.content.firstChild;
            var range = document.createRange();
            """);
        fixture.Execute(fromInside
            ? "range.setStart(partial, 0); range.setEnd(root, 3);"
            : "range.setStart(root, 0); range.setEnd(partial, 0);");
        fixture.Execute("var result = range." + operation + "(); var shallow = result." + (fromInside ? "firstChild" : "lastChild") + "; var deep = result." + (fromInside ? "lastChild" : "firstChild") + ";");
        fixture.Number("shallow.content.childNodes.length").Should().Be(0);
        fixture.Text("deep.content.querySelector('template').content.textContent").Should().Be("nested");
        fixture.Bool("partial.content.firstChild === original").Should().BeTrue();
        fixture.Bool("shallow !== partial").Should().BeTrue();
        if (operation == "extractContents")
        {
            fixture.Bool(fromInside ? "deep === after" : "deep === before").Should().BeTrue();
        }
    }
}
