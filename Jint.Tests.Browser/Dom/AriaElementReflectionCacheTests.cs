namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// What the frozen array an ARIA element-reflecting getter answers does across a <i>set</i> (#4531).
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes">HTML's
/// reflection of <c>FrozenArray&lt;T&gt;?</c></a> keeps the cache in the <b>getter</b> and nowhere else: the getter
/// recomputes the attr-associated elements, returns the cached array object when the contents are equal to the
/// cached list, and otherwise builds, caches and returns a new one. The setter never reads or writes the cache.
/// Two things follow, and each is pinned here per member:
/// </para>
/// <list type="bullet">
/// <item>A setter whose IDL conversion throws never reaches the set steps, so the elements and the cache are both
/// as they were and the getter answers the very same object.</item>
/// <item>A set that succeeds changes the answer only if the elements differ. Setting the same elements again
/// is the getter finding equal contents, so it answers the same object too.</item>
/// </list>
/// <para>
/// Every expected value was cross-checked against headless Microsoft Edge 156 (Chromium), which agrees on
/// each row except the ones <c>Jint.Browser/Dom/AGENTS.md</c> records. <c>ariaOwnsElements</c> is not exposed
/// by that Chromium, so its rows rest on the standard alone.
/// </para>
/// </remarks>
public sealed class AriaElementReflectionCacheTests
{
    private const string Page = """
        <!doctype html>
        <html><body>
          <div id="owner"></div>
          <span id="one"></span>
          <span id="two"></span>
          <i id="three"></i>
        </body></html>
        """;

    /// <summary>The seven <c>FrozenArray&lt;Element&gt;?</c> members and the content attribute each reflects.</summary>
    private static readonly (string Member, string Attribute)[] ArrayMembers =
    [
        ("ariaControlsElements", "aria-controls"),
        ("ariaDescribedByElements", "aria-describedby"),
        ("ariaDetailsElements", "aria-details"),
        ("ariaErrorMessageElements", "aria-errormessage"),
        ("ariaFlowToElements", "aria-flowto"),
        ("ariaLabelledByElements", "aria-labelledby"),
        ("ariaOwnsElements", "aria-owns"),
    ];

    private static IEnumerable<TestCaseData> EveryArrayMember()
    {
        foreach (var (member, attribute) in ArrayMembers)
        {
            yield return new TestCaseData(member, attribute).SetArgDisplayNames(member);
        }
    }

    /// <summary>
    /// The refusals a <c>sequence&lt;Element&gt;</c> conversion makes (https://webidl.spec.whatwg.org/#es-sequence), one
    /// per way it can fail: a non-iterable, a primitive, a string, a bare element, an entry that is not an
    /// <c>Element</c> (first, last, <c>null</c>, <c>undefined</c>), an <c>@@iterator</c> that is not callable, an
    /// array-like with no iterator, and an iterator that throws part-way through.
    /// </summary>
    private static IEnumerable<TestCaseData> Refusals()
    {
        yield return Refusal("a non-iterable object", "{}");
        yield return Refusal("a number", "1");
        yield return Refusal("a string", "'nope'");
        yield return Refusal("the empty string", "''");
        yield return Refusal("a bare element", "two");
        yield return Refusal("a non-element entry", "[1]");
        yield return Refusal("a non-element entry after an element", "[three, 1]");
        yield return Refusal("a null entry", "[null]");
        yield return Refusal("an undefined entry", "[undefined]");
        yield return Refusal("an @@iterator that is not callable", "({ [Symbol.iterator]: 1 })");
        yield return Refusal("an array-like without an iterator", "({ length: 1, 0: two })");
        yield return Refusal("an iterator that throws part-way", "(function* () { yield three; throw new Error('mid'); })()");
    }

    private static TestCaseData Refusal(string name, string value) => new TestCaseData(value).SetArgDisplayNames(name);

    /// <summary>
    /// <b>The bug.</b> A set the IDL conversion refuses still dropped the cached array, so the getter answered
    /// a new object afterwards even though the elements, and the content attribute, had not moved.
    /// </summary>
    [TestCaseSource(nameof(Refusals))]
    public void ARefusedSetKeepsTheFrozenArrayAndTheElements(string value)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one'), two = document.getElementById('two'), three = document.getElementById('three');
              owner.ariaLabelledByElements = [one];
              const before = owner.ariaLabelledByElements;
              const attribute = owner.getAttribute('aria-labelledby');

              let threw = false;
              try { owner.ariaLabelledByElements = {{value}}; } catch (e) { threw = true; }

              const after = owner.ariaLabelledByElements;
              return [threw, after === before, owner.getAttribute('aria-labelledby') === attribute, after.map(e => e.id).join()].join('|');
            })()
            """).Should().Be("true|true|true|one");
    }

    /// <summary>The same, member by member: every <c>FrozenArray</c> member shares the setter, and each has its own cache slot.</summary>
    [TestCaseSource(nameof(EveryArrayMember))]
    public void EveryArrayMemberKeepsItsArrayAcrossARefusedSet(string member, string attribute)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one');
              owner.{{member}} = [one];
              const before = owner.{{member}};

              const refused = [];
              for (const value of ['nope', {}, one, [1]]) {
                try { owner.{{member}} = value; refused.push('no throw'); } catch (e) { refused.push(e.constructor.name); }
              }

              return [refused.join(), owner.{{member}} === before, owner.getAttribute('{{attribute}}')].join('|');
            })()
            """).Should().Be("TypeError,TypeError,TypeError,TypeError|true|");
    }

    /// <summary>
    /// A refusal while the answer came from the content attribute's ids rather than from an explicit set: the
    /// attribute still holds its ids afterwards, and the array is the one it was.
    /// </summary>
    [Test]
    public void ARefusedSetKeepsAnArrayThatCameFromIdrefs()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.setAttribute('aria-labelledby', 'one two');
              const before = owner.ariaLabelledByElements;

              let threw = false;
              try { owner.ariaLabelledByElements = 'nope'; } catch (e) { threw = true; }

              return [threw, owner.ariaLabelledByElements === before, owner.getAttribute('aria-labelledby'),
                      owner.ariaLabelledByElements.map(e => e.id).join()].join('|');
            })()
            """).Should().Be("true|true|one two|one,two");
    }

    /// <summary>A refusal leaves "no relationship" as no relationship: still <c>null</c>, still no content attribute.</summary>
    [Test]
    public void ARefusedSetWhereNothingWasSetStaysNull()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              let threw = false;
              try { owner.ariaLabelledByElements = 'nope'; } catch (e) { threw = true; }
              return [threw, owner.ariaLabelledByElements, owner.hasAttribute('aria-labelledby')].join('|');
            })()
            """).Should().Be("true||false");
    }

    /// <summary>Several refusals in a row, and between reads, cannot add up to a change.</summary>
    [Test]
    public void RepeatedRefusalsStillAnswerOneArray()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaLabelledByElements = [document.getElementById('one')];
              const before = owner.ariaLabelledByElements;
              const reads = [];

              for (const value of ['x', {}, 1, [1]]) {
                try { owner.ariaLabelledByElements = value; } catch (e) { }
                reads.push(owner.ariaLabelledByElements === before);
              }

              return reads.join();
            })()
            """).Should().Be("true,true,true,true");
    }

    /// <summary>
    /// "A setter that throws during the IDL conversion never reaches the attribute's set steps": nothing is
    /// written, so there is no attribute mutation for an observer to see. A set that succeeds is the control.
    /// </summary>
    [Test]
    public async Task ARefusedSetWritesNothingForAnObserver()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=owner></div><span id=one></span><span id=two></span>");

        (await page.EvaluateAsync<string>("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaLabelledByElements = [document.getElementById('one')];

              const observer = new MutationObserver(() => { });
              observer.observe(owner, { attributes: true });
              try { owner.ariaLabelledByElements = 'nope'; } catch (e) { }
              const refused = observer.takeRecords().length;

              owner.ariaLabelledByElements = [document.getElementById('two')];
              const accepted = observer.takeRecords().map(r => r.attributeName).join();
              return refused + '|' + accepted;
            })()
            """)).Should().Be("0|aria-labelledby");
    }

    /// <summary>
    /// <b>Setting the same elements again is not a change.</b> The getter finds equal contents and answers the
    /// cached array, whether the new value is a fresh array, any other iterable, or the frozen array the getter
    /// itself last answered.
    /// </summary>
    [TestCase("[one]")]
    [TestCase("new Set([one])")]
    [TestCase("owner.ariaLabelledByElements")]
    [TestCase("(function* () { yield one; })()")]
    public void SettingTheSameElementsAnswersTheSameArray(string value)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one');
              owner.ariaLabelledByElements = [one];
              const before = owner.ariaLabelledByElements;

              owner.ariaLabelledByElements = {{value}};

              return [owner.ariaLabelledByElements === before, owner.ariaLabelledByElements === owner.ariaLabelledByElements,
                      owner.getAttribute('aria-labelledby') === ''].join();
            })()
            """).Should().Be("true,true,true");
    }

    /// <summary>Every <c>FrozenArray</c> member, not only the one the bug was reported against.</summary>
    [TestCaseSource(nameof(EveryArrayMember))]
    public void EveryArrayMemberAnswersTheSameArrayForTheSameElements(string member, string attribute)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one');
              owner.{{member}} = [one];
              const before = owner.{{member}};
              owner.{{member}} = [one];
              return [owner.{{member}} === before, Object.isFrozen(owner.{{member}}), owner.getAttribute('{{attribute}}')].join('|');
            })()
            """).Should().Be("true|true|");
    }

    /// <summary>
    /// Different elements are a different answer: another element, the same elements in another order, a
    /// superset and nothing at all each build a new array, which is itself stable until the next change.
    /// </summary>
    [TestCase("[two]", "two")]
    [TestCase("[two, one]", "two,one")]
    [TestCase("[one, two]", "one,two")]
    [TestCase("[]", "")]
    public void SettingDifferentElementsAnswersANewArray(string value, string expected)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one'), two = document.getElementById('two');
              owner.ariaLabelledByElements = [one];
              const before = owner.ariaLabelledByElements;

              owner.ariaLabelledByElements = {{value}};
              const after = owner.ariaLabelledByElements;

              return [after !== before, after === owner.ariaLabelledByElements, Object.isFrozen(after),
                      Object.isFrozen(before), after.map(e => e.id).join()].join('|');
            })()
            """).Should().Be($"true|true|true|true|{expected}");
    }

    /// <summary>An empty answer is cached like any other: <c>[]</c> set twice answers one empty array.</summary>
    [Test]
    public void AnEmptySetIsCachedLikeAnyOther()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaLabelledByElements = [document.getElementById('one')];
              const full = owner.ariaLabelledByElements;
              owner.ariaLabelledByElements = [];
              const empty = owner.ariaLabelledByElements;
              owner.ariaLabelledByElements = [];
              return [empty !== full, owner.ariaLabelledByElements === empty, empty.length, owner.getAttribute('aria-labelledby')].join('|');
            })()
            """).Should().Be("true|true|0|");
    }

    /// <summary>
    /// <b><c>null</c> removes the relationship.</b> The getter answers <c>null</c> and the content attribute is
    /// gone; <c>undefined</c> converts to <c>null</c> for a nullable type; and the next set after the getter has
    /// seen <c>null</c> builds a new array, because the getter dropped its cache when it answered <c>null</c>.
    /// </summary>
    [TestCase("null")]
    [TestCase("undefined")]
    public void NullRemovesTheRelationshipAndTheNextSetBuildsANewArray(string value)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one');
              owner.ariaLabelledByElements = [one];
              const before = owner.ariaLabelledByElements;

              owner.ariaLabelledByElements = {{value}};
              const removed = [owner.ariaLabelledByElements === null, owner.hasAttribute('aria-labelledby')];

              owner.ariaLabelledByElements = [one];
              return removed.concat([owner.ariaLabelledByElements !== before, owner.getAttribute('aria-labelledby')]).join('|');
            })()
            """).Should().Be("true|false|true|");
    }

    /// <summary>
    /// A refusal after <c>null</c> is still a refusal: it leaves the removed relationship removed rather than
    /// resurrecting or clearing anything.
    /// </summary>
    [Test]
    public void ARefusedSetAfterNullLeavesTheRelationshipRemoved()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.ariaLabelledByElements = [document.getElementById('one')];
              owner.ariaLabelledByElements = null;

              let threw = false;
              try { owner.ariaLabelledByElements = 'nope'; } catch (e) { threw = true; }
              return [threw, owner.ariaLabelledByElements, owner.hasAttribute('aria-labelledby')].join('|');
            })()
            """).Should().Be("true||false");
    }

    /// <summary>
    /// The setter never touches the cache, not even for <c>null</c>: when the getter has not seen the removal,
    /// setting the same elements again finds them equal to the cached ones. Chromium answers the same object
    /// here too; the row is a guard against the setter growing a cache write.
    /// </summary>
    [Test]
    public void NullThenTheSameElementsWithoutAReadAnswersTheCachedArray()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one');
              owner.ariaLabelledByElements = [one];
              const before = owner.ariaLabelledByElements;
              owner.ariaLabelledByElements = null;
              owner.ariaLabelledByElements = [one];
              return owner.ariaLabelledByElements === before;
            })()
            """).Should().BeTrue();
    }

    /// <summary>Every <c>FrozenArray</c> member, null: the getter answers <c>null</c> and the content attribute goes.</summary>
    [TestCaseSource(nameof(EveryArrayMember))]
    public void NullClearsEveryArrayMember(string member, string attribute)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              owner.{{member}} = [document.getElementById('one')];
              owner.{{member}} = null;
              return [owner.{{member}} === null, owner.hasAttribute('{{attribute}}')].join('|');
            })()
            """).Should().Be("true|false");
    }

    /// <summary>
    /// <c>ariaActiveDescendantElement</c> answers an element rather than an array, so there is no array whose
    /// identity could move; what a refused set owes it is the same as the others owe theirs: the content
    /// attribute and the element it reflects are untouched. (These rows passed before the fix. They are here so
    /// that the two kinds of member are held to the one rule.)
    /// </summary>
    [TestCase("[one]")]
    [TestCase("'nope'")]
    [TestCase("{}")]
    [TestCase("1")]
    public void ARefusedSetKeepsTheSingleElementMember(string value)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text($$"""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one'), two = document.getElementById('two');
              owner.ariaActiveDescendantElement = two;
              const attribute = owner.getAttribute('aria-activedescendant');

              let threw = false;
              try { owner.ariaActiveDescendantElement = {{value}}; } catch (e) { threw = true; }

              return [threw, owner.ariaActiveDescendantElement === two, owner.getAttribute('aria-activedescendant') === attribute].join();
            })()
            """).Should().Be("true,true,true");
    }

    /// <summary>The single-element member after a refusal when it came from an idref rather than a set.</summary>
    [Test]
    public void ARefusedSetKeepsTheSingleElementMemberThatCameFromAnIdref()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              owner.setAttribute('aria-activedescendant', 'one');
              let threw = false;
              try { owner.ariaActiveDescendantElement = 'nope'; } catch (e) { threw = true; }
              return [threw, owner.ariaActiveDescendantElement === document.getElementById('one'),
                      owner.getAttribute('aria-activedescendant')].join('|');
            })()
            """).Should().Be("true|true|one");
    }

    /// <summary>
    /// The single-element member after a successful set: the same element answers the same element, another
    /// element answers that one, and <c>null</c> removes it.
    /// </summary>
    [Test]
    public void TheSingleElementMemberFollowsEachSuccessfulSet()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            (function () {
              const owner = document.getElementById('owner');
              const one = document.getElementById('one'), two = document.getElementById('two');
              owner.ariaActiveDescendantElement = one;
              owner.ariaActiveDescendantElement = one;
              const same = owner.ariaActiveDescendantElement === one;

              owner.ariaActiveDescendantElement = two;
              const other = owner.ariaActiveDescendantElement === two;

              owner.ariaActiveDescendantElement = null;
              return [same, other, owner.ariaActiveDescendantElement === null, owner.hasAttribute('aria-activedescendant')].join();
            })()
            """).Should().Be("true,true,true,false");
    }
}
