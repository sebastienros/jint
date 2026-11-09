#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// A template literal joins a substitution that is already a long string the way <c>+</c> does, without
/// copying it, so a template that accumulates (<c>s = `${s}x`</c>) is linear rather than quadratic (issue
/// #4460). These check that the text it produces is the same whichever way it was built.
/// </summary>
public class TemplateLiteralConcatenationTests
{
    [TestCase("`${s}ab`", "s + 'ab'", 20000)]
    [TestCase("`ab${s}`", "'ab' + s", 20000)]
    [TestCase("`a${s}b`", "'a' + s + 'b'", 20000)]
    [TestCase("`${s}${i % 10}-`", "s + (i % 10) + '-'", 10000)]
    public void AnAccumulatingTemplateBuildsTheSameTextAsPlus(string template, string plus, int iterations)
    {
        var engine = new Engine();
        var actual = engine.Evaluate($"var s = ''; for (var i = 0; i < {iterations}; i++) {{ s = {template}; }} s").AsString();
        var expected = engine.Evaluate($"var s = ''; for (var i = 0; i < {iterations}; i++) {{ s = {plus}; }} s").AsString();

        actual.Length.Should().Be(2 * iterations);
        actual.Should().Be(expected);
    }

    /// <summary>
    /// Long substitutions next to quasis, short substitutions and each other, in every position.
    /// </summary>
    [TestCase("`${big}`", "big")]
    [TestCase("`<${big}>`", "'<' + big + '>'")]
    [TestCase("`${big}${big}`", "big + big")]
    [TestCase("`a${1}b${big}c${'d'}e${big}f`", "'a' + 1 + 'b' + big + 'c' + 'd' + 'e' + big + 'f'")]
    [TestCase("`${big}${small}${big}`", "big + small + big")]
    [TestCase("`${small}${big}${small}`", "small + big + small")]
    [TestCase("`${{ toString() { return big; } }}|${big}`", "big + '|' + big")]
    public void LongSubstitutionsGiveTheSameTextAsPlus(string template, string plus)
    {
        var engine = new Engine();
        engine.Execute("var big = 'x'.repeat(600) + 'y'.repeat(600); var small = 'small';");
        var expected = engine.Evaluate(plus).AsString();

        engine.Evaluate(template).AsString().Should().Be(expected);
        engine.Evaluate($"{template} === ({plus})").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{template}.length").AsNumber().Should().Be(expected.Length);
        engine.Evaluate($"{template}.charAt({expected.Length - 1})").AsString().Should().Be(expected[^1].ToString());
    }

    /// <summary>
    /// Substitutions are still evaluated and coerced left to right, each one before the next is evaluated.
    /// </summary>
    [Test]
    public void SubstitutionsAreCoercedInOrder()
    {
        var engine = new Engine();
        engine.Execute("""
            var log = [];
            var big = 'z'.repeat(1000);
            function v(name, text) { log.push('get ' + name); return { toString() { log.push('str ' + name); return text; } }; }
            """);

        engine.Evaluate("`${v('a', big)}-${(log.push('get b'), big)}-${v('c', 'c')}`.length").AsNumber().Should().Be(2003);
        engine.Evaluate("log.join()").AsString().Should().Be("get a,str a,get b,get c,str c");
    }

    /// <summary>
    /// An <c>await</c> inside a template, after a long substitution has been joined: the part already built
    /// is kept across the suspension and nothing is evaluated twice.
    /// </summary>
    [Test]
    public void ALongSubstitutionBeforeAnAwaitIsKept()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async () => {
                var big = 'q'.repeat(700);
                var i = 0;
                var s = `<${big}|${++i}|${await Promise.resolve('mid')}|${big}|${++i}>`;
                return s === '<' + big + '|1|mid|' + big + '|2>' && i === 2 ? 'ok' : 'bad: ' + s.length + ' ' + i;
            })()
            """).UnwrapIfPromise().AsString();

        result.Should().Be("ok");
    }

    /// <summary>
    /// The length limit is checked on the summed lengths before the result is built, so a template over it is
    /// refused without first making the text of its substitutions (see <see cref="StringLengthLimitTests"/>).
    /// </summary>
    [TestCase("`${s}${s}`")]
    [TestCase("`${s}y${s}`")]
    [TestCase("`y${s}${s}`")]
    public void ATemplatePastTheLengthLimitIsACatchableRangeError(string template)
    {
        var engine = new Engine();
        engine.Execute("var s = 'x'; for (var i = 0; i < 28; i++) { s = s + s; }");
        engine.Evaluate($"`${{s}}y`.length").AsNumber().Should().Be(268_435_457);

        engine.Evaluate($"try {{ {template}; 'no error' }} catch (e) {{ e.constructor.name + ': ' + e.message }}")
            .AsString().Should().Be("RangeError: Invalid string length");
        JsString.MaxLength.Should().BeLessThan(2 * 268_435_456);
    }

    /// <summary>
    /// The join defers rather than copies: a substitution already past the threshold is kept as a
    /// <see cref="JsString.RopeString"/>, which is what makes an accumulating template linear. On a build that
    /// copies, the result is a flat <see cref="JsString"/> and this fails.
    /// </summary>
    [Test]
    public void ALongSubstitutionIsJoinedAsARopeString()
    {
        var engine = new Engine();
        engine.Execute("var big = 'x'.repeat(600);");
        engine.Evaluate("`${big}!`").Should().BeOfType<JsString.RopeString>();
    }

    /// <summary>
    /// Joining a long substitution does not read its text: a <see cref="LazyJsString"/> is deferred by its
    /// length alone, so reading the joined result's <c>length</c> never calls
    /// <see cref="LazyJsString.Materialize"/> — only reading a character does. On a build that copies, the
    /// substitution is materialized while the template is built and the first assertion fails.
    /// </summary>
    [Test]
    public void JoiningALazySubstitutionDoesNotMaterializeIt()
    {
        var engine = new Engine();
        var lazy = new CountingLazyString(600);
        engine.SetValue("lazy", lazy);

        engine.Evaluate("`${lazy}!`.length").AsNumber().Should().Be(601);
        lazy.MaterializeCount.Should().Be(0, "the join defers by length and never reads the text");

        engine.Evaluate("`${lazy}!`.charAt(0)").AsString().Should().Be("x");
        lazy.MaterializeCount.Should().Be(1, "reading a character flattens the rope, which reads the text once");
    }

    private sealed class CountingLazyString : LazyJsString
    {
        public CountingLazyString(int length) : base(length)
        {
        }

        public int MaterializeCount { get; private set; }

        protected override string Materialize()
        {
            MaterializeCount++;
            return new string('x', Length);
        }
    }
}
