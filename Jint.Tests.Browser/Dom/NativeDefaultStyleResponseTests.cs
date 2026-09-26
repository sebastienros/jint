using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeDefaultStyleResponseTests
{
    [TestCase("night, literal")]
    [TestCase("")]
    public async Task ResponsePreferencePrecedesFirstScriptAndBelongsOnlyToItsDocument(string lastValue)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/child", _ => LoopbackResponse.Html("<script>window.firstPreference=document.preferredStyleSheetSet;</script>")
                .With("Default-Style", "child-first").With("Default-Style", "child-last"))
            .Map("/", _ => LoopbackResponse.Html("""
                <script>window.firstPreference=document.preferredStyleSheetSet;</script>
                <iframe id=network src=/child></iframe>
                <iframe id=blank></iframe>
                <iframe id=inline srcdoc="<script>window.firstPreference=document.preferredStyleSheetSet;</script>"></iframe>
                """).With("Default-Style", "first").With("Default-Style", lastValue)));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("firstPreference")).Should().Be(lastValue);
        (await fixture.Page.EvaluateAsync<string>("frames[0].firstPreference")).Should().Be("child-last");
        (await fixture.Page.EvaluateAsync<string>("blank.contentDocument.preferredStyleSheetSet")).Should().Be("");
        (await fixture.Page.EvaluateAsync<string>("frames[2].firstPreference")).Should().Be("");
        (await fixture.Page.EvaluateAsync<string>("new Document().preferredStyleSheetSet")).Should().Be("");
        fixture.Page.Errors.Should().BeEmpty();
    }
}
