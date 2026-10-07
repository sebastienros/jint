namespace Jint.Tests.Browser.Dom;

/// <summary>HTML's shared association between labels and labelable form controls.</summary>
public sealed class LabelAssociationTests
{
    private const string Page = """
        <!doctype html>
        <html><body>
        <label id="first" for="named">First</label>
        <label id="hidden" for="named" hidden>Hidden</label>
        <input id="named">
        <label id="second" for="named">Second</label>
        <label id="wrapped">Wrapped <input id="inside"></label>
        <label id="empty" for="">Not wrapped <input id="not-associated"></label>
        </body></html>
        """;

    [Test]
    public void LabelsReturnsExplicitAndWrappingLabelsInTreeOrder()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const explicit = Array.from(document.getElementById('named').labels, x => x.id).join(',');
              const wrapped = Array.from(document.getElementById('inside').labels, x => x.id).join(',');
              const empty = document.getElementById('not-associated').labels.length;
              return [explicit, wrapped, empty].join('|');
            })()
            """).Should().Be("first,hidden,second|wrapped|0");
    }

    [Test]
    public void LabelControlUsesForOrTheFirstLabelableDescendant()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              return [
                document.getElementById('first').control.id,
                document.getElementById('wrapped').control.id,
                String(document.getElementById('empty').control),
              ].join('|');
            })()
            """).Should().Be("named|inside|null");
    }

    [Test]
    public void LabelsIsALiveNodeList()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const input = document.getElementById('named');
              const labels = input.labels;
              const before = labels.length;
              document.getElementById('second').htmlFor = 'inside';
              return [before, labels.length, labels.item(1).id].join('|');
            })()
            """).Should().Be("3|2|hidden");
    }

    [Test]
    public void AHiddenInputHasNoLabelsNodeList()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const input = document.createElement('input');
              input.type = 'hidden';
              return String(input.labels);
            })()
            """).Should().Be("null");
    }
    [Test]
    public void LabelsRetainsItsIdentityThroughFormOwnerChangesAndHiddenState()
    {
        using var fixture = DomTestFixture.Create("""
            <!doctype html><form id="a"><label id="label" for="control">Name</label></form>
            <form id="b"><input id="control"></form>
            """);
        fixture.Text("""
            (() => {
                const input = document.getElementById('control'), label = document.getElementById('label');
                const labels = input.labels;
                labels.marker = 42;
                const initial = input.labels === labels && labels[0] === label;
                input.setAttribute('form', 'a');
                const reassociated = input.labels === labels && labels[0] === label;
                input.type = 'hidden';
                const hidden = input.labels === null && labels.length === 0 && labels.item(0) === null;
                input.type = 'text';
                return [initial, reassociated, hidden, input.labels === labels,
                    input.labels.marker, input.labels[0] === label].join('|');
            })()
            """).Should().Be("true|true|true|true|42|true");
    }

}
