using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Fixtures;

public sealed class BootstrapCollapseTests
{
    [Test]
    public async Task OpeningAnAccordionSectionClosesThePreviousSectionAfterItsTransition()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
            FixtureOrigin.Serve(server);
            server.MapHtml("/", """
                <!doctype html>
                <style>
                .collapse:not(.show) { display:none }
                .collapsing { height:0; overflow:hidden; transition:height .05s ease }
                </style>
                <ul id="menu">
                  <li><figure>
                    <button id="contentButton" data-bs-toggle="collapse" data-bs-target="#content" aria-expanded="false">Content</button>
                    <ul id="content" class="nav-group-items collapse" data-bs-parent="#menu"><li><a id="article">Article</a></li></ul>
                  </figure></li>
                  <li><figure>
                    <button id="designButton" data-bs-toggle="collapse" data-bs-target="#design" aria-expanded="false">Design</button>
                    <ul id="design" class="nav-group-items collapse" data-bs-parent="#menu"><li><a id="placement">Placement</a></li></ul>
                  </figure></li>
                </ul>
                <script src="/vendor/bootstrap-5.3.8/bootstrap.bundle.min.js"></script>
                <script>
                window.transitions=[];
                for(const name of ['show','shown','hide','hidden'])
                  menu.addEventListener(name+'.bs.collapse',e=>transitions.push(name+':'+e.target.id));
                </script>
                """);
        });
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.ClickAsync("#contentButton")).Should().BeTrue();
        (await fixture.Page.WaitForAsync("transitions.includes('shown:content')", TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await fixture.Page.ClickAsync("#designButton")).Should().BeTrue();
        (await fixture.Page.WaitForAsync("transitions.includes('shown:design') && transitions.includes('hidden:content')",
            TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("content.className+'|'+design.className+'|'+contentButton.getAttribute('aria-expanded')+'|'+designButton.getAttribute('aria-expanded')"))
            .Should().Be("nav-group-items collapse|nav-group-items collapse show|false|true");
        fixture.Page.Errors.Should().BeEmpty();
    }
}
