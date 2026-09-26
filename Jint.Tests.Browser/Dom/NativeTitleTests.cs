#nullable enable

namespace Jint.Tests.Browser.Dom;

public sealed class NativeTitleTests
{
    [Test]
    public void DocumentTitleCollapsesAsciiWhitespaceButElementTextRetainsIt()
    {
        using var dom = DomTestFixture.Create("<title>  A\t B\n C&nbsp;D </title>");
        dom.Text("document.title").Should().Be("A B C\u00a0D");
        dom.Text("document.querySelector('title').text").Should().Be("  A\t B\n C\u00a0D ");
        dom.Execute("document.title='  changed\\t title  ';");
        dom.Text("document.title").Should().Be("changed title");
        dom.Text("document.querySelector('title').text").Should().Be("  changed\t title  ");
    }

    [Test]
    public void ElementTitleTextReadsOnlyImmediateTextChildren()
    {
        using var dom = DomTestFixture.Create("<title>original</title>");
        dom.Execute("var t=document.querySelector('title'), nested=document.createElement('b'); nested.textContent='hidden'; t.appendChild(nested);");
        dom.Text("t.text").Should().Be("original");
        dom.Text("document.title").Should().Be("original");
        dom.Execute("t.text='replacement';");
        dom.Number("t.childNodes.length").Should().Be(1);
        dom.Text("t.text").Should().Be("replacement");
    }

    [Test]
    public void SettingTitleCreatesOneInTheExistingHeadAndIgnoresAHeadlessDocument()
    {
        using var dom = DomTestFixture.Create("<head></head><body></body>");
        dom.Execute("document.title='first'; var title=document.querySelector('title'); document.title='second';");
        dom.Bool("title===document.querySelector('title')").Should().BeTrue();
        dom.Text("title.text").Should().Be("second");
        dom.Execute("title.remove(); document.head.remove(); document.title='ignored';");
        dom.Text("document.title").Should().BeEmpty();
    }
}
