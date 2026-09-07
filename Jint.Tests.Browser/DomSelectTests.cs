using Jint.Native.Object;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser;

public sealed class DomSelectTests
{
    private const string Document = """
        <form id="form">
          <select id="select" name="choice">
            <option value="a" selected>A</option>
            <optgroup label="Group">
              <option value="b" disabled>B</option>
              <option value="c">C</option>
            </optgroup>
          </select>
        </form>
        """;

    [TestCase(false)]
    [TestCase(true)]
    public void SelectedIndexUpdatesTheSameLiveOptionState(bool multiple)
    {
        using var fixture = DomTestFixture.Create(Document);
        fixture.Evaluate("var select = document.getElementById('select'); var selected = select.selectedOptions;");
        fixture.Evaluate("select.multiple = " + (multiple ? "true" : "false"));
        fixture.Evaluate("select.options[2].selected = true; select.selectedIndex = 1;");

        fixture.Number("select.selectedIndex").Should().Be(1);
        fixture.Number("select.options.selectedIndex").Should().Be(1);
        fixture.Text("select.value").Should().Be("b", "programmatic selection may select a disabled option");
        fixture.Text("Array.from(select.options, option => option.selected).join('|')").Should().Be("false|true|false");
        fixture.Number("selected.length").Should().Be(1);
        fixture.Bool("selected[0] === select.options[1]").Should().BeTrue();
        fixture.Text("Array.from(select.options, option => option.defaultSelected).join('|')").Should().Be("true|false|false");

        fixture.Evaluate("select.options.selectedIndex = 2");
        fixture.Number("select.selectedIndex").Should().Be(2);
        fixture.Text("select.value").Should().Be("c");
    }

    [TestCase("-1", -1)]
    [TestCase("-2", -1)]
    [TestCase("3", -1)]
    [TestCase("2147483647", -1)]
    [TestCase("'2'", 2)]
    [TestCase("1.9", 1)]
    [TestCase("null", 0)]
    [TestCase("undefined", 0)]
    [TestCase("NaN", 0)]
    [TestCase("4294967297", 1)]
    public void SelectedIndexUsesWebIdlLongConversionAndClearsOutOfRangeValues(string value, int expected)
    {
        using var fixture = DomTestFixture.Create(Document);
        fixture.Evaluate("var select = document.getElementById('select'); select.selectedIndex = " + value);
        fixture.Number("select.selectedIndex").Should().Be(expected);
        fixture.Number("select.selectedOptions.length").Should().Be(expected < 0 ? 0 : 1);
        if (expected < 0)
        {
            fixture.Text("select.value").Should().BeEmpty();
        }
    }

    [Test]
    public void SelectedIndexIsABrandedPrototypeAccessorWithoutAnExpando()
    {
        using var fixture = DomTestFixture.Create(Document);
        fixture.Text("""
            (() => {
              const p = HTMLSelectElement.prototype;
              const d = Object.getOwnPropertyDescriptor(p, 'selectedIndex');
              const select = document.getElementById('select');
              select.selectedIndex = 2;
              let errors = 0;
              for (const value of [{}, undefined, document.createElement('input')]) {
                try { d.set.call(value, 0); } catch (e) { if (e instanceof TypeError) errors++; }
              }
              return [typeof d.get, typeof d.set, d.enumerable, d.configurable,
                Object.hasOwn(select, 'selectedIndex'), errors].join('|');
            })()
            """).Should().Be("function|function|true|true|false|3");
        fixture.Engine.Advanced.HasSharedShape((ObjectInstance) fixture.Evaluate("HTMLSelectElement.prototype"))
            .Should().BeTrue();
    }

    [Test]
    public void AnEmptySelectAcceptsSelectionWithoutCreatingAnOption()
    {
        using var fixture = DomTestFixture.Create("<select id='select'></select>");
        fixture.Evaluate("var select = document.getElementById('select'); select.selectedIndex = 0;");
        fixture.Number("select.selectedIndex").Should().Be(-1);
        fixture.Number("select.length").Should().Be(0);
    }

    [Test]
    public async Task SettingSelectedIndexFiresNoInputEventsAndFormResetRestoresDefaults()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync(Document);
        (await fixture.Page.EvaluateAsync<string>("""
            (() => {
              const select = document.getElementById('select');
              let events = 0;
              select.addEventListener('input', () => events++);
              select.addEventListener('change', () => events++);
              select.selectedIndex = 2;
              const before = select.value;
              document.getElementById('form').reset();
              return [before, select.value, select.selectedIndex, events,
                select.options[0].defaultSelected, select.options[2].defaultSelected].join('|');
            })()
            """)).Should().Be("c|a|0|0|true|false");
        fixture.Page.Errors.Should().BeEmpty();
    }
}
