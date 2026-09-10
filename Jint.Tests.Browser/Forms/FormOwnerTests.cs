using System.Text.Json;
using AngleSharp.Html.Dom;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Tests.Browser.Parsing;

namespace Jint.Tests.Browser.Forms;

/// <summary>
/// HTML's <a href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#reset-the-form-owner">reset
/// the form owner</a>: a connected listed element's <c>form</c> content attribute decides its owner, and the
/// ancestor form is only the fallback for the elements the explicit branch does not apply to.
/// </summary>
/// <remarks>
/// AngleSharp's <c>HtmlElement.GetAssignedForm()</c> has the two branches the other way round, so every
/// assertion here about a control inside one form pointing at another is a regression test for
/// <a href="https://github.com/sebastienros/jint/issues/3939">#3939</a>.
/// </remarks>
public sealed class FormOwnerTests
{
    /// <summary>The issue's own markup, read the way the issue reads it — through the window's named properties.</summary>
    [Test]
    public async Task AnExplicitFormAttributeBeatsTheAncestorForm()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """<form id="first"><input id="control" name="field" value="value" form="second"></form><form id="second"></form>""");

        (await page.EvaluateAsync<bool>("control.form === second")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("control.form === first")).Should().BeFalse();
    }

    /// <summary>
    /// Reassociation in both directions at once, so neither answer can be the accident of a scan order: the
    /// control in <c>first</c> is owned by <c>second</c> and the control in <c>second</c> by <c>first</c>.
    /// </summary>
    [Test]
    public void ReassociationWorksInBothDirections()
    {
        using var fixture = DomTestFixture.Create(
            """
            <form id="first"><input id="into-second" form="second"></form>
            <form id="second"><input id="into-first" form="first"></form>
            """);

        fixture.Text("document.getElementById('into-second').form.id").Should().Be("second");
        fixture.Text("document.getElementById('into-first').form.id").Should().Be("first");
    }

    /// <summary>
    /// Step 4 applies on the <i>presence</i> of the attribute and step 5 is an "otherwise" of that condition,
    /// so a target that is missing, is not a form, or is named by the empty string leaves the owner null —
    /// the ancestor form is not a fallback for a failed explicit association.
    /// </summary>
    [TestCase("form='absent'", null, TestName = "A form attribute naming no element at all")]
    [TestCase("form='not-a-form'", null, TestName = "A form attribute naming a non-form element")]
    [TestCase("form=''", null, TestName = "A form attribute that is the empty string")]
    [TestCase("form='second'", "second", TestName = "A form attribute naming a form")]
    [TestCase("", "first", TestName = "No form attribute at all")]
    public void AFailedExplicitAssociationDoesNotFallBackToTheAncestor(string attribute, string? owner)
    {
        using var fixture = DomTestFixture.Create(
            $"""
            <div id="not-a-form"></div>
            <form id="first"><input id="control" {attribute}></form>
            <form id="second"></form>
            """);

        fixture.Text("(document.getElementById('control').form || {}).id ?? null").Should().Be(owner);
    }

    /// <summary>
    /// The lookup is DOM's own — the <i>first</i> element in tree order with that ID, tested for being a form
    /// afterwards — so a non-form element that duplicates a form's ID and comes first leaves the owner null.
    /// </summary>
    [Test]
    public void TheFirstElementWithTheIdDecidesEvenWhenItIsNotAForm()
    {
        using var fixture = DomTestFixture.Create(
            """
            <div id="duplicate"></div><form id="duplicate"></form>
            <input id="first-wins" form="duplicate">
            <form id="reverse"></form><div id="reverse"></div>
            <input id="form-wins" form="reverse">
            """);

        fixture.Text("(document.getElementById('first-wins').form || {}).id ?? null").Should().BeNull();
        fixture.Text("document.getElementById('form-wins').form.id").Should().Be("reverse");
    }

    /// <summary>A control in no form at all, pointing into one: the case AngleSharp already answered.</summary>
    [Test]
    public void AControlOutsideEveryFormCanPointIntoOne()
    {
        using var fixture = DomTestFixture.Create("""<input id="control" form="target"><form id="target"></form>""");

        fixture.Text("document.getElementById('control').form.id").Should().Be("target");
    }

    /// <summary>
    /// Step 4's third condition is <i>connected</i>, so a control in a detached subtree keeps the ancestor
    /// form it is inside however its <c>form</c> attribute reads — and has no owner at all when the subtree
    /// holds no form, even though the attribute names one that is in the document.
    /// </summary>
    [Test]
    public void ADisconnectedControlIgnoresItsFormAttribute()
    {
        using var fixture = DomTestFixture.Create("""<form id="connected"></form>""");
        fixture.Execute(
            """
            var detached = document.createElement('div');
            detached.innerHTML = '<form id="detached"><input id="inside" form="connected"></form>'
                + '<input id="outside" form="connected">';
            var inside = detached.querySelector('#inside');
            var outside = detached.querySelector('#outside');
            """);

        fixture.Text("inside.form.id").Should().Be("detached", "the ancestor form of a detached control still owns it");
        fixture.Text("(outside.form || {}).id ?? null").Should().BeNull();

        // And the owner is recomputed at every read rather than stored, so connecting the subtree hands both
        // controls to the form their attribute names.
        fixture.Execute("document.body.appendChild(detached);");
        fixture.Text("inside.form.id").Should().Be("connected");
        fixture.Text("outside.form.id").Should().Be("connected");
    }

    /// <summary>
    /// Nothing caches the association: the owner moves when the <c>form</c> attribute is rewritten or
    /// removed, and when the <i>target's</i> <c>id</c> is rewritten under it.
    /// </summary>
    [Test]
    public void TheOwnerFollowsTheAttributeAndTheTargetId()
    {
        using var fixture = DomTestFixture.Create(
            """
            <form id="first"><input id="control" form="second"></form>
            <form id="second"></form>
            <form id="third"></form>
            """);

        fixture.Text("document.getElementById('control').form.id").Should().Be("second");

        fixture.Execute("document.getElementById('control').setAttribute('form', 'third');");
        fixture.Text("document.getElementById('control').form.id").Should().Be("third");

        // The target's own id changing is the other half: the control now names nothing, and does not fall
        // back to the form containing it.
        fixture.Execute("document.getElementById('third').id = 'renamed';");
        fixture.Text("(document.getElementById('control').form || {}).id ?? null").Should().BeNull();

        // Removing the attribute is what puts the ancestor form back in charge.
        fixture.Execute("document.getElementById('control').removeAttribute('form');");
        fixture.Text("document.getElementById('control').form.id").Should().Be("first");
    }

    /// <summary>
    /// Every listed element carries the <c>form</c> content attribute and the matching IDL member, so all
    /// seven answer the one rule. <c>keygen</c> is the eighth and is HTML 5.1's rather than HTML's.
    /// </summary>
    [TestCase("<button id='control' form='second'></button>")]
    [TestCase("<fieldset id='control' form='second'></fieldset>")]
    [TestCase("<input id='control' form='second'>")]
    [TestCase("<object id='control' form='second'></object>")]
    [TestCase("<output id='control' form='second'></output>")]
    [TestCase("<select id='control' form='second'></select>")]
    [TestCase("<textarea id='control' form='second'></textarea>")]
    [TestCase("<keygen id='control' form='second'>")]
    public void EveryListedElementReadsTheSameRule(string markup)
    {
        using var fixture = DomTestFixture.Create($"""<form id="first">{markup}</form><form id="second"></form>""");

        fixture.Text("document.getElementById('control').form.id").Should().Be("second");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#dom-label-form — a <c>label</c> is not a
    /// form-associated element: its <c>form</c> is its labeled control's owner, so a label inside one form
    /// labelling a control associated with another answers the other one, and a label labelling nothing
    /// answers null.
    /// </summary>
    [Test]
    public void ALabelBorrowsItsLabeledControlsOwner()
    {
        using var fixture = DomTestFixture.Create(
            """
            <form id="first">
              <label id="explicit" for="control">caption</label>
              <label id="wrapping"><input id="inside"></label>
              <label id="empty">caption</label>
              <label id="dangling" for="not-labelable">caption</label>
              <div id="not-labelable"></div>
            </form>
            <form id="second"><input id="control" form="second"></form>
            """);

        fixture.Text("document.getElementById('explicit').form.id").Should().Be("second");
        fixture.Text("document.getElementById('wrapping').form.id").Should().Be("first");
        fixture.Text("(document.getElementById('empty').form || {}).id ?? null").Should().BeNull();
        fixture.Text("(document.getElementById('dangling').form || {}).id ?? null").Should().BeNull();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#the-legend-element — a legend's <c>form</c>
    /// is its <i>parent</i> fieldset's, so it follows the fieldset's explicit association and is null when
    /// the legend's parent is not a fieldset at all.
    /// </summary>
    [Test]
    public void ALegendBorrowsItsParentFieldsetsOwner()
    {
        using var fixture = DomTestFixture.Create(
            """
            <form id="first">
              <fieldset form="second"><legend id="reassociated">caption</legend></fieldset>
              <fieldset><div><legend id="grandchild">caption</legend></div></fieldset>
              <legend id="orphan">caption</legend>
            </form>
            <form id="second"></form>
            """);

        fixture.Text("document.getElementById('reassociated').form.id").Should().Be("second");
        fixture.Text("(document.getElementById('grandchild').form || {}).id ?? null").Should().BeNull();
        fixture.Text("(document.getElementById('orphan').form || {}).id ?? null").Should().BeNull();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#dom-option-form — an option's <c>form</c> is
    /// its nearest ancestor select's owner, which is what answers for one inside an <c>optgroup</c> and
    /// answers null for one in no select.
    /// </summary>
    [Test]
    public void AnOptionBorrowsItsSelectsOwner()
    {
        using var fixture = DomTestFixture.Create(
            """
            <form id="first">
              <select form="second"><optgroup><option id="grouped">a</option></optgroup></select>
              <select><option id="contained">b</option></select>
              <datalist><option id="loose">c</option></datalist>
            </form>
            <form id="second"></form>
            """);

        fixture.Text("document.getElementById('grouped').form.id").Should().Be("second");
        fixture.Text("document.getElementById('contained').form.id").Should().Be("first");
        fixture.Text("(document.getElementById('loose').form || {}).id ?? null").Should().BeNull();
    }

    /// <summary>
    /// The entry list is built from the same rule, so the control the issue reported contributes to
    /// <c>second</c> and to nothing else — and tree order across the association is still the tree's.
    /// </summary>
    [Test]
    public async Task TheEntryListFollowsTheFormOwner()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id="first">
              <input name="stays" value="first">
              <input name="moves" value="second" form="second">
            </form>
            <form id="second"><input name="own" value="second"></form>
            """);

        (await page.EvaluateAsync<string>("JSON.stringify([...new FormData(first)])"))
            .Should().Be(JsonSerializer.Serialize(new[] { new[] { "stays", "first" } }));
        (await page.EvaluateAsync<string>("JSON.stringify([...new FormData(second)])"))
            .Should().Be(JsonSerializer.Serialize(new[] { new[] { "moves", "second" }, new[] { "own", "second" } }));
    }

    /// <summary>
    /// <c>new FormData(form, submitter)</c> and <c>form.requestSubmit(submitter)</c> both check that the
    /// submitter's form owner <i>is</i> the form, so a button reassociated out of the form containing it is a
    /// <c>NotFoundError</c> there and accepted by the form it names.
    /// </summary>
    [Test]
    public async Task ASubmitterIsOwnedByTheFormItNamesAndNotByTheOneAroundIt()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id="first"><button id="submitter" name="action" value="go" form="second">submit</button></form>
            <form id="second"><input name="own" value="second"></form>
            """);
        await page.EvaluateAsync("second.onsubmit = e => e.preventDefault(); first.onsubmit = e => e.preventDefault();");

        // Tree order is the tree's and not the form's: the reassociated button sits above `second` in the
        // document, so its entry comes first.
        (await page.EvaluateAsync<string>("JSON.stringify([...new FormData(second, submitter)])"))
            .Should().Be(JsonSerializer.Serialize(new[] { new[] { "action", "go" }, new[] { "own", "second" } }));
        (await page.EvaluateAsync<string>(Attempt("new FormData(first, submitter)"))).Should().Be("NotFoundError|true");
        (await page.EvaluateAsync<string>(Attempt("first.requestSubmit(submitter)"))).Should().Be("NotFoundError|true");
        (await page.EvaluateAsync<string>(Attempt("second.requestSubmit(submitter)"))).Should().Be("no error");
    }

    /// <summary>
    /// An image button's activation submits the form it owns rather than the one it sits in, and the selected
    /// coordinate goes with it — the entries land in that form's entry list and nowhere else.
    /// </summary>
    [Test]
    public async Task AnImageSubmittersCoordinateGoesToTheFormItIsAssociatedWith()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<form id=first><input id=image type=image name=go form=second src='data:image/png;base64,"
            + Convert.ToBase64String(ImageBytes.Png(64, 32)) + "'></form><form id=second></form>");
        await page.EvaluateAsync(
            """
            window.submitted = [];
            first.onsubmit = e => { submitted.push('first'); e.preventDefault(); };
            second.onsubmit = e => { submitted.push('second'); e.preventDefault(); };
            """);

        (await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var input = (IHtmlInputElement) runtime.Document!.GetElementById("image")!;
            var box = runtime.Layout.Current().ClientBoxOf(input)!.Value;
            InputDispatcher.DispatchMouse(runtime, new MouseInput(
                MouseInputKind.Released, box.X + 7.75, box.Y + 5.5, 0, 0, 1, EventModifiers.None, 0, 0));
            return true;
        })).Should().BeTrue();

        (await page.EvaluateAsync<string>("submitted.join(',')")).Should().Be("second");
        (await page.EvaluateAsync<string>("JSON.stringify([...new FormData(second, image)])"))
            .Should().Be(JsonSerializer.Serialize(new[] { new[] { "go.x", "7" }, new[] { "go.y", "5" } }));
        (await page.EvaluateAsync<string>(Attempt("new FormData(first, image)"))).Should().Be("NotFoundError|true");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// Interactive validation reads the same inventory the entry list does, so a required control associated
    /// into a form refuses that form's submission and leaves the form it sits in submittable.
    /// </summary>
    [Test]
    public async Task ValidationJudgesTheControlsTheFormOwns()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id="first"><input name="missing" required form="second"></form>
            <form id="second"></form>
            """);
        await page.EvaluateAsync(
            """
            window.submitted = [];
            first.onsubmit = e => { submitted.push('first'); e.preventDefault(); };
            second.onsubmit = e => { submitted.push('second'); e.preventDefault(); };
            first.requestSubmit();
            second.requestSubmit();
            """);

        (await page.EvaluateAsync<string>("submitted.join(',')")).Should().Be("first");
    }

    /// <summary>
    /// §4.10.21.2's default button is decided over form ownership in tree order, so <c>:default</c> follows a
    /// submit button out of the form containing it — the same rule
    /// <a href="https://github.com/sebastienros/jint/pull/4023">#4023</a> introduced, now reading one
    /// resolver.
    /// </summary>
    [Test]
    public async Task TheDefaultButtonIsDecidedOverFormOwnership()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <button id="outside" form="second">submit</button>
            <form id="first"><button id="reassociated" form="second">submit</button><button id="own">submit</button></form>
            <form id="second"></form>
            """);

        // `second`'s own default button is the one above it in the document, so the button inside `first`
        // that names `second` is not a default button of anything; `first`'s is the one it still owns.
        (await page.EvaluateAsync<bool>("reassociated.matches(':default')")).Should().BeFalse();
        (await page.EvaluateAsync<string>("[...document.querySelectorAll(':default')].map(x => x.id).join(',')"))
            .Should().Be("outside,own");
    }

    /// <summary>
    /// A radio button group is "the same tree, the same form owner, the same name", so an explicit
    /// association takes a radio out of the group inside the form and puts one from outside into it.
    /// </summary>
    [Test]
    public async Task ARadioButtonGroupIsDecidedOverFormOwnership()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id="outside" type="radio" name="pick" form="second">
            <form id="first"><input id="inside" type="radio" name="pick" form="second"></form>
            <form id="second"><input id="own" type="radio" name="pick"></form>
            """);
        await page.EvaluateAsync("outside.click(); inside.click();");

        // One group: checking the second member cleared the first, and the member the same name puts in the
        // form is cleared too.
        (await page.EvaluateAsync<string>("[outside.checked, inside.checked, own.checked].join(',')"))
            .Should().Be("false,true,false");
    }

    /// <summary>Reads an expression's DOMException name, or "no error" when it completes.</summary>
    private static string Attempt(string expression)
        => "(() => { try { " + expression + "; return 'no error'; } "
            + "catch (e) { return [e.name, e instanceof DOMException].join('|'); } })()";
}
