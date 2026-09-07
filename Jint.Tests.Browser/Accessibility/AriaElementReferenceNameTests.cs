using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// A relationship a page expressed as <c>element.ariaLabelledByElements</c> rather than as idrefs is part of
/// the accessible name, the accessible description and the role that depends on being named.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file has an engine where the rest of the directory has none.</b> `PageFixture` is engine-free
/// on purpose and the computation still is: it takes an <c>IElement</c> and nothing else. What needs an
/// engine here is the *page*, because setting the IDL attribute is the only way to express the relationship
/// this is about — and it is the case the content attribute cannot describe, since setting it writes the
/// empty string and holds the elements by reference so that a page may name an element no id could name.
/// </para>
/// <para>
/// The corpus cannot see any of this: <c>html/dom/aria-element-reflection-labelledby.html</c> is not vendored
/// and no accname corpus is, so these assertions are the only thing standing between the two halves
/// agreeing and quietly disagreeing.
/// </para>
/// </remarks>
public sealed class AriaElementReferenceNameTests
{
    private const string Page = """
        <!doctype html>
        <html><body>
          <span id="billing">Billing</span>
          <span id="address">Address</span>
          <span id="hint">Enter your full address</span>
          <input id="field">
          <section id="region"></section>
        </body></html>
        """;

    [Test]
    public void AnIdlSetLabelledByRelationshipNamesTheElement()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            (function () {
              const field = document.getElementById('field');
              field.ariaLabelledByElements = [document.getElementById('billing'), document.getElementById('address')];
            })()
            """);

        // The content attribute is the empty string, which is exactly why reading it alone is not enough.
        fixture.Text("document.getElementById('field').getAttribute('aria-labelledby')").Should().Be("");
        Name(fixture, "field").Should().Be("Billing Address");
    }

    [Test]
    public void AnIdlSetDescribedByRelationshipDescribesTheElement()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            (function () {
              const field = document.getElementById('field');
              field.ariaLabel = 'Address';
              field.ariaDescribedByElements = [document.getElementById('hint')];
            })()
            """);

        Description(fixture, "field").Should().Be("Enter your full address");
    }

    /// <summary>
    /// <c>section</c> takes its role from whether it is named, and an IDL-set relationship names it — which
    /// is the third site the two halves could disagree at.
    /// </summary>
    [Test]
    public void AnIdlSetLabelledByRelationshipGivesASectionItsRegionRole()
    {
        using var fixture = DomTestFixture.Create(Page);

        AccessibleName.ResolveRole(fixture.Document.GetElementById("region")!).Should().Be("generic");

        fixture.Evaluate("document.getElementById('region').ariaLabelledByElements = [document.getElementById('billing')]");

        AccessibleName.ResolveRole(fixture.Document.GetElementById("region")!).Should().Be("region");
    }

    /// <summary>
    /// The attribute change steps reach the accessibility tree too: an idref a page writes over an IDL-set
    /// relationship wins, and the name follows it. This is the case a reader of the stored field alone would
    /// get wrong, because those steps run inside the binding rather than on a notification.
    /// </summary>
    [Test]
    public void AnIdrefWrittenOverAnIdlSetRelationshipNamesTheElementInstead()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            (function () {
              const field = document.getElementById('field');
              field.ariaLabelledByElements = [document.getElementById('billing')];
              field.setAttribute('aria-labelledby', 'address');
            })()
            """);

        Name(fixture, "field").Should().Be("Address");
    }

    /// <summary>
    /// And a relationship whose target has left the tree is not exposed, so the name falls back the way it
    /// would if the relationship had never been made.
    /// </summary>
    [Test]
    public void ARelationshipWhoseTargetLeftTheTreeDoesNotName()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            (function () {
              const field = document.getElementById('field');
              field.ariaLabel = 'Fallback';
              field.ariaLabelledByElements = [document.getElementById('billing')];
              document.getElementById('billing').remove();
            })()
            """);

        Name(fixture, "field").Should().Be("Fallback");
    }

    private static string Name(DomTestFixture fixture, string id)
    {
        var element = fixture.Document.GetElementById(id)!;
        return Computation().Compute(element, AccessibleName.ResolveRole(element));
    }

    private static string Description(DomTestFixture fixture, string id)
    {
        var element = fixture.Document.GetElementById(id)!;
        var computation = Computation();
        var name = computation.Compute(element, AccessibleName.ResolveRole(element));
        return computation.ComputeDescription(element, name);
    }

    private static AccessibleName Computation() => new(new ElementVisibility(useComputedStyle: true));
}
