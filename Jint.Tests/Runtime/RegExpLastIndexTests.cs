#nullable enable

namespace Jint.Tests.Runtime;

public class RegExpLastIndexTests
{
    [Theory]
    [InlineData("a", "")]
    [InlineData("a", "g")]
    [InlineData("a", "y")]
    [InlineData("(?:a(b)c)+", "")]
    [InlineData("(?:a(b)c)+", "g")]
    [InlineData("(?:a(b)c)+", "y")]
    public void TestUsesThePatternRecompiledDuringLastIndexCoercion(string pattern, string flags)
    {
        using var engine = new Engine();

        engine.Evaluate($$"""
            const r = new RegExp('{{pattern}}', '{{flags}}');
            r.lastIndex = { valueOf() { r.compile('b', 'g'); return 0; } };
            r.test('b');
            """).Should().Be(true);
    }

    [Theory]
    [InlineData("(?:a(b)c)+", "g", "abc")]
    [InlineData("(?:a(b)c)+", "y", "abc")]
    [InlineData("(?:a(b)c)+", "g", "z")]
    [InlineData("(?:a(b)c)+", "y", "z")]
    [InlineData("(?:)", "g", "a")]
    [InlineData("(?:)", "y", "a")]
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

    [Theory]
    [InlineData("g", 0, "[true,0]")]
    [InlineData("y", 0, "[true,0]")]
    [InlineData("g", 1, "[true,1]")]
    [InlineData("y", 1, "[true,1]")]
    [InlineData("g", 9, "[false,0]")]
    [InlineData("y", 9, "[false,0]")]
    public void EmptyPatternTestWritesTheCoercedLastIndex(string flags, int start, string expected)
    {
        using var engine = new Engine();

        engine.Evaluate($$"""
            const r = /(?:)/{{flags}};
            r.lastIndex = { valueOf() { return {{start}}; } };
            JSON.stringify([r.test('a'), r.lastIndex]);
            """).Should().Be(expected);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("y")]
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

    [Fact]
    public void EmptyPatternExecIncludesMatchIndices()
    {
        using var engine = new Engine();

        engine.Evaluate("JSON.stringify(/(?:)/d.exec('a').indices);")
            .Should().Be("[[0,0]]");
    }
}
