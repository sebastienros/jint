namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://w3c.github.io/aria/#ARIAMixin">ARIA §10.1's <c>ARIAMixin</c></a> on <c>Element</c>:
/// <c>role</c> and the <c>aria-*</c> IDL attributes, each a view of one content attribute.
/// </summary>
/// <remarks>
/// The mixin was not on <c>Element</c> at all, so <c>element.role</c> was <c>undefined</c> and
/// <c>element.ariaLabel = 'x'</c> made an expando and wrote no attribute — which is what
/// <c>html/dom/aria-attribute-reflection.html</c> says forty-one times and
/// <c>custom-elements/reactions/AriaMixin-string-attributes.html</c> eighty more.
/// </remarks>
public sealed class AriaReflectionTests
{
    private const string Page = """
        <!doctype html>
        <html><body><div id="a" role="button" aria-label="Close" aria-checked="true"></div></body></html>
        """;

    /// <summary>
    /// Both directions of one attribute, for every member of the mixin's string half: the IDL attribute
    /// reads the content attribute, writing it writes the content attribute, and <c>null</c> and
    /// <c>undefined</c> both remove it.
    /// </summary>
    [TestCase("role", "role")]
    [TestCase("ariaAtomic", "aria-atomic")]
    [TestCase("ariaAutoComplete", "aria-autocomplete")]
    [TestCase("ariaBrailleLabel", "aria-braillelabel")]
    [TestCase("ariaBrailleRoleDescription", "aria-brailleroledescription")]
    [TestCase("ariaBusy", "aria-busy")]
    [TestCase("ariaChecked", "aria-checked")]
    [TestCase("ariaColCount", "aria-colcount")]
    [TestCase("ariaColIndex", "aria-colindex")]
    [TestCase("ariaColIndexText", "aria-colindextext")]
    [TestCase("ariaColSpan", "aria-colspan")]
    [TestCase("ariaCurrent", "aria-current")]
    [TestCase("ariaDescription", "aria-description")]
    [TestCase("ariaDisabled", "aria-disabled")]
    [TestCase("ariaExpanded", "aria-expanded")]
    [TestCase("ariaHasPopup", "aria-haspopup")]
    [TestCase("ariaHidden", "aria-hidden")]
    [TestCase("ariaInvalid", "aria-invalid")]
    [TestCase("ariaKeyShortcuts", "aria-keyshortcuts")]
    [TestCase("ariaLabel", "aria-label")]
    [TestCase("ariaLevel", "aria-level")]
    [TestCase("ariaLive", "aria-live")]
    [TestCase("ariaModal", "aria-modal")]
    [TestCase("ariaMultiLine", "aria-multiline")]
    [TestCase("ariaMultiSelectable", "aria-multiselectable")]
    [TestCase("ariaOrientation", "aria-orientation")]
    [TestCase("ariaPlaceholder", "aria-placeholder")]
    [TestCase("ariaPosInSet", "aria-posinset")]
    [TestCase("ariaPressed", "aria-pressed")]
    [TestCase("ariaReadOnly", "aria-readonly")]
    [TestCase("ariaRelevant", "aria-relevant")]
    [TestCase("ariaRequired", "aria-required")]
    [TestCase("ariaRoleDescription", "aria-roledescription")]
    [TestCase("ariaRowCount", "aria-rowcount")]
    [TestCase("ariaRowIndex", "aria-rowindex")]
    [TestCase("ariaRowIndexText", "aria-rowindextext")]
    [TestCase("ariaRowSpan", "aria-rowspan")]
    [TestCase("ariaSelected", "aria-selected")]
    [TestCase("ariaSetSize", "aria-setsize")]
    [TestCase("ariaSort", "aria-sort")]
    [TestCase("ariaValueMax", "aria-valuemax")]
    [TestCase("ariaValueMin", "aria-valuemin")]
    [TestCase("ariaValueNow", "aria-valuenow")]
    [TestCase("ariaValueText", "aria-valuetext")]
    public void EachMemberReflectsItsContentAttribute(string member, string attribute)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const el = document.createElement('div');
              const absent = String(el.{{member}});

              el.setAttribute('{{attribute}}', 'from the attribute');
              const read = String(el.{{member}});

              el.{{member}} = 'from the property';
              const written = el.getAttribute('{{attribute}}');

              el.{{member}} = null;
              const cleared = [String(el.{{member}}), el.hasAttribute('{{attribute}}')];

              el.{{member}} = 'again';
              el.{{member}} = undefined;
              const undef = [String(el.{{member}}), el.hasAttribute('{{attribute}}')];

              return [absent, read, written, cleared.join(','), undef.join(',')].join('|');
            })()
            """).Should().Be("null|from the attribute|from the property|null,false|null,false");
    }

    /// <summary>
    /// The parser's attributes are visible through the IDL attributes without anything having to
    /// synchronise, because there is only ever one place the value lives.
    /// </summary>
    [Test]
    public void TheMarkupsAttributesAreWhatTheMembersAnswer()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const el = document.getElementById('a');
              el.setAttribute('aria-label', 'Dismiss');
              return [el.role, el.ariaLabel, el.ariaChecked, String(el.ariaBusy)].join('|');
            })()
            """).Should().Be("button|Dismiss|true|null");
    }

    /// <summary>
    /// The property attributes are WebIDL's for an attribute — an accessor pair on the interface prototype,
    /// enumerable and configurable — which is what makes the member the standard's rather than an expando.
    /// </summary>
    [Test]
    public void TheMembersAreAccessorsOnElementsPrototype()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const own = Object.getOwnPropertyDescriptor(Element.prototype, 'ariaLabel');
              const el = document.getElementById('a');
              return [
                'ariaLabel' in el,
                el.hasOwnProperty('ariaLabel'),
                typeof own.get,
                typeof own.set,
                own.enumerable,
                own.configurable,
              ].join('|');
            })()
            """).Should().Be("true|false|function|function|true|true");
    }

    /// <summary>
    /// It is on <c>Element</c>, so every element interface has it and an SVG or a custom element is not a
    /// special case.
    /// </summary>
    [Test]
    public void EveryElementHasIt()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
              const custom = document.createElement('x-thing');
              svg.ariaHidden = 'true';
              custom.role = 'button';
              return [svg.getAttribute('aria-hidden'), custom.getAttribute('role'), custom.role].join('|');
            })()
            """).Should().Be("true|button|button");
    }

    /// <summary>
    /// A wrong receiver is WebIDL's <c>TypeError</c>, the same as every projected member's, because the
    /// accessor pair goes through the same brand check and the same guard.
    /// </summary>
    [Test]
    public void AWrongReceiverIsAnIllegalInvocation()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              try { Object.getOwnPropertyDescriptor(Element.prototype, 'ariaLabel').get.call({}); return 'no throw'; }
              catch (e) { return e.constructor.name + ': ' + e.message; }
            })()
            """).Should().Be("TypeError: Failed to execute 'Element.ariaLabel': Illegal invocation");
    }
}
