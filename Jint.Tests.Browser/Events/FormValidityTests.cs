using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Events;

namespace Jint.Tests.Browser.Events;

public sealed class FormValidityTests
{
    [Test]
    public void DirectValidityCheckIgnoresNovalidateAndRetainsFailureAfterAListenerFixesTheValue()
    {
        using var fixture = DomTestFixture.Create("<form id=f novalidate><input id=t required></form>");
        fixture.Execute(
            """
            globalThis.invalidCount = 0;
            document.getElementById('t').addEventListener('invalid', event => {
              invalidCount++;
              event.preventDefault();
              event.target.value = 'fixed';
            });
            """);
        var realm = DomRealm.Of(fixture.Engine);
        var form = ContentDom.ElementById(fixture.Document, "f")!;
        FormSubmission.CheckValidity(realm, form).Should().BeFalse();
        fixture.Number("invalidCount").Should().Be(1);
        FormSubmission.CheckValidity(realm, form).Should().BeTrue();
    }

    [Test]
    public void InvalidControlsAreSnapshottedBeforeListenersChangeFormOwnership()
    {
        using var fixture = DomTestFixture.Create("<form id=f><input id=a required><input id=b required></form><form id=other></form>");
        fixture.Execute(
            """
            globalThis.invalidOrder = [];
            const a = document.getElementById('a');
            const b = document.getElementById('b');
            a.addEventListener('invalid', () => { invalidOrder.push('a'); b.setAttribute('form', 'other'); });
            b.addEventListener('invalid', () => invalidOrder.push('b'));
            """);
        var realm = DomRealm.Of(fixture.Engine);
        var form = ContentDom.ElementById(fixture.Document, "f")!;
        FormSubmission.CheckValidity(realm, form).Should().BeFalse();
        fixture.Text("invalidOrder.join('|')").Should().Be("a|b");
    }
}
