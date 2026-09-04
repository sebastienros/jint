namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://w3c.github.io/aria/#ARIAMixin">ARIA §10.1</a>'s element-reflecting half:
/// <c>ariaActiveDescendantElement</c> and the seven <c>…Elements</c> arrays, each reflecting an
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#attr-associated-element">attr-associated element</a>.
/// </summary>
/// <remarks>
/// The corpus that owns this is <c>html/dom/aria-element-reflection.html</c> and its disconnected sibling,
/// which the browser lane runs whole. What is here is the half of the standard those two documents state
/// least directly — the three-way split between "no content attribute", "an idref that resolves now" and "a
/// reference that was set explicitly" — plus the one place this diverges, so that closing the divergence
/// fails a test rather than merely satisfying a reviewer.
/// </remarks>
public sealed class AriaElementReflectionTests
{
    private const string Page = """
        <!doctype html>
        <html><body>
          <div id="owner" aria-activedescendant="one" aria-describedby="one two"></div>
          <div id="one"></div>
          <div id="two"></div>
          <div id="bare"></div>
        </body></html>
        """;

    /// <summary>
    /// The three states a page can tell apart: no content attribute is <c>null</c>, a content attribute whose
    /// ids resolve is those elements, and a content attribute whose ids resolve to nothing is empty rather
    /// than absent.
    /// </summary>
    [Test]
    public void AbsentResolvedAndUnresolvedAreThreeDifferentAnswers()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("document.getElementById('bare').ariaActiveDescendantElement").IsNull().Should().BeTrue();
        fixture.Evaluate("document.getElementById('bare').ariaDescribedByElements").IsNull().Should().BeTrue();

        fixture.Bool("document.getElementById('owner').ariaActiveDescendantElement === document.getElementById('one')")
            .Should().BeTrue();
        fixture.Text("document.getElementById('owner').ariaDescribedByElements.map(e => e.id).join(',')")
            .Should().Be("one,two");

        fixture.Evaluate("document.getElementById('owner').setAttribute('aria-describedby', 'nothing-has-this-id')");
        fixture.Number("document.getElementById('owner').ariaDescribedByElements.length").Should().Be(0);
        fixture.Evaluate("document.getElementById('owner').ariaDescribedByElements").IsNull().Should().BeFalse();
    }

    /// <summary>
    /// Setting the IDL attribute writes the <b>empty string</b> and holds the element by reference, so it
    /// works for an element no id could name and survives that element's id changing under it.
    /// </summary>
    [Test]
    public void AnExplicitReferenceNeedsNoIdAndSurvivesOneChanging()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            (function () {
              const anonymous = document.createElement('span');
              document.body.appendChild(anonymous);
              document.getElementById('owner').ariaActiveDescendantElement = anonymous;
            })()
            """);

        fixture.Text("document.getElementById('owner').getAttribute('aria-activedescendant')").Should().Be("");
        fixture.Bool("document.getElementById('owner').ariaActiveDescendantElement === document.body.lastElementChild")
            .Should().BeTrue();

        fixture.Evaluate("document.getElementById('one').id = 'renamed'");
        fixture.Bool("document.getElementById('owner').ariaActiveDescendantElement === document.body.lastElementChild")
            .Should().BeTrue();
    }

    /// <summary>
    /// HTML's attribute change steps: an idref a page writes wins over a reference it set earlier, and
    /// removing the attribute ends the relationship rather than leaving the old reference to be uncovered.
    /// </summary>
    [Test]
    public void WritingTheContentAttributeDropsTheExplicitReference()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaDescribedByElements = [document.getElementById('two')];
              owner.setAttribute('aria-describedby', 'one');
              return owner.ariaDescribedByElements.map(e => e.id).join(',');
            })()
            """).Should().Be("one");

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaDescribedByElements = [document.getElementById('two')];
              owner.removeAttribute('aria-describedby');
              owner.setAttribute('aria-describedby', 'one');
              return owner.ariaDescribedByElements.map(e => e.id).join(',');
            })()
            """).Should().Be("one");
    }

    /// <summary>
    /// <b>The one divergence.</b> HTML's attribute change steps drop the explicit reference for
    /// <em>every</em> write of the content attribute; this reads the attribute's value rather than an
    /// observer, and the value the IDL setter writes is the empty string — so a page writing that same empty
    /// string by hand keeps the reference where a browser drops it. Pinned so that closing it is a
    /// deliberate act, and <c>Jint.Browser/Dom/AGENTS.md</c> records it.
    /// </summary>
    [Test]
    public void AnEmptyStringWrittenByHandKeepsTheReferenceThatABrowserDrops()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaDescribedByElements = [document.getElementById('two')];
              owner.setAttribute('aria-describedby', '');
              return owner.ariaDescribedByElements.map(e => e.id).join(',');
            })()
            """).Should().Be("two");
    }

    /// <summary>
    /// A relationship is exposed only while both ends are in a tree the referring element can see, and the
    /// reference itself is kept — so leaving the document hides it and coming back restores it.
    /// </summary>
    [Test]
    public void LeavingTheTreeHidesAReferenceAndReturningRestoresIt()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one');
              owner.ariaDescribedByElements = [one, document.getElementById('two')];

              one.remove();
              const hidden = owner.ariaDescribedByElements.map(e => e.id).join(',');

              document.body.appendChild(one);
              const restored = owner.ariaDescribedByElements.map(e => e.id).join(',');

              return hidden + '|' + restored + '|' + owner.getAttribute('aria-describedby');
            })()
            """).Should().Be("two|one,two|");
    }

    /// <summary>
    /// WebIDL's <c>FrozenArray</c> attribute answers the same array until its value changes, per member and
    /// per element — which is what makes <c>el.ariaOwnsElements === el.ariaOwnsElements</c> hold — and the
    /// array it answers is frozen.
    /// </summary>
    [Test]
    public void AnArrayMemberAnswersTheSameFrozenArrayUntilItsValueChanges()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaOwnsElements = [document.getElementById('one')];

              const stable = owner.ariaOwnsElements === owner.ariaOwnsElements;
              const frozen = Object.isFrozen(owner.ariaOwnsElements);

              const before = owner.ariaOwnsElements;
              owner.ariaOwnsElements = [document.getElementById('two')];

              return stable + '|' + frozen + '|' + (before !== owner.ariaOwnsElements);
            })()
            """).Should().Be("true|true|true");
    }

    /// <summary>
    /// Two members are two IDL types, and each refuses what the other takes: the single-element member
    /// refuses a list, and a <c>FrozenArray</c> member refuses a bare element and a list of non-elements.
    /// The refusal happens before anything is written.
    /// </summary>
    [Test]
    public void EachMemberRefusesTheOtherOnesValue()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              const before = owner.getAttribute('aria-activedescendant');
              try { owner.ariaActiveDescendantElement = [document.getElementById('one')]; return 'no throw'; }
              catch (e) { return e.constructor.name + '|' + (owner.getAttribute('aria-activedescendant') === before); }
            })()
            """).Should().Be("TypeError|true");

        fixture.Text("""
            (function () {
              try { document.getElementById('owner').ariaOwnsElements = document.getElementById('one'); return 'no throw'; }
              catch (e) { return e.constructor.name; }
            })()
            """).Should().Be("TypeError");

        fixture.Text("""
            (function () {
              try { document.getElementById('owner').ariaOwnsElements = [1, 2]; return 'no throw'; }
              catch (e) { return e.constructor.name; }
            })()
            """).Should().Be("TypeError");
    }

    /// <summary>
    /// A wrong receiver is WebIDL's <c>TypeError</c>, the same as every projected member's, because these
    /// accessors go through the same brand check and the same guard as the string half's.
    /// </summary>
    [Test]
    public void AWrongReceiverIsAnIllegalInvocation()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              try { Object.getOwnPropertyDescriptor(Element.prototype, 'ariaOwnsElements').get.call({}); return 'no throw'; }
              catch (e) { return e.constructor.name + ': ' + e.message; }
            })()
            """).Should().Be("TypeError: Failed to execute 'Element.ariaOwnsElements': Illegal invocation");
    }
}
