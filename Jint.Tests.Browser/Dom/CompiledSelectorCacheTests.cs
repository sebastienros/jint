using Jint.Browser.Dom;

namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <c>matches</c>, <c>closest</c> and the query methods compile a selector once per realm and reuse it
/// (#4619), without remembering a failure or growing without bound.
/// </summary>
public sealed class CompiledSelectorCacheTests
{
    private const string Page = "<ul><li id='a' class='x'>a</li><li id='b'>b</li></ul>";

    [Test]
    public void ARepeatedSelectorIsCompiledOnceAndAnswersTheSameAsAFreshOne()
    {
        using var fixture = DomTestFixture.Create(Page);
        var realm = DomRealm.Of(fixture.Engine);

        fixture.Text(
            """
            (() => {
              var a = document.getElementById('a'), b = document.getElementById('b');
              return [a.matches('li.x'), a.matches('li.x'), b.matches('li.x'), b.closest('ul') === a.closest('ul')].join();
            })()
            """).Should().Be("true,true,false,true");

        realm.CompiledSelectors.Should().NotBeNull().And.ContainKey("li.x");
        var compiled = realm.CompiledSelectors!["li.x"];

        fixture.Bool("document.getElementById('a').matches('li.x')").Should().BeTrue();
        realm.CompiledSelectors!["li.x"].Should().BeSameAs(compiled, "the second call reuses the program the first compiled");
    }

    [Test]
    public void ASelectorThatDoesNotParseThrowsEveryTime()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            (() => {
              var a = document.getElementById('a'), seen = [];
              for (var i = 0; i < 2; i++) {
                try { a.matches('li['); seen.push('matched'); }
                catch (e) { seen.push(e.name); }
              }
              return seen.join();
            })()
            """).Should().Be("SyntaxError,SyntaxError");

        DomRealm.Of(fixture.Engine).CompiledSelectors?.ContainsKey("li[").Should().NotBe(true);
    }

    [Test]
    public void TheCacheIsBoundedAndStaysCorrectPastTheBound()
    {
        using var fixture = DomTestFixture.Create(Page);
        var distinct = DomSelectors.MaxCompiledSelectors + 100;

        fixture.Number(
            $$"""
            (() => {
              var a = document.getElementById('a'), hits = 0;
              for (var i = 0; i < {{distinct}}; i++) {
                if (a.matches('li:not(.never' + i + ')')) hits++;
                if (a.matches('#nope' + i)) hits += 1000;
              }
              return hits;
            })()
            """).Should().Be(distinct);

        DomRealm.Of(fixture.Engine).CompiledSelectors!.Count.Should().BeLessThanOrEqualTo(DomSelectors.MaxCompiledSelectors);
    }

    [Test]
    public void ALongSelectorIsAnsweredButNotRemembered()
    {
        using var fixture = DomTestFixture.Create(Page);
        var selector = "li" + string.Concat(Enumerable.Repeat(":not(.z)", DomSelectors.MaxCompiledSelectorLength / 8 + 1));

        fixture.Bool($"document.getElementById('a').matches('{selector}')").Should().BeTrue();
        DomRealm.Of(fixture.Engine).CompiledSelectors?.ContainsKey(selector).Should().NotBe(true);
    }
}
