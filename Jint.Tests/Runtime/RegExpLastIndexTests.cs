#nullable enable

namespace Jint.Tests.Runtime;

public class RegExpLastIndexTests
{
    [TestCase("a", "")]
    [TestCase("a", "g")]
    [TestCase("a", "y")]
    [TestCase("(?:a(b)c)+", "")]
    [TestCase("(?:a(b)c)+", "g")]
    [TestCase("(?:a(b)c)+", "y")]
    public void TestUsesThePatternRecompiledDuringLastIndexCoercion(string pattern, string flags)
    {
        using var engine = new Engine();

        engine.Evaluate($$"""
            const r = new RegExp('{{pattern}}', '{{flags}}');
            r.lastIndex = { valueOf() { r.compile('b', 'g'); return 0; } };
            r.test('b');
            """).Should().Be(true);
    }

    [TestCase("(?:a(b)c)+", "g", "abc")]
    [TestCase("(?:a(b)c)+", "y", "abc")]
    [TestCase("(?:a(b)c)+", "g", "z")]
    [TestCase("(?:a(b)c)+", "y", "z")]
    [TestCase("(?:)", "g", "a")]
    [TestCase("(?:)", "y", "a")]
    public void SearchPreservesTheThrowingLastIndexWrite(string pattern, string flags, string subject)
    {
        using var engine = new Engine();

        engine.Evaluate($$"""
            (() => {
                const r = new RegExp('{{pattern}}', '{{flags}}');
                Object.defineProperty(r, 'lastIndex', { writable: false });
                try { r[Symbol.search]('{{subject}}'); } catch (e) { return e.name; }
                return 'did not throw';
            })()
            """).Should().Be("TypeError");
    }

    [TestCase("g", 0, "[true,0]")]
    [TestCase("y", 0, "[true,0]")]
    [TestCase("g", 1, "[true,1]")]
    [TestCase("y", 1, "[true,1]")]
    [TestCase("g", 9, "[false,0]")]
    [TestCase("y", 9, "[false,0]")]
    public void EmptyPatternTestWritesTheCoercedLastIndex(string flags, int start, string expected)
    {
        using var engine = new Engine();

        engine.Evaluate($$"""
            const r = /(?:)/{{flags}};
            r.lastIndex = { valueOf() { return {{start}}; } };
            JSON.stringify([r.test('a'), r.lastIndex]);
            """).Should().Be(expected);
    }

    [TestCase("g")]
    [TestCase("y")]
    public void EmptyPatternTestPreservesTheThrowingLastIndexWrite(string flags)
    {
        using var engine = new Engine();

        engine.Evaluate($$"""
            (() => {
                const r = /(?:)/{{flags}};
                Object.defineProperty(r, 'lastIndex', {
                    value: { valueOf() { return 0; } }, writable: false
                });
                try { r.test('a'); } catch (e) { return e.name; }
                return 'did not throw';
            })()
            """).Should().Be("TypeError");
    }

    [Test]
    public void EmptyPatternExecIncludesMatchIndices()
    {
        using var engine = new Engine();

        engine.Evaluate("JSON.stringify(/(?:)/d.exec('a').indices);")
            .Should().Be("[[0,0]]");
    }
}
