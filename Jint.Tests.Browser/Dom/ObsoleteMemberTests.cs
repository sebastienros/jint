namespace Jint.Tests.Browser.Dom;

/// <summary>
/// The three shapes <a href="https://html.spec.whatwg.org/multipage/obsolete.html">HTML §16</a> has, and
/// which of them each of these members is: a name the standard removed, a member it keeps and defines to
/// answer nothing, and an interface AngleSharp split that HTML never had.
/// </summary>
/// <remarks>
/// <c>html/dom/historical.html</c> is the corpus file that tells them apart, and telling them apart is the
/// whole difficulty: "remove it" is the wrong answer for two of the three.
/// </remarks>
public sealed class ObsoleteMemberTests
{
    private const string Page = """
        <!doctype html>
        <html><body>
          <applet name="war" align="left"></applet>
          <table><tr><th id="h" scope="ROW">h</th><td id="d">d</td></tr></table>
        </body></html>
        """;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/obsolete.html#htmlappletelement — HTML removed the interface,
    /// so the name is gone from the global and the element takes the interface every unlisted HTML name
    /// takes. AngleSharp still builds an <c>HtmlAppletElement</c>, which is why the local name has to decide.
    /// </summary>
    [Test]
    public void AnAppletIsAnHtmlUnknownElement()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            (() => {
              const el = document.getElementsByTagName('applet')[0];
              return [
                el instanceof HTMLUnknownElement,
                el instanceof HTMLElement,
                Object.prototype.toString.call(el),
                typeof HTMLAppletElement
              ].map(String).join(',');
            })()
            """)
            .Should().Be("true,true,[object HTMLUnknownElement],undefined");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/obsolete.html#dom-document-applets — the member is <b>not</b>
    /// removed: it must answer an <c>HTMLCollection</c> whose filter matches nothing, and it is
    /// <c>[SameObject]</c>.
    /// </summary>
    [Test]
    public void DocumentAppletsIsAnEmptySameObjectCollection()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            [
              document.applets.length,
              document.applets instanceof HTMLCollection,
              document.applets === document.applets,
              document.applets.item(0),
              document.applets.namedItem('war'),
              Array.from(document.applets).length
            ].map(String).join(',')
            """)
            .Should().Be("0,true,true,null,null,0");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/tables.html#htmltablecellelement — HTML has one interface for
    /// both cells, and `html/dom/historical.html` asserts that the two names the DOM once had are gone.
    /// AngleSharp declares both, which put two names on the global that no standard declares.
    /// </summary>
    [Test]
    public void BothCellsAreOneInterfaceAndTheTwoRemovedNamesAreGone()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            [
              typeof HTMLTableDataCellElement,
              typeof HTMLTableHeaderCellElement,
              Object.getPrototypeOf(document.getElementById('d')) === HTMLTableCellElement.prototype,
              Object.getPrototypeOf(document.getElementById('h')) === HTMLTableCellElement.prototype,
              Object.prototype.toString.call(document.getElementById('h'))
            ].map(String).join(',')
            """)
            .Should().Be("undefined,undefined,true,true,[object HTMLTableCellElement]");
    }

    /// <summary>
    /// The one member the removed interface carried, put back where HTML has it and taking HTML's reflection
    /// rules with it: <c>scope</c> is limited to only known values, so an unknown value reads back as the
    /// empty string and a known one in its canonical case.
    /// </summary>
    [Test]
    public void ScopeIsHtmlsReflectedMemberOnEveryCell()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            (() => {
              const th = document.getElementById('h');
              const td = document.getElementById('d');
              const before = th.scope;
              th.scope = 'colgroup';
              const written = [th.scope, th.getAttribute('scope')].join('/');
              th.scope = '5%';
              return [before, written, th.scope, td.scope, 'scope' in td].map(String).join(',');
            })()
            """)
            .Should().Be("row,colgroup/colgroup,,,true");
    }
}
