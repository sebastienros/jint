namespace Jint.Tests.Runtime;

/// <summary>
/// RegExp.prototype[@@replace] and [@@match] read <c>flags</c> off the receiver. While the instance has no
/// own properties and RegExp.prototype still carries its built-in accessors, the engine answers from the
/// instance's own flags instead of running nine getters; these pin that every way of making the read
/// observable still takes the specified path.
/// </summary>
public class RegExpDefaultFlagsTests
{
    [Test]
    [TestCase("'aXbX'.replace(/x/gi, '-')", "a-b-")]
    [TestCase("'aXbX'.replace(/x/ig, '-')", "a-b-")]
    [TestCase("'aXbX'.replace(/x/i, '-')", "a-bX")]
    [TestCase("'a\\u{1F600}b'.replace(/(?:)/gu, '.')", ".a.\\u{1F600}.b.")]
    [TestCase("'aXbX'.match(/x/gi).join()", "X,X")]
    [TestCase("String('aXbX'.match(/x/yi))", "null")]
    public void DefaultFlagsBehaveAsTheAccessorsWould(string script, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"String({script})").AsString().Should().Be(engine.Evaluate($"'{expected}'").AsString());
    }

    [Test]
    public void ReplacedFlagAccessorIsObserved()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let reads = 0;
            const original = Object.getOwnPropertyDescriptor(RegExp.prototype, 'global');
            Object.defineProperty(RegExp.prototype, 'global', { get() { reads++; return false; }, configurable: true });
            const replaced = 'aaa'.replace(/a/g, 'b');
            Object.defineProperty(RegExp.prototype, 'global', original);
            replaced + reads + 'aaa'.replace(/a/g, 'b');
            """).AsString();

        // With the accessor replaced the regexp reads as non-global (one replacement, one read); once the
        // original accessor is back the cached answer must not be the stale one either.
        result.Should().Be("baa1bbb");
    }

    [Test]
    public void ReplacedFlagsAccessorIsObserved()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Object.defineProperty(RegExp.prototype, 'flags', { get() { return ''; }, configurable: true });
            'aaa'.replace(/a/g, 'b') + 'aaa'.match(/a/g);
            """).AsString();

        result.Should().Be("baaa");
    }

    [Test]
    public void DeletedFlagAccessorIsObserved()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            delete RegExp.prototype.global;
            'aaa'.replace(/a/g, 'b');
            """).AsString();

        result.Should().Be("baa");
    }

    [Test]
    public void OwnFlagsPropertyOnInstanceIsObserved()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const re = /a/g;
            Object.defineProperty(re, 'flags', { value: '' });
            'aaa'.replace(re, 'b');
            """).AsString();

        result.Should().Be("baa");
    }

    [Test]
    public void SubclassAccessorIsObserved()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            class R extends RegExp { get flags() { return ''; } }
            'aaa'.replace(new R('a', 'g'), 'b');
            """).AsString();

        result.Should().Be("baa");
    }

    [Test]
    public void CompileUpdatesTheAnsweredFlags()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const re = /a/;
            const first = 'aaa'.replace(re, 'b');
            re.compile('a', 'g');
            first + 'aaa'.replace(re, 'b') + re.flags;
            """).AsString();

        result.Should().Be("baabbbg");
    }
}
