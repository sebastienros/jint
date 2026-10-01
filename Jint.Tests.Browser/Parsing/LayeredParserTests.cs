using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public sealed class LayeredParserTests
{
    [Test]
    public async Task AnIgnoredLateImportDoesNotForceThePrecedingStyleBody()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<style>p { @scope (.unused) {} } @import '/ignored.css';</style><p>ready</p>")
            .Map("/ignored.css", _ => LoopbackResponse.Css("p { color:red }")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        fixture.Page.Errors.Should().BeEmpty();
        fixture.Server.Received.Should().NotContain(request => request.Path == "/ignored.css");
        (await fixture.Page.EvaluateAsync<string>("document.querySelector('p').textContent")).Should().Be("ready");
    }

    [Test]
    public async Task ImportedSheetsLoadWithoutDemandingUnrelatedRuleGrammars()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<style id=s>@import '/child.css';</style><p id=target>ready</p>")
            .Map("/child.css", _ => LoopbackResponse.Css("@scope (.unused) { p { color:red } }")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        fixture.Page.Errors.Should().BeEmpty();
        fixture.Server.Received.Count(request => request.Path == "/child.css").Should().Be(1);
        (await fixture.Page.EvaluateAsync<bool>(
            "document.querySelector('#target').textContent==='ready' && s.sheet.cssRules[0].styleSheet!==null"))
            .Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>(
            "(()=>{try{return String(s.sheet.cssRules[0].styleSheet.cssRules.length)}catch(e){return e.name}})()"))
            .Should().Be("1");
    }

    [TestCase("@media print")]
    [TestCase("@supports (unknown-property:value)")]
    public async Task StyleQueriesSkipInactiveBodiesButCssomCanDemandThem(string condition)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<style id=s>" + condition + " { @scope (.unused) {} } p { color:blue }</style><p id=target>ready</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(target).color")).Should().Be("rgb(0, 0, 255)");
        (await fixture.Page.EvaluateAsync<bool>("s.sheet.cssRules.length===2")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>(
            "(()=>{try{return String(s.sheet.cssRules[0].cssRules.length)}catch(e){return e.name}})()"))
            .Should().Be("1");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ChangingMediaActivatesDeferredParsingAndReplacementDiscardsTheOldSource()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<style id=s>@media print { @scope (.unused) {} } p { color:blue }</style><p id=target>ready</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(target).color")).Should().Be("rgb(0, 0, 255)");
        (await fixture.Page.EvaluateAsync<string>(
            "s.sheet.cssRules[0].media.mediaText='screen';" +
            "(()=>{try{return getComputedStyle(target).color}catch(e){return e.name}})()"))
            .Should().Be("rgb(0, 0, 255)");
        (await fixture.Page.EvaluateAsync<string>(
            "s.textContent='p { color:green }'; getComputedStyle(target).color")).Should().Be("rgb(0, 128, 0)");
    }
}
