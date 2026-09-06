using AngleSharp.Dom;
using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// The accessible name and description computation, step by step and then by its edge cases.
/// </summary>
public sealed class AccessibleNameTests
{
    private static IEnumerable<TestCaseData> Names()
    {
        // 2B: aria-labelledby wins over everything below it, and joins its targets with a space.
        yield return Case("<span id=a>Hello</span><span id=b>world</span><button id=t aria-labelledby='a b' aria-label=Ignored>Content</button>", "Hello world");
        yield return Case("<button id=t aria-labelledby='missing'>Content</button>", "Content");

        // 2B again: a hidden node that is the direct target of the reference still contributes (accname 2A).
        yield return Case("<span id=a hidden>Hidden label</span><button id=t aria-labelledby=a>Content</button>", "Hidden label");
        yield return Case("<span id=a style='display:none'>Hidden label</span><button id=t aria-labelledby=a>Content</button>", "Hidden label");

        // 2D: aria-label.
        yield return Case("<button id=t aria-label='Close dialog'>x</button>", "Close dialog");
        yield return Case("<button id=t aria-label='   '>Content</button>", "Content");

        // 2E: the native host language label.
        yield return Case("<label for=t>Full name</label><input id=t>", "Full name");
        yield return Case("<label>Full name <input id=t></label>", "Full name");
        yield return Case("<label for=t>A</label><label for=t>B</label><input id=t>", "A B");
        yield return Case("<label for=t hidden>Hidden</label><label for=t>Visible</label><input id=t>", "Hidden Visible");
        yield return Case("<label for=t aria-hidden=true>Hidden</label><label for=t>Visible</label><input id=t>", "Hidden Visible");
        yield return Case("<label for=t style='display:none'>Hidden</label><label for=t>Visible</label><input id=t>", "Hidden Visible");
        yield return Case("<input id=t placeholder='Search the docs'>", "Search the docs");
        yield return Case("<label for=t>Query</label><input id=t placeholder='Search'>", "Query");
        yield return Case("<label for=t>Native</label><input id=t aria-label='ARIA label'>", "ARIA label");
        yield return Case("<span id=a hidden>Referenced</span><label for=t>Native</label><input id=t aria-labelledby=a aria-label='ARIA label'>", "Referenced");
        yield return Case("<input id=t type=submit>", "Submit");
        yield return Case("<input id=t type=submit value='Send it'>", "Send it");
        yield return Case("<input id=t type=reset>", "Reset");
        yield return Case("<input id=t type=button value=Go>", "Go");
        yield return Case("<input id=t type=image alt='Search'>", "Search");
        yield return Case("<input id=t type=image>", "Submit Query");
        yield return Case("<img id=t src=x.png alt='A red cat'>", "A red cat");
        yield return Case("<fieldset id=t><legend>Shipping</legend></fieldset>", "Shipping");
        yield return Case("<table id=t><caption>Sales</caption><tr><td>x</td></tr></table>", "Sales");
        yield return Case("<figure id=t><figcaption>Figure 1</figcaption></figure>", "Figure 1");
        yield return Case("<select><optgroup id=t label='Group A'><option>x</option></optgroup></select>", "Group A");

        // 2C: an embedded control inside a label contributes its value, not its own label.
        yield return Case("<label for=q>Search <input id=q value=cats></label><div id=t role=link aria-labelledby=q>x</div>", "cats");

        // 2F: name from content, only for the roles that allow it.
        yield return Case("<button id=t>Save <b>now</b></button>", "Save now");
        yield return Case("<a id=t href='/x'>Read <em>more</em></a>", "Read more");
        yield return Case("<h2 id=t>A <span>nested</span> heading</h2>", "A nested heading");
        yield return Case("<div id=t>Plain text</div>", "");
        yield return Case("<p id=t>Plain text</p>", "");
        yield return Case("<select><option id=t>Choice</option></select>", "Choice");

        // 2F: a button whose content is an image takes the image's alternative text.
        yield return Case("<button id=t><img src=save.png alt=Save></button>", "Save");
        yield return Case("<button id=t><img src=deco.png alt=''>Save</button>", "Save");
        yield return Case("<button id=t><img src=deco.png alt=''></button>", "");

        // 2F: whitespace joins by HTML's suggested display, not by a used one.
        yield return Case("<button id=t><b>a</b><b>b</b></button>", "ab");
        yield return Case("<button id=t><div>a</div><div>b</div></button>", "a b");
        yield return Case("<button id=t>  Save\n  the   file  </button>", "Save the file");

        // 2A inside content: a hidden descendant contributes nothing.
        yield return Case("<button id=t>Save<span hidden> secretly</span></button>", "Save");
        yield return Case("<button id=t>Save<span aria-hidden=true> secretly</span></button>", "Save");
        yield return Case("<button id=t>Save<span style='display:none'> secretly</span></button>", "Save");

        // 2I: a title, but only when everything above produced nothing.
        yield return Case("<button id=t title='Tooltip only'></button>", "Tooltip only");
        yield return Case("<button id=t title='Tooltip'>Content</button>", "Content");
        yield return Case("<div id=t title='Tooltip only'></div>", "Tooltip only");
    }

    private static TestCaseData Case(string html, string name) => new TestCaseData(html, name).SetArgDisplayNames(html, name.Length == 0 ? "(empty)" : name);

    [TestCaseSource(nameof(Names))]
    public void ComputesTheNameAccnameSpecifies(string html, string expected)
    {
        using var document = PageFixture.Parse(html);
        Name(document, "t").Should().Be(expected);
    }

    [Test]
    public void AnAriaLabelledByCycleTerminates()
    {
        using var document = PageFixture.Parse("<div id=t role=button aria-labelledby=b>A</div><div id=b role=button aria-labelledby=t>B</div>");

        // The visited guard stops the recursion; what matters is that it answers at all.
        Name(document, "t").Should().Be("B");
    }

    [Test]
    public void ALabelledControlDoesNotNameItselfThroughItsOwnLabel()
    {
        // The control being named is on the visited set before its label is walked, so the label's content
        // contributes and the control's value does not.
        using var document = PageFixture.Parse("<label>Name <input id=t value=Bob></label>");

        Name(document, "t").Should().Be("Name");
    }

    [Test]
    public void NameFromContentReachesANestedControlsValue()
    {
        using var document = PageFixture.Parse("<div id=t role=button>Total <input value='42' readonly></div>");

        Name(document, "t").Should().Be("Total 42");
    }

    [Test]
    public void ADescriptionComesFromDescribedByThenTitleThenPlaceholder()
    {
        using var document = PageFixture.Parse(
            "<span id=d>The long help text</span>" +
            "<input id=a aria-label=A aria-describedby=d title=T placeholder=P>" +
            "<input id=b aria-label=B title=T placeholder=P>" +
            "<input id=c aria-label=C placeholder=P>" +
            "<input id=e placeholder=P>");

        Description(document, "a").Should().Be("The long help text");
        Description(document, "b").Should().Be("T");
        Description(document, "c").Should().Be("P");

        // The placeholder became the name, so it cannot also be the description.
        Name(document, "e").Should().Be("P");
        Description(document, "e").Should().BeEmpty();
    }

    [Test]
    public void FlattenCollapsesEveryRunOfWhitespaceAndTrimsTheEnds()
    {
        AccessibleName.Flatten("  a \n\t b  ").Should().Be("a b");
        AccessibleName.Flatten("   ").Should().BeEmpty();
        AccessibleName.Flatten("").Should().BeEmpty();
    }

    private static string Name(IDocument document, string id)
    {
        var element = document.GetElementById(id)!;
        return Computation(document).Compute(element, AccessibleName.ResolveRole(element));
    }

    private static string Description(IDocument document, string id)
    {
        var element = document.GetElementById(id)!;
        var computation = Computation(document);
        var name = computation.Compute(element, AccessibleName.ResolveRole(element));
        return computation.ComputeDescription(element, name);
    }

    private static AccessibleName Computation(IDocument document)
    {
        _ = document;
        return new AccessibleName(new ElementVisibility(useComputedStyle: true));
    }
}
