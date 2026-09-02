using AngleSharp.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// HTML-AAM's element-to-role mapping, one case per row of the table this package claims to implement.
/// </summary>
/// <remarks>
/// Every case names the markup rather than the element, because more than a third of the table's rows are
/// conditional — <c>a</c> on <c>href</c>, <c>input</c> on <c>type</c> and <c>list</c>, <c>li</c> on its
/// parent, <c>header</c> and <c>aside</c> on their scope — and a test that only fed a bare element would
/// pass while every condition was wrong.
/// </remarks>
public sealed class ImplicitRoleTests
{
    private static IEnumerable<TestCaseData> Rows()
    {
        // Anchors, areas and links.
        yield return Case("<a id=t href='/x'>x</a>", "link");
        yield return Case("<a id=t>x</a>", "generic");
        yield return Case("<map><area id=t href='/x'></map>", "link");
        yield return Case("<map><area id=t></map>", "generic");

        // Document structure.
        yield return Case("<article id=t></article>", "article");
        yield return Case("<blockquote id=t></blockquote>", "blockquote");
        yield return Case("<address id=t></address>", "group");
        yield return Case("<hgroup id=t></hgroup>", "group");
        yield return Case("<main id=t></main>", "main");
        yield return Case("<nav id=t></nav>", "navigation");
        yield return Case("<search id=t></search>", "search");
        yield return Case("<hr id=t>", "separator");
        yield return Case("<p id=t>x</p>", "paragraph");
        yield return Case("<div id=t></div>", "generic");
        yield return Case("<span id=t></span>", "generic");
        yield return Case("<label id=t>x</label>", "generic");

        // Landmarks whose role depends on where they are.
        yield return Case("<header id=t></header>", "banner");
        yield return Case("<article><header id=t></header></article>", "sectionheader");
        yield return Case("<footer id=t></footer>", "contentinfo");
        yield return Case("<section><footer id=t></footer></section>", "sectionfooter");
        yield return Case("<aside id=t></aside>", "complementary");
        yield return Case("<main><aside id=t></aside></main>", "complementary");
        yield return Case("<article><aside id=t></aside></article>", "generic");
        yield return Case("<article><aside id=t aria-label=Notes></aside></article>", "complementary");
        yield return Case("<section id=t></section>", "generic");
        yield return Case("<section id=t aria-label=Intro></section>", "region");

        // Headings.
        yield return Case("<h1 id=t>x</h1>", "heading");
        yield return Case("<h6 id=t>x</h6>", "heading");

        // Lists.
        yield return Case("<ul id=t></ul>", "list");
        yield return Case("<ol id=t></ol>", "list");
        yield return Case("<menu id=t></menu>", "list");
        yield return Case("<dl id=t></dl>", "list");
        yield return Case("<ul><li id=t>x</li></ul>", "listitem");
        yield return Case("<div><li id=t>x</li></div>", "generic");
        yield return Case("<dl><dt id=t>x</dt></dl>", "term");
        yield return Case("<dl><dd id=t>x</dd></dl>", "definition");

        // Tables.
        yield return Case("<table id=t></table>", "table");
        yield return Case("<table><caption id=t>x</caption></table>", "caption");
        yield return Case("<table><tbody id=t><tr><td>x</td></tr></tbody></table>", "rowgroup");
        yield return Case("<table><tr id=t><td>x</td></tr></table>", "row");
        yield return Case("<table><tr><td id=t>x</td></tr></table>", "cell");
        yield return Case("<table><tr><th id=t>x</th></tr></table>", "columnheader");
        yield return Case("<table><tr><th id=t scope=row>x</th></tr></table>", "rowheader");

        // Form controls.
        yield return Case("<button id=t>x</button>", "button");
        yield return Case("<fieldset id=t></fieldset>", "group");
        yield return Case("<fieldset><legend id=t>x</legend></fieldset>", "generic");
        yield return Case("<form id=t></form>", "form");
        yield return Case("<textarea id=t></textarea>", "textbox");
        yield return Case("<select id=t></select>", "combobox");
        yield return Case("<select id=t multiple></select>", "listbox");
        yield return Case("<select id=t size=4></select>", "listbox");
        yield return Case("<select><optgroup id=t label=G></optgroup></select>", "group");
        yield return Case("<select><option id=t>x</option></select>", "option");
        yield return Case("<datalist id=t></datalist>", "listbox");
        yield return Case("<output id=t></output>", "status");
        yield return Case("<progress id=t></progress>", "progressbar");
        yield return Case("<meter id=t></meter>", "meter");

        // Inputs, by type and by the presence of a list.
        yield return Case("<input id=t>", "textbox");
        yield return Case("<input id=t type=text>", "textbox");
        yield return Case("<input id=t type=email>", "textbox");
        yield return Case("<input id=t type=tel>", "textbox");
        yield return Case("<input id=t type=url>", "textbox");
        yield return Case("<input id=t type=search>", "searchbox");
        yield return Case("<datalist id=d></datalist><input id=t list=d>", "combobox");
        yield return Case("<datalist id=d></datalist><input id=t type=search list=d>", "combobox");
        yield return Case("<input id=t type=checkbox>", "checkbox");
        yield return Case("<input id=t type=radio>", "radio");
        yield return Case("<input id=t type=number>", "spinbutton");
        yield return Case("<input id=t type=range>", "slider");
        yield return Case("<input id=t type=button>", "button");
        yield return Case("<input id=t type=submit>", "button");
        yield return Case("<input id=t type=reset>", "button");
        yield return Case("<input id=t type=image>", "button");
        yield return Case("<input id=t type=password>", "generic");
        yield return Case("<input id=t type=file>", "generic");

        // Images.
        yield return Case("<img id=t src=x.png alt='A cat'>", "image");
        yield return Case("<img id=t src=x.png>", "image");
        yield return Case("<img id=t src=x.png alt=''>", "none");

        // Disclosure, dialogs and figures.
        yield return Case("<details id=t></details>", "group");
        yield return Case("<details><summary id=t>x</summary></details>", "button");
        yield return Case("<div><summary id=t>x</summary></div>", "generic");
        yield return Case("<dialog id=t></dialog>", "dialog");
        yield return Case("<figure id=t></figure>", "figure");
        yield return Case("<figure><figcaption id=t>x</figcaption></figure>", "caption");

        // Phrasing semantics that do map to a role.
        yield return Case("<code id=t>x</code>", "code");
        yield return Case("<del id=t>x</del>", "deletion");
        yield return Case("<ins id=t>x</ins>", "insertion");
        yield return Case("<em id=t>x</em>", "emphasis");
        yield return Case("<strong id=t>x</strong>", "strong");
        yield return Case("<mark id=t>x</mark>", "mark");
        yield return Case("<sub id=t>x</sub>", "subscript");
        yield return Case("<sup id=t>x</sup>", "superscript");
        yield return Case("<dfn id=t>x</dfn>", "term");
        yield return Case("<time id=t>x</time>", "time");
        yield return Case("<b id=t>x</b>", "generic");
        yield return Case("<i id=t>x</i>", "generic");
        yield return Case("<q id=t>x</q>", "generic");
        yield return Case("<small id=t>x</small>", "generic");
    }

    private static TestCaseData Case(string html, string role) => new TestCaseData(html, role).SetArgDisplayNames(html, role);

    [TestCaseSource(nameof(Rows))]
    public void MapsTheElementToTheRoleHtmlAamNames(string html, string expected)
    {
        using var document = PageFixture.Parse(html);
        var element = document.GetElementById("t")!;

        ImplicitRole.For(element).Should().Be(expected);
    }

    [Test]
    public void MapsBrAndAHiddenInputToNoRoleAtAll()
    {
        using var document = PageFixture.Parse("<br id=b><wbr id=w><input id=i type=hidden>");

        ImplicitRole.For(document.GetElementById("b")!).Should().BeNull();
        ImplicitRole.For(document.GetElementById("w")!).Should().BeNull();
        ImplicitRole.For(document.GetElementById("i")!).Should().BeNull();
    }

    [Test]
    public void TreatsMetadataContentAsProducingNoNode()
    {
        using var document = PageFixture.Parse("<head><title>t</title></head><body><script id=s></script><style id=y></style><template id=p></template></body>");

        foreach (var id in new[] { "s", "y", "p" })
        {
            ImplicitRole.IsMetadataContent(document.GetElementById(id)!).Should().BeTrue();
        }
    }

    [Test]
    public void AnExplicitRoleOverridesTheImplicitOne()
    {
        using var document = PageFixture.Parse("<div id=t role=button>x</div>");
        var root = AccessibilityTree.Build(document);

        Find(root, "button").Should().NotBeNull();
    }

    [Test]
    public void AnUnknownExplicitRoleFallsBackToTheImplicitOne()
    {
        AriaRoles.Explicit("nonsense").Should().BeNull();
        AriaRoles.Explicit("doc-chapter region").Should().Be("region");
        AriaRoles.Explicit("BUTTON").Should().Be("button");
        AriaRoles.Explicit(null).Should().BeNull();

        using var document = PageFixture.Parse("<nav id=t role=nonsense></nav>");
        AccessibleName.ResolveRole(document.GetElementById("t")!).Should().Be("navigation");
    }

    [Test]
    public void AnAbstractRoleIsNotAValidExplicitRole()
    {
        // ARIA forbids an author from using an abstract role, so `role="widget"` names nothing and the
        // implicit role stands.
        AriaRoles.Explicit("widget").Should().BeNull();
        AriaRoles.Explicit("landmark").Should().BeNull();
        AriaRoles.Explicit("input").Should().BeNull();
    }

    internal static AxNode? Find(AxNode node, string role)
    {
        if (string.Equals(node.Role, role, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (Find(child, role) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    internal static IReadOnlyList<AxNode> FindAll(AxNode node, string role)
    {
        var found = new List<AxNode>();
        Collect(node, role, found);
        return found;

        static void Collect(AxNode node, string role, List<AxNode> into)
        {
            if (string.Equals(node.Role, role, StringComparison.Ordinal))
            {
                into.Add(node);
            }

            foreach (var child in node.Children)
            {
                Collect(child, role, into);
            }
        }
    }
}
