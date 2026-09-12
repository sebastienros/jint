using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#dom-option-selected">HTML §4.10.10</a>'s
/// <c>selected</c> IDL setter: set the selectedness, set the dirtiness, and then ask the select for a reset,
/// which is
/// <a href="https://html.spec.whatwg.org/multipage/form-elements.html#selectedness-setting-algorithm">the
/// selectedness setting algorithm</a>.
/// </summary>
public sealed class OptionSelectednessTests
{
    private const string OneChoice = """
        <form id="form">
          <select id="select">
            <option value="a" selected>A</option>
            <option value="b">B</option>
            <option value="c">C</option>
          </select>
        </form>
        """;

    /// <summary>
    /// The algorithm's second step. Nothing here is a selector question: <c>option.selected</c>,
    /// <c>select.selectedIndex</c>, <c>select.value</c> and <c>select.selectedOptions</c> all answered that a
    /// one-choice <c>select</c> held three choices.
    /// </summary>
    [Test]
    public void SettingSelectedOnAOneChoiceSelectDeselectsEveryOtherOption()
    {
        using var fixture = DomTestFixture.Create(OneChoice);
        fixture.Evaluate("var select = document.getElementById('select');");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("true|false|false");

        fixture.Evaluate("select.options[1].selected = true;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("false|true|false");
        fixture.Number("select.selectedIndex").Should().Be(1);
        fixture.Text("select.value").Should().Be("b");
        fixture.Number("select.selectedOptions.length").Should().Be(1);
        fixture.Bool("select.selectedOptions[0] === select.options[1]").Should().BeTrue();

        // Backwards, which is the direction that tells the option asking for the reset apart from the last
        // selected one in tree order: the write wins either way.
        fixture.Evaluate("select.options[2].selected = true; select.options[0].selected = true;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("true|false|false");
        fixture.Number("select.selectedIndex").Should().Be(0);
        fixture.Text("select.value").Should().Be("a");
    }

    /// <summary>
    /// Upstream's own
    /// <a href="https://github.com/web-platform-tests/wpt/blob/master/html/semantics/forms/the-select-element/select-ask-for-reset.html"><c>select-ask-for-reset.html</c></a>
    /// third case, which is the one this stream can answer — the other two are the option insertion and
    /// removing steps, which no member of the binding's surface passes through. It selects every option in
    /// turn forwards and then backwards, so it is what decides that the option which asked for the reset is
    /// the one kept rather than the last selected one in tree order.
    /// </summary>
    [Test]
    public void EveryWriteWinsInBothDirections()
    {
        using var fixture = DomTestFixture.Create("<select id='select'><option>A</option><option>B</option><option>C</option></select>");
        fixture.Evaluate("var select = document.getElementById('select');");
        fixture.Text("""
            (() => {
              const options = select.options;
              const seen = [];
              for (let i = 0; i < options.length; i++) {
                options[i].selected = true;
                seen.push(select.selectedIndex);
              }
              for (let i = options.length - 1; i >= 0; i--) {
                options[i].selected = true;
                seen.push(select.selectedIndex);
              }
              return seen.join('');
            })()
            """).Should().Be("012210");

        // The file's own tail: nothing selected falls to the first eligible option, then to the second once
        // the first is disabled, and to none at all once the display size is no longer 1.
        fixture.Evaluate("select.options[2].selected = true; select.options[2].selected = false;");
        fixture.Number("select.selectedIndex").Should().Be(0);

        fixture.Evaluate("select.options[0].disabled = true; select.options[2].selected = true; select.options[2].selected = false;");
        fixture.Number("select.selectedIndex").Should().Be(1);

        fixture.Evaluate("select.size = 2; select.options[1].selected = false;");
        fixture.Number("select.selectedIndex").Should().Be(-1);
    }

    /// <summary>
    /// The IDL attribute is the selectedness and <c>defaultSelected</c> is the content attribute; §4.10.10
    /// gives the setter no step that writes one, so a form reset still restores the parsed defaults.
    /// </summary>
    [Test]
    public void SettingSelectedLeavesTheContentAttributeAlone()
    {
        using var fixture = DomTestFixture.Create(OneChoice);
        fixture.Evaluate("var select = document.getElementById('select'); select.options[1].selected = true;");
        fixture.Text("Array.from(select.options, option => option.defaultSelected).join('|')").Should().Be("true|false|false");
        fixture.Text("Array.from(select.options, option => option.hasAttribute('selected')).join('|')").Should().Be("true|false|false");
    }

    /// <summary>
    /// The algorithm's first step, which is the one the display size gates: with nothing selected, a
    /// drop-down box selects the first option that is not disabled. So the only option of a one-choice select
    /// cannot be deselected through this setter, and a disabled first option is passed over.
    /// </summary>
    [TestCase("<select id='select'><option selected>A</option><option>B</option></select>", "true|false", 0)]
    [TestCase("<select id='select'><option disabled selected>A</option><option>B</option></select>", "false|true", 1)]
    [TestCase("<select id='select'><optgroup disabled><option selected>A</option></optgroup><option>B</option></select>", "false|true", 1)]
    [TestCase("<select id='select'><option disabled selected>A</option><option disabled>B</option></select>", "false|false", -1)]
    public void DeselectingTheLastSelectedOptionOfADropDownBoxSelectsTheFirstEnabledOne(string html, string expected, int index)
    {
        using var fixture = DomTestFixture.Create(html);
        fixture.Evaluate("var select = document.getElementById('select'); select.options[0].selected = false;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be(expected);
        fixture.Number("select.selectedIndex").Should().Be(index);
    }

    /// <summary>
    /// A <c>multiple</c> select is outside both steps, so every option may be selected at once and none is
    /// selected on its behalf.
    /// </summary>
    [Test]
    public void AMultipleSelectKeepsEveryOptionTheScriptSelected()
    {
        using var fixture = DomTestFixture.Create("<select id='select' multiple><option selected>A</option><option>B</option></select>");
        fixture.Evaluate("var select = document.getElementById('select'); select.options[1].selected = true;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("true|true");
        fixture.Number("select.selectedIndex").Should().Be(0);
        fixture.Number("select.selectedOptions.length").Should().Be(2);

        fixture.Evaluate("select.options[0].selected = false; select.options[1].selected = false;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("false|false");
        fixture.Number("select.selectedIndex").Should().Be(-1);
    }

    /// <summary>
    /// The two steps are gated differently, which is the part a display size alone would get wrong: a list
    /// box with no <c>multiple</c> attribute still holds one selection at a time — step 2 asks only that the
    /// attribute is absent — while step 1 does not select anything on its behalf.
    /// </summary>
    [TestCase("4")]
    [TestCase("2")]
    public void AListBoxWithNoMultipleAttributeStillHoldsOneSelection(string size)
    {
        using var fixture = DomTestFixture.Create(
            "<select id='select' size='" + size + "'><option selected>A</option><option>B</option></select>");
        fixture.Evaluate("var select = document.getElementById('select'); select.options[1].selected = true;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("false|true");
        fixture.Number("select.selectedIndex").Should().Be(1);

        fixture.Evaluate("select.options[1].selected = false;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')")
            .Should().Be("false|false", "step 1 asks for a display size of 1");
        fixture.Number("select.selectedIndex").Should().Be(-1);
    }

    /// <summary>
    /// The display size is the <c>size</c> attribute under HTML's rules for parsing non-negative integers,
    /// which is not the <c>size</c> IDL attribute: that one's missing value default is 0, and a display size
    /// of 0 is not a display size of 1. The last two rows are the pair that proves the two cannot be one
    /// read — <c>size="-1"</c> and <c>size="0"</c> both reflect 0, and only the first is a drop-down box.
    /// </summary>
    [TestCase("", 0, "true|false")]
    [TestCase("1", 1, "true|false")]
    [TestCase(" +1junk", 1, "true|false")]
    [TestCase("1.9", 1, "true|false")]
    [TestCase("junk", 0, "true|false")]
    [TestCase("-1", 0, "true|false")]
    [TestCase("0", 0, "false|false")]
    public void TheDisplaySizeReadsTheAttributeWithHtmlsRulesRatherThanTheSizeIdlAttribute(
        string size,
        int reflected,
        string afterDeselecting)
    {
        using var fixture = DomTestFixture.Create(
            "<select id='select' size='" + size + "'><option selected>A</option><option>B</option></select>");
        fixture.Number("document.getElementById('select').size").Should().Be(reflected);

        fixture.Evaluate("var select = document.getElementById('select'); select.options[0].selected = false;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be(afterDeselecting);
    }

    /// <summary>
    /// The reset reaches the select through an <c>optgroup</c>, and "in no select at all" is the case that
    /// asks nobody: an option outside one keeps whatever the setter wrote, and a detached select is reset
    /// like any other.
    /// </summary>
    [Test]
    public void OnlyAnOptionWithASelectAsksForAReset()
    {
        using var fixture = DomTestFixture.Create("<select id='select'><option selected>A</option><optgroup><option>B</option></optgroup></select>");
        fixture.Evaluate("var select = document.getElementById('select'); select.options[1].selected = true;");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("false|true");

        fixture.Text("""
            (() => {
              const orphan = document.createElement('option');
              orphan.selected = true;
              const detached = document.createElement('select');
              const a = document.createElement('option');
              const b = document.createElement('option');
              a.selected = true;
              detached.append(a, b);
              b.selected = true;
              return [orphan.selected, orphan.defaultSelected, a.selected, b.selected, detached.selectedIndex].join('|');
            })()
            """).Should().Be("true|false|false|true|1");
    }

    /// <summary>
    /// The setter's own dirtiness step is AngleSharp's already, and the reset must not undo it: once a script
    /// has written <c>selected</c>, adding or removing the content attribute changes nothing.
    /// </summary>
    [Test]
    public void ADirtyOptionIgnoresTheContentAttribute()
    {
        using var fixture = DomTestFixture.Create("<select id='select' multiple><option>A</option></select>");
        fixture.Evaluate("var option = document.getElementById('select').options[0]; option.selected = true;");
        fixture.Evaluate("option.removeAttribute('selected');");
        fixture.Bool("option.selected").Should().BeTrue();

        fixture.Evaluate("option.selected = false; option.setAttribute('selected', '');");
        fixture.Bool("option.selected").Should().BeFalse();
        fixture.Bool("option.defaultSelected").Should().BeTrue();
    }

    /// <summary>
    /// The setter is the accessor on the prototype WebIDL declares, not an own property the reset writes onto
    /// the wrapper, and a wrong receiver is a <c>TypeError</c> rather than a silent write.
    /// </summary>
    [Test]
    public void SelectedIsABrandedPrototypeAccessorWithoutAnExpando()
    {
        using var fixture = DomTestFixture.Create(OneChoice);
        fixture.Text("""
            (() => {
              const d = Object.getOwnPropertyDescriptor(HTMLOptionElement.prototype, 'selected');
              const option = document.getElementById('select').options[1];
              option.selected = true;
              let errors = 0;
              for (const value of [{}, undefined, document.createElement('input')]) {
                try { d.set.call(value, true); } catch (e) { if (e instanceof TypeError) errors++; }
              }
              return [typeof d.get, typeof d.set, d.enumerable, d.configurable,
                Object.hasOwn(option, 'selected'), errors].join('|');
            })()
            """).Should().Be("function|function|true|true|false|3");
    }

    /// <summary>
    /// WebIDL's <c>boolean</c> conversion, which is what the corpus's own <c>option.selected = "selected"</c>
    /// depends on: every value is accepted and none of them throws.
    /// </summary>
    [TestCase("true", 1)]
    [TestCase("'selected'", 1)]
    [TestCase("1", 1)]
    [TestCase("{}", 1)]
    [TestCase("''", 0)]
    [TestCase("0", 0)]
    [TestCase("null", 0)]
    [TestCase("undefined", 0)]
    [TestCase("NaN", 0)]
    public void TheSetterTakesWebIdlsBooleanConversion(string value, int expected)
    {
        using var fixture = DomTestFixture.Create("<select id='select' multiple><option>A</option></select>");
        fixture.Evaluate("var option = document.getElementById('select').options[0]; option.selected = " + value);
        fixture.Number("option.selected ? 1 : 0").Should().Be(expected);
    }

    /// <summary>
    /// A form reset restores the parsed defaults over whatever the setter and its reset left, and fires no
    /// <c>input</c> or <c>change</c> event on the way — the setter is a programmatic write, not a user
    /// interaction, so neither half sends select update notifications.
    /// </summary>
    [Test]
    public async Task SettingSelectedFiresNoEventsAndFormResetRestoresTheDefaults()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync(OneChoice);
        (await fixture.Page.EvaluateAsync<string>("""
            (() => {
              const select = document.getElementById('select');
              let events = 0;
              select.addEventListener('input', () => events++);
              select.addEventListener('change', () => events++);
              select.options[2].selected = true;
              const before = [Array.from(select.options, o => o.selected).join(''), select.selectedIndex].join(':');
              document.getElementById('form').reset();
              return [before, Array.from(select.options, o => o.selected).join(''), select.selectedIndex, events].join('|');
            })()
            """)).Should().Be("falsefalsetrue:2|truefalsefalse|0|0");
        fixture.Page.Errors.Should().BeEmpty();
    }
}
