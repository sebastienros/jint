namespace Jint.Tests.Browser;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-domtokenlist">DOM §7.1</a>'s <c>DOMTokenList</c>: the
/// validation steps, the update steps, <c>replace</c>, <c>supports</c>, <c>item</c>, <c>value</c> and
/// WebIDL's value iterator.
/// </summary>
public sealed class DomTokenListTests
{
    private const string Page = """<!doctype html><html><body><span id="s" class="   a  a b "></span><a id="l" rel="next noopener"></a></body></html>""";

    [Test]
    public void AnEmptyTokenIsASyntaxError()
    {
        using var fixture = DomTestFixture.Create(Page);

        foreach (var call in new[] { "add('')", "add('a', '')", "remove('')", "toggle('')", "replace('', 'a')", "replace('a', '')" })
        {
            fixture.Text("(() => { try { document.body.classList." + call + " } catch (e) { return e.name } return 'no throw' })()")
                .Should().Be("SyntaxError", call);
        }
    }

    [Test]
    public void AnAsciiWhitespaceTokenIsAnInvalidCharacterError()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://infra.spec.whatwg.org/#ascii-whitespace, all five of them, leading and trailing.
        foreach (var token in new[] { "a b", " a", "a ", "\\ta", "a\\t", "\\na", "\\ra", "\\fa" })
        {
            fixture.Text("(() => { try { document.body.classList.add('" + token + "') } catch (e) { return e.name } return 'no throw' })()")
                .Should().Be("InvalidCharacterError", token);
        }

        // A token that is only refused once it has been checked for emptiness: replace asks whether *either*
        // argument is empty before it asks whether either has whitespace.
        fixture.Text("(() => { try { document.body.classList.replace(' ', '') } catch (e) { return e.name } return 'no throw' })()")
            .Should().Be("SyntaxError");
    }

    [Test]
    public void NothingIsAppendedWhenOneOfSeveralTokensIsRejected()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var e = document.body;
            try { e.classList.add('one', 'two three') } catch (ignored) {}
            e.getAttribute('class');
            """)
            .Should().BeNull("every token is validated before any is appended");
    }

    [Test]
    public void TheUpdateStepsRewriteTheAttributeEvenWhenTheTokenSetDidNotMove()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM's update steps are unconditional, so adding a token the element already has still serializes
        // the token set over the attribute — which is what makes the "wrong class after modification" rows
        // of dom/nodes/Element-classlist.html about the *attribute* rather than about the list.
        fixture.Text("var s = document.getElementById('s'); s.classList.add('a'); s.getAttribute('class')")
            .Should().Be("a b");

        fixture.Text("var t = document.getElementById('l'); t.classList.remove('nope'); t.getAttribute('rel')")
            .Should().Be("next noopener");
    }

    [Test]
    public void ReadingAnEmptyListDoesNotGiveTheElementTheAttribute()
    {
        using var fixture = DomTestFixture.Create(Page);

        // Step 1 of the update steps: an absent attribute and an empty token set writes nothing.
        fixture.Text(
            """
            var e = document.createElement('div');
            e.classList.remove('a');
            String(e.hasAttribute('class')) + '|' + e.outerHTML;
            """)
            .Should().Be("false|<div></div>");
    }

    [Test]
    public void ItemAnswersNullPastTheEndInsteadOfThrowing()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("var s = document.getElementById('s'); [s.classList.item(0), s.classList.item(1), s.classList.item(2), s.classList.item(-1)].join('|')")
            .Should().Be("a|b||", "WebIDL's indexed getter never throws for an index out of range");
    }

    [Test]
    public void ToggleTellsAForceThatWasGivenFromOneThatWasNot()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The one AngleSharp cannot express: `bool force = false` reads "not given" and "given as false"
        // alike, so an absent token was added by a call that asked for it to be removed.
        fixture.Text(
            """
            var e = document.createElement('div');
            var log = [];
            log.push(e.classList.toggle('a', false), e.className);
            log.push(e.classList.toggle('a'), e.className);
            log.push(e.classList.toggle('a', true), e.className);
            log.push(e.classList.toggle('a', false), e.className);
            log.push(e.classList.toggle('a', undefined), e.className);
            log.join('|');
            """)
            .Should().Be("false||true|a|true|a|false||true|a");
    }

    [Test]
    public void ReplaceSwapsATokenInPlace()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var e = document.createElement('div');
            e.className = 'a b c';
            var log = [e.classList.replace('b', 'z'), e.getAttribute('class')];
            log.push(e.classList.replace('nope', 'q'), e.getAttribute('class'));
            e.className = 'a b c';
            log.push(e.classList.replace('a', 'c'), e.getAttribute('class'));
            log.join('|');
            """)
            // In place, so the order of the rest survives; and replacing with a token already in the set
            // collapses onto the replaced position rather than appending.
            .Should().Be("true|a z c|false|a z c|true|c b");
    }

    [Test]
    public void SupportsIsATypeErrorForAnAttributeWithNoSupportedTokens()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("(() => { try { document.body.classList.supports('a') } catch (e) { return e.constructor.name } return 'no throw' })()")
            .Should().Be("TypeError");
    }

    [Test]
    public void ValueAndTheStringifierAnswerTheAttributeVerbatim()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var s = document.getElementById('s');
            [s.classList.value, String(s.classList), s.classList.toString(), s.classList.length].join('|');
            """)
            // Neither normalises: the attribute is "   a  a b " and the token set is [a, b].
            .Should().Be("   a  a b |   a  a b |   a  a b |2");

        fixture.Text("String(document.createElement('span').classList)")
            .Should().Be("", "an absent attribute is the empty string, not the string \"null\"");

        fixture.Text(
            """
            var s = document.getElementById('s');
            s.classList.value = ' foo bar foo ';
            [s.classList.value, s.classList.length, s.getAttribute('class')].join('|');
            """)
            .Should().Be(" foo bar foo |2| foo bar foo ");
    }

    [Test]
    public void AssigningToTheListItselfForwardsToValue()
    {
        using var fixture = DomTestFixture.Create(Page);

        // WebIDL's [PutForwards=value], which every one of the seven accessors carries. Without it the
        // member is read-only and the assignment is a TypeError in strict mode.
        fixture.Text(
            """
            'use strict';
            var e = document.createElement('a');
            e.classList = '  p  q ';
            e.relList = 'next';
            [e.getAttribute('class'), e.classList.length, e.getAttribute('rel')].join('|');
            """)
            .Should().Be("  p  q |2|next");
    }

    [Test]
    public void TheValueIteratorMembersAreArrayPrototypesOwn()
    {
        using var fixture = DomTestFixture.Create(Page);

        // WebIDL's iterable<DOMString>: the four members *are* %Array.prototype%'s functions, which is what
        // dom/lists/DOMTokenList-iteration.html asserts with assert_equals rather than by calling them.
        fixture.Text(
            """
            var list = document.getElementById('s').classList;
            [
              list.keys === Array.prototype.keys,
              list.values === Array.prototype.values,
              list.entries === Array.prototype.entries,
              list.forEach === Array.prototype.forEach,
              list[Symbol.iterator] === Array.prototype[Symbol.iterator],
            ].join('|');
            """)
            .Should().Be("true|true|true|true|true");

        fixture.Text(
            """
            var list = document.getElementById('s').classList;
            var seen = [];
            list.forEach(function (value, key, self) { seen.push(key + ':' + value + ':' + (self === list)) });
            [[...list].join(','), [...list.keys()].join(','), [...list.values()].join(','), JSON.stringify([...list.entries()]), seen.join(' ')].join('|');
            """)
            .Should().Be("a,b|0,1|a,b|[[0,\"a\"],[1,\"b\"]]|0:a:true 1:b:true");
    }

    [Test]
    public void EveryTokenListAccessorProjectsOneThatKnowsItsAttribute()
    {
        using var fixture = DomTestFixture.Create(Page);

        // Seven accessors project an ITokenList, and each has to record the attribute it reflects or `value`
        // and the update steps have nothing to read and write.
        fixture.Text(
            """
            function valueOf(tag, attribute, member) {
              var e = document.createElement(tag);
              e.setAttribute(attribute, '  x  y ');
              return e[member].value + ':' + e[member].length;
            }
            [
              valueOf('span', 'class', 'classList'),
              valueOf('a', 'rel', 'relList'),
              valueOf('area', 'rel', 'relList'),
              valueOf('link', 'rel', 'relList'),
              valueOf('iframe', 'sandbox', 'sandbox'),
            ].join('|');
            """)
            .Should().Be("  x  y :2|  x  y :2|  x  y :2|  x  y :2|  x  y :2");
    }
}
