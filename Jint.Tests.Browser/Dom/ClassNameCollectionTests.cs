namespace Jint.Tests.Browser.Dom;

/// <summary>DOM's live lists of elements by class names.</summary>
public sealed class ClassNameCollectionTests
{
    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-getelementsbyclassname - the mode that decides the comparison is
    /// the <i>root's node document's</i>, so the document-rooted and element-rooted spellings answer alike,
    /// and limited quirks is not quirks.
    /// </summary>
    [TestCase("", "BackCompat", "upper,lower")]
    [TestCase("<!doctype html>", "CSS1Compat", "upper")]
    [TestCase("<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>", "CSS1Compat", "upper")]
    public void DocumentAndElementQueriesUseTheDocumentMode(string doctype, string mode, string expected)
    {
        using var fixture = DomTestFixture.Create(doctype + """
            <main id="root"><span id="upper" class="A B"></span><span id="lower" class="a b"></span></main>
            """);

        fixture.Text("document.compatMode").Should().Be(mode);
        fixture.Text("[...document.getElementsByClassName('A B')].map(x => x.id).join(',')").Should().Be(expected);
        fixture.Text("[...document.getElementById('root').getElementsByClassName('A B')].map(x => x.id).join(',')").Should().Be(expected);
    }

    /// <summary>
    /// The fold is ASCII-only and the split is on ASCII whitespace, so a Latin-1 letter keeps its identity in
    /// both modes and NBSP, VT and punctuation are all ordinary class-name characters.
    /// </summary>
    [TestCase("")]
    [TestCase("<!doctype html>")]
    public void ClassMatchingPreservesNonAsciiCharactersAndTokenBoundaries(string doctype)
    {
        using var fixture = DomTestFixture.Create(doctype + "<main id='root'></main>");
        fixture.Execute("""
            var root = document.getElementById('root');
            for (const [id, classes] of [
                ['ascii', 'K'], ['kelvin', '\u212A'], ['upper', '\u00C4'], ['lower', '\u00E4'],
                ['tokens', 'a\tb\nc\fd\re'], ['nbsp', 'a\u00A0b'], ['vertical', 'a\vb'], ['punctuation', '.a#b']
            ]) {
                const child = document.createElement('span');
                child.id = id;
                child.className = classes;
                root.appendChild(child);
            }
            function ids(query) { return [...root.getElementsByClassName(query)].map(x => x.id).join(','); }
            """);

        fixture.Text("""ids('K')""").Should().Be("ascii");
        fixture.Text("""ids('\u212A')""").Should().Be("kelvin");
        fixture.Text("""ids('\u00C4')""").Should().Be("upper");
        fixture.Text("""ids('\u00E4')""").Should().Be("lower");
        fixture.Text("""ids(' \ta b\nc\fd\re a ')""").Should().Be("tokens");
        fixture.Text("""ids('a\u00A0b')""").Should().Be("nbsp");
        fixture.Text("""ids('a\vb')""").Should().Be("vertical");
        fixture.Text("""ids('.a#b')""").Should().Be("punctuation");
        fixture.Text("""ids('') + ids(' \t\n\f\r') + ids('missing')""").Should().BeEmpty();
    }

    /// <summary>
    /// The quirks fold belongs to this one algorithm: <c>classList</c>, <c>getElementById</c> and a
    /// collection's named lookup all stay exact, and the collection the fold produced is still live.
    /// </summary>
    [TestCase("document")]
    [TestCase("document.getElementById('root')")]
    public void QuirksCollectionRemainsLiveWithoutChangingClassListOrIdMatching(string context)
    {
        using var fixture = DomTestFixture.Create("<main id='root'><span id='UPPER' class='a b'></span></main>");
        fixture.Execute($$"""
            var root = document.getElementById('root');
            var first = document.getElementById('UPPER');
            var conversions = 0;
            var items = {{context}}.getElementsByClassName({ toString() { conversions++; return 'A a B'; } });
            """);
        fixture.Number("items.length").Should().Be(1);
        fixture.Bool("first.classList.contains('A')").Should().BeFalse();
        fixture.Bool("document.getElementById('upper') === null && items.namedItem('upper') === null").Should().BeTrue();
        fixture.Execute("first.classList.remove('b')");
        fixture.Number("items.length").Should().Be(0);
        fixture.Execute("first.setAttribute('class', 'A B')");
        fixture.Bool("items.item(0) === first && items.namedItem('UPPER') === first").Should().BeTrue();
        fixture.Execute("root.innerHTML += '<span id=second class=\"a b\"></span>'");
        fixture.Text("[...items].map(x => x.id).join(',')").Should().Be("UPPER,second");
        fixture.Execute("root.lastElementChild.remove()");
        fixture.Number("items.length").Should().Be(1);
        fixture.Number("conversions").Should().Be(1);
    }

    /// <summary>
    /// The mode is read inside the filter, so a saved collection over an adopted root answers its
    /// <i>current</i> node document's mode rather than the one it was created under.
    /// </summary>
    [Test]
    public void SavedElementCollectionUsesItsCurrentNodeDocumentAfterAdoption()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var root = document.createElement('div');
            root.className = 'A';
            root.innerHTML = '<span class=a></span>';
            var items = root.getElementsByClassName('A');
            var standards = document.implementation.createHTMLDocument('standards');
            """);
        fixture.Number("items.length").Should().Be(1);
        fixture.Execute("standards.adoptNode(root)");
        fixture.Text("root.ownerDocument.compatMode").Should().Be("CSS1Compat");
        fixture.Number("items.length").Should().Be(0);
        fixture.Execute("document.adoptNode(root)");
        fixture.Number("items.length").Should().Be(1);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-getelementsbyclassname matches "descendant elements that have
    /// all their classes in classes", and an element's
    /// <a href="https://dom.spec.whatwg.org/#concept-class">classes</a> are the token set of
    /// <c>classList</c>, whose associated attribute DOM §7.1 reads by <b>getting an attribute value given
    /// null namespace and the local name <c>class</c></b>. That is not
    /// <a href="https://dom.spec.whatwg.org/#dom-element-getattribute"><c>getAttribute("class")</c></a>,
    /// which matches a <i>qualified</i> name — so an attribute placed in a namespace under the qualified
    /// name <c>class</c> is reachable through <c>getAttribute</c> and is still not the class content
    /// attribute, exactly as <c>className</c> and <c>classList</c> already say it is not.
    /// </summary>
    [Test]
    public void NamespacedAttributeSpelledClassIsNotOneOfTheElementsClasses()
    {
        using var fixture = DomTestFixture.Create(
            "<!doctype html><body><span id='namespaced'></span><span id='own'></span></body>");
        fixture.Execute("""
            document.getElementById('namespaced').setAttributeNS('http://example.test/ns', 'class', 'match');
            document.getElementById('own').setAttribute('class', 'match');
            """);

        // It is an attribute, and the qualified-name lookup finds it.
        fixture.Text("document.getElementById('namespaced').getAttribute('class')").Should().Be("match");

        // It is not the class content attribute, which is what every other reader of the element's classes
        // already agrees about.
        fixture.Text("document.getElementById('namespaced').className").Should().BeEmpty();
        fixture.Number("document.getElementById('namespaced').classList.length").Should().Be(0);

        // ... so the collection has to agree with them rather than with getAttribute.
        fixture.Text("[...document.getElementsByClassName('match')].map(x => x.id).join(',')").Should().Be("own");
    }

    /// <summary>
    /// The element walk behind a live collection keeps a stack of child indices whose first levels live
    /// inside the walker and which spills to the heap below that, so a document deeper than the inline part
    /// is the case that exercises the spill — and, on the way back up, the case that reads a level recorded
    /// before it. Matches at every depth, and in tree order, is what says both halves of that stack agree.
    /// </summary>
    [Test]
    public void DeeplyNestedDocumentIsWalkedInTreeOrderPastTheInlineIndexStack()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><main id='root'></main></body>");
        fixture.Execute("""
            var depth = 40;
            var parent = document.getElementById('root');
            for (var i = 0; i < depth; i++) {
                var child = document.createElement('div');
                child.id = 'd' + i;
                child.className = 'match';
                // A sibling at every level, so unwinding has somewhere to go rather than running straight
                // back to the root.
                var sibling = document.createElement('span');
                sibling.id = 's' + i;
                sibling.className = 'match';
                parent.appendChild(child);
                parent.appendChild(sibling);
                parent = child;
            }
            var items = document.getElementsByClassName('match');
            // Pre-order: down the whole chain of divs first, then every span on the way back up, each one
            // reached from the level its own index was pushed at.
            var expected = [];
            for (var i = 0; i < depth; i++) { expected.push('d' + i); }
            for (var i = depth - 1; i >= 0; i--) { expected.push('s' + i); }
            """);

        fixture.Number("items.length").Should().Be(80);
        fixture.Bool("[...items].map(x => x.id).join(',') === expected.join(',')").Should().BeTrue();
        fixture.Bool("items[39].id === 'd39' && items.item(40).id === 's39' && items[79].id === 's0'").Should().BeTrue();
        fixture.Bool("items.namedItem('s39') === document.getElementById('s39')").Should().BeTrue();
        fixture.Bool("items[80] === undefined").Should().BeTrue();
    }

    /// <summary>An XML document is never in quirks mode, so its comparison is exact from both roots.</summary>
    [Test]
    public void XmlDocumentRemainsCaseSensitive()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var xml = document.implementation.createDocument(null, 'root');
            for (const name of ['a', 'A']) {
                const item = xml.createElement('item');
                item.setAttribute('class', name);
                xml.documentElement.appendChild(item);
            }
            """);
        fixture.Number("xml.getElementsByClassName('A').length").Should().Be(1);
        fixture.Number("xml.documentElement.getElementsByClassName('A').length").Should().Be(1);
    }

    [TestCase("document")]
    [TestCase("document.getElementById('root')")]
    public void SavedCollectionObservesInsertionRemovalAndClassChanges(string context)
    {
        using var fixture = DomTestFixture.Create("<main id='root'><span id='first' class='match'></span></main>");
        fixture.Execute($$"""
            var root = document.getElementById('root');
            var first = document.getElementById('first');
            var items = {{context}}.getElementsByClassName('match');
            var second = document.createElement('span');
            second.id = 'second';
            second.className = 'match';
            root.appendChild(second);
            """);

        fixture.Number("items.length").Should().Be(2);
        fixture.Bool("items[1] === second && items.namedItem('second') === second").Should().BeTrue();
        fixture.Execute("first.className = 'other'");
        fixture.Text("[...items].map(x => x.id).join(',')").Should().Be("second");
        fixture.Execute("first.classList.add('match')");
        fixture.Number("items.length").Should().Be(2);
        fixture.Execute("root.removeChild(second)");
        fixture.Number("items.length").Should().Be(1);
        fixture.Bool("items[0] === first && items.namedItem('second') === null").Should().BeTrue();
    }

    [Test]
    public void DetachedRootExcludesItselfAndConvertsClassNamesOnce()
    {
        using var fixture = DomTestFixture.Create("<body></body>");
        fixture.Execute("""
            var root = document.createElement('div');
            root.className = 'a b';
            var conversions = 0;
            var items = root.getElementsByClassName({ toString() { conversions++; return 'a b'; } });
            var child = document.createElement('span');
            child.className = 'a';
            root.appendChild(child);
            """);

        fixture.Number("items.length").Should().Be(0);
        fixture.Execute("child.classList.add('b')");
        fixture.Number("items.length").Should().Be(1);
        fixture.Bool("items.item(0) === child").Should().BeTrue();
        fixture.Number("conversions").Should().Be(1);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-getelementsbyclassname - the comparison is ASCII case-insensitive
    /// while the root's node document is in quirks mode, and exact otherwise.
    /// </summary>
    [Test]
    public void QuirksModeFoldsAsciiCaseAndNothingElse()
    {
        // No doctype, so compatMode is "BackCompat".
        using var fixture = DomTestFixture.Create(
            "<html class='a A'><body class='a a'><span id='ascii' class='K'></span>"
            + "<span id='kelvin' class='\u212A'></span></body></html>");

        fixture.Text("document.compatMode").Should().Be("BackCompat");
        fixture.Text("[...document.getElementsByClassName('A a')].map(x => x.localName).join(',')")
            .Should().Be("html,body");

        // U+212A KELVIN SIGN is outside ASCII, so it keeps its own identity even in quirks mode - which is
        // what OrdinalIgnoreCase would not have done.
        fixture.Text("[...document.getElementsByClassName('K')].map(x => x.id).join(',')").Should().Be("ascii");
        fixture.Text("[...document.getElementsByClassName('\u212A')].map(x => x.id).join(',')").Should().Be("kelvin");
    }

    [Test]
    public void StandardsModeComparesExactly()
    {
        using var fixture = DomTestFixture.Create(
            "<!doctype html><html class='a A'><body class='a a'></body></html>");

        fixture.Text("document.compatMode").Should().Be("CSS1Compat");
        fixture.Text("[...document.getElementsByClassName('A a')].map(x => x.localName).join(',')")
            .Should().Be("html");
    }

    /// <summary>
    /// "If classes is the empty set, return an empty HTMLCollection" - and the split is on ASCII whitespace,
    /// so a run of it is one separator rather than a set of empty tokens.
    /// </summary>
    [Test]
    public void WhitespaceOnlyClassNamesMatchNothingAndRunsOfItSeparate()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><p class='a  b'></p></body>");

        fixture.Number("document.getElementsByClassName('').length").Should().Be(0);
        fixture.Number("document.getElementsByClassName('   ').length").Should().Be(0);
        fixture.Number("document.getElementsByClassName('a\\t\\n\\f\\r b').length").Should().Be(1);
    }
    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-getelementsbyclassname matches an element whose
    /// <a href="https://dom.spec.whatwg.org/#concept-class">classes</a> — the <b>tokens</b> of its class
    /// attribute — contain the candidate, never an element whose attribute merely <i>contains</i> the
    /// candidate's characters. The six shapes below are the ones any search over the whole attribute value
    /// has to reject: the candidate at the start of a longer token, at its end, inside it, doubled, and
    /// found once as part of a token before or after it is found as a whole one.
    /// </summary>
    [TestCase("")]
    [TestCase("<!doctype html>")]
    public void ACandidateFoundOnlyInsideADeclaredTokenIsNotOneOfTheElementsClasses(string doctype)
    {
        using var fixture = DomTestFixture.Create(doctype + "<main id='root'></main>");
        fixture.Execute("""
            var root = document.getElementById('root');
            for (const [id, classes] of [
                ['prefix', 'foobar'], ['suffix', 'xfoo'], ['inside', 'xfooy'], ['doubled', 'foofoo'],
                ['after', 'xfoo foo'], ['before', 'foo foobar'], ['exact', 'foo']
            ]) {
                const child = document.createElement('span');
                child.id = id;
                child.setAttribute('class', classes);
                root.appendChild(child);
            }
            function ids(query) { return [...root.getElementsByClassName(query)].map(x => x.id).join(','); }
            """);

        // A rejected occurrence does not end the search: 'after' holds one inside xfoo and then one of its
        // own, and 'before' holds its own and then one inside foobar.
        fixture.Text("""ids('foo')""").Should().Be("after,before,exact");
        fixture.Text("""ids('foobar')""").Should().Be("prefix,before");
        fixture.Text("""ids('xfoo')""").Should().Be("suffix,after");

        // Nothing that is only a part of a token is a class, at either end of one or inside it.
        fixture.Text("""ids('bar') + ids('oo') + ids('ooy')""").Should().BeEmpty();
    }

    /// <summary>
    /// The candidate is a class wherever it stands in the attribute — first, in the middle, last, or alone
    /// between runs of any of https://infra.spec.whatwg.org/#ascii-whitespace's five characters. "Elements
    /// that have <b>all</b> their classes in classes" is the other half: one missing candidate rejects the
    /// element however many of the rest it has.
    /// </summary>
    [TestCase("")]
    [TestCase("<!doctype html>")]
    public void EveryCandidateMustBeAWholeTokenAndAllOfThemMustBePresent(string doctype)
    {
        using var fixture = DomTestFixture.Create(doctype + "<main id='root'></main>");
        fixture.Execute("""
            var root = document.getElementById('root');
            for (const [id, classes] of [
                ['first', 'target alpha beta'], ['middle', 'alpha target beta'], ['last', 'alpha beta target'],
                ['padded', ' \t\n\f\r target \t\n\f\r '], ['runs', 'alpha   target    beta'],
                ['others', 'alpha beta'], ['blank', '']
            ]) {
                const child = document.createElement('span');
                child.id = id;
                child.setAttribute('class', classes);
                root.appendChild(child);
            }
            // The element with no class content attribute at all, which is the case the read short-circuits.
            root.appendChild(document.createElement('span')).id = 'absent';
            function ids(query) { return [...root.getElementsByClassName(query)].map(x => x.id).join(','); }
            """);

        fixture.Text("""ids('target')""").Should().Be("first,middle,last,padded,runs");
        fixture.Text("""ids('alpha')""").Should().Be("first,middle,last,runs,others");
        fixture.Text("""ids('beta target alpha')""").Should().Be("first,middle,last,runs");

        // One candidate missing rejects the element, whichever of them it is.
        fixture.Text("""ids('target missing')""").Should().BeEmpty();
        fixture.Text("""ids('missing target')""").Should().BeEmpty();
        fixture.Text("""ids('alpha beta target')""").Should().Be("first,middle,last,runs");

        // The element with an empty class attribute and the one with no class attribute at all are both
        // walked and neither is ever matched, which is what the exact results above say by omitting them;
        // the empty set of candidates is the other half of the same case.
        fixture.Number("root.children.length").Should().Be(8);
        fixture.Number("root.getElementsByClassName('').length").Should().Be(0);
    }

    /// <summary>
    /// The quirks comparison is https://infra.spec.whatwg.org/#ascii-case-insensitive and nothing wider, in
    /// both directions: U+0131 DOTLESS I and U+212A KELVIN SIGN are what <c>OrdinalIgnoreCase</c> folds onto
    /// <c>I</c> and <c>k</c>, and neither is a match here whether it is the declared token or the candidate.
    /// </summary>
    [Test]
    public void QuirksFoldStaysInsideAsciiInBothDirectionsAndAcrossAWholeAttribute()
    {
        // No doctype, so compatMode is "BackCompat" and the comparison folds ASCII case.
        using var fixture = DomTestFixture.Create("<main id='root'></main>");
        fixture.Execute("""
            var root = document.getElementById('root');
            for (const [id, classes] of [
                ['dotless', '\u0131'], ['capital', 'I'], ['ascii', 'k'], ['kelvin', '\u212A'],
                ['framework', 'BtN bTn-PRIMARY Btn-LG'], ['partial', 'BTN-PRIMARY-LG']
            ]) {
                const child = document.createElement('span');
                child.id = id;
                child.setAttribute('class', classes);
                root.appendChild(child);
            }
            function ids(query) { return [...root.getElementsByClassName(query)].map(x => x.id).join(','); }
            """);

        fixture.Text("document.compatMode").Should().Be("BackCompat");

        // ASCII folds, in one token and across several.
        fixture.Text("""ids('K')""").Should().Be("ascii");
        fixture.Text("""ids('i')""").Should().Be("capital");
        fixture.Text("""ids('btn btn-primary btn-lg')""").Should().Be("framework");
        fixture.Text("""ids('BTN-LG btn')""").Should().Be("framework");

        // Nothing outside it does, in either role.
        fixture.Text("""ids('\u0131')""").Should().Be("dotless");
        fixture.Text("""ids('\u212A')""").Should().Be("kelvin");

        // And a folded comparison is still a comparison of whole tokens.
        fixture.Text("""ids('btn-primary')""").Should().Be("framework");
        fixture.Text("""ids('primary') + ids('btn-primary-l')""").Should().BeEmpty();
    }
}
