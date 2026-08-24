using System;
using System.Text;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host can expose its own string representation by deriving from <see cref="LazyJsString"/>: it declares
/// the length up front and implements one <c>Materialize()</c> method, and the flat .NET string is produced
/// only when something actually asks for the characters. These tests pin that contract for the engine paths
/// a script can reach.
///
/// <para>
/// Before <see cref="LazyJsString"/> the same thing was written by passing <see langword="null"/> to
/// <see cref="JsString(string)"/> — whose parameter is typed <see cref="string"/> — and overriding
/// <c>ToString()</c>, <c>Length</c> and the indexer, with the memoization written by hand in each host. That
/// still works and <see cref="RavenApiUsageTests"/> still exercises it; what it never had was a signature, a
/// place for the contract to be written down, or anything that checked it.
/// </para>
/// </summary>
public class LazyHostStringTests
{
    private const string Text = "abc";

    /// <summary>
    /// Whether this run has host-contract verification on. Public and static so xUnit can read it for
    /// <c>SkipUnless</c>.
    /// </summary>
    public static bool Verifying => HostContractVerificationSwitch.Enabled;

    /// <inheritdoc cref="Verifying" />
    public static bool NotVerifying => !Verifying;

    private static Engine CreateEngine(out LazyHostString host, string value = Text)
    {
        host = new LazyHostString(value);
        var engine = new Engine();
        engine.SetValue("host", host);
        return engine;
    }

    [Fact]
    public void CanConcatWithoutMaterializedBackingValue()
    {
        // String.prototype.concat asks the receiver for a growable buffer first; the base
        // implementation used to hand the (still null) backing value to the buffer and blow up.
        var engine = CreateEngine(out var host);

        engine.Evaluate("host.concat('def', 'ghi')").AsString().Should().Be("abcdefghi");
        engine.Evaluate("host.concat()").AsString().Should().Be(Text);
        host.MaterializationCount.Should().Be(1, "the flat value is produced once and cached");
    }

    [Fact]
    public void CanBuildUpWithCompoundAssignmentChain()
    {
        var engine = CreateEngine(out _);

        var result = engine.Evaluate("var s = host; s += '-1'; s += '-2'; s += '-3'; s").AsString();

        result.Should().Be("abc-1-2-3");
    }

    [Fact]
    public void CanBeConcatenatedWithPlusOperator()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host + 'def'").AsString().Should().Be("abcdef");
        engine.Evaluate("'xyz' + host").AsString().Should().Be("xyzabc");
    }

    [Fact]
    public void CanBeUsedInTemplateLiteral()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("`[${host}]`").AsString().Should().Be("[abc]");
        engine.Evaluate("`${host}${host}`").AsString().Should().Be("abcabc");
    }

    [Fact]
    public void ReportsLengthWithoutMaterializing()
    {
        var engine = CreateEngine(out var host);

        engine.Evaluate("host.length").AsNumber().Should().Be(3);

        host.MaterializationCount.Should().Be(0, "the length was declared to the constructor, so nothing has to be produced to answer it");
    }

    [Fact]
    public void SupportsCharacterAccess()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host[0]").AsString().Should().Be("a");
        engine.Evaluate("host.charAt(1)").AsString().Should().Be("b");
        engine.Evaluate("host.charCodeAt(2)").AsNumber().Should().Be('c');
    }

    [Fact]
    public void IsEqualToTheEquivalentPlainString()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host === 'abc'").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc' === host").AsBoolean().Should().BeTrue();
        engine.Evaluate("host == 'abc'").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc' == host").AsBoolean().Should().BeTrue();

        engine.Evaluate("host === 'abd'").AsBoolean().Should().BeFalse();
        engine.Evaluate("host !== 'abcd'").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void HasTheSameEqualityAndHashAsTheEquivalentPlainString()
    {
        // the base Equals/GetHashCode implementations are what an unspecialized host subclass
        // inherits, so they have to answer from the materialized value rather than the raw field
        var host = new LazyHostString(Text);
        var flat = new JsString(Text);

        host.Equals(flat).Should().BeTrue();
        flat.Equals(host).Should().BeTrue();
        host.Equals(Text).Should().BeTrue();
        host.GetHashCode().Should().Be(flat.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsPropertyKey()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("var o = {}; o[host] = 42; o.abc").AsNumber().Should().Be(42);
        engine.Evaluate("Object.keys(o)[0]").AsString().Should().Be(Text);
        engine.Evaluate("({ abc: 7 })[host]").AsNumber().Should().Be(7);
        engine.Evaluate("host in { abc: 1 }").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CanBeUsedAsCollectionKey()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("var m = new Map(); m.set(host, 1); m.get('abc')").AsNumber().Should().Be(1);
        engine.Evaluate("new Set([host, 'abc']).size").AsNumber().Should().Be(1);
    }

    [Fact]
    public void CanBeSerializedToJson()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("JSON.stringify(host)").AsString().Should().Be("\"abc\"");
        engine.Evaluate("JSON.stringify({ value: host })").AsString().Should().Be("{\"value\":\"abc\"}");
        engine.Evaluate("JSON.stringify({ [host]: 1 })").AsString().Should().Be("{\"abc\":1}");
        engine.Evaluate("JSON.stringify([host])").AsString().Should().Be("[\"abc\"]");
    }

    [Fact]
    public void SupportsCommonStringPrototypeMethods()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host.toUpperCase()").AsString().Should().Be("ABC");
        engine.Evaluate("host.indexOf('b')").AsNumber().Should().Be(1);
        engine.Evaluate("host.slice(1)").AsString().Should().Be("bc");
        engine.Evaluate("host.split('b').join('-')").AsString().Should().Be("a-c");
        engine.Evaluate("String(host)").AsString().Should().Be(Text);
        engine.Evaluate("host.toString()").AsString().Should().Be(Text);
    }

    /// <summary>
    /// The laziness itself, expressed as a count. Every script below is answered from the declared length
    /// (or, for the indexer, from the host's own encoded buffer) and never asks for the text.
    /// </summary>
    [Theory]
    [InlineData("host.length")]
    [InlineData("for (var i = 0, n = 0; i < host.length; i++) n++;")]
    [InlineData("typeof host")]
    [InlineData("!!host")]
    [InlineData("host ? 1 : 0")]
    [InlineData("host[0]")]
    [InlineData("host.charAt(1)")]
    [InlineData("host.charCodeAt(2)")]
    [InlineData("host === host")]
    [InlineData("host === 'abcd'")]
    [InlineData("host !== 'ab'")]
    [InlineData("host == 'abcd'")]
    public void NothingThatOnlyNeedsTheLengthMaterializes(string script)
    {
        var engine = CreateEngine(out var host);

        engine.Evaluate(script);

        host.MaterializationCount.Should().Be(0);
    }

    [Fact]
    public void ComparingTwoLazyStringsOfDifferentLengthsMaterializesNeither()
    {
        var shorter = new LazyHostString("abc");
        var longer = new LazyHostString("abcd");
        var engine = new Engine();
        engine.SetValue("a", shorter);
        engine.SetValue("b", longer);

        engine.Evaluate("a === b").AsBoolean().Should().BeFalse();

        shorter.MaterializationCount.Should().Be(0, "string equality compares lengths first, and both answer that from the declared length");
        longer.MaterializationCount.Should().Be(0);
    }

    [Fact]
    public void ReadingTheTextMaterializesExactlyOnceAndStaysThere()
    {
        var engine = CreateEngine(out var host);

        engine.Evaluate("String(host)").AsString().Should().Be(Text);
        host.MaterializationCount.Should().Be(1);

        engine.Evaluate("host.toUpperCase() + host.slice(1) + JSON.stringify(host) + (host === 'abd')");
        host.MaterializationCount.Should().Be(1, "the base class memoizes what Materialize() returned");
    }

    /// <summary>
    /// The other half of the count, and the part a host has to plan for: three script-visible operations
    /// genuinely need the characters and therefore materialize. Using the value as a **property key** is the
    /// one that surprises — a key is a string plus its ordinal hash, and neither can be answered from a
    /// length — so <c>host in obj</c>, <c>obj[host] = …</c> and a computed key in an object literal all
    /// produce the text. So does anything that writes it out: <c>JSON.stringify</c>, string concatenation,
    /// and every <c>String.prototype</c> method that reads more than one character.
    /// </summary>
    [Theory]
    [InlineData("host in { abc: 1 }")]
    [InlineData("host in { zzz: 1 }")]
    [InlineData("var o = {}; o[host] = 1;")]
    [InlineData("({ [host]: 1 })")]
    [InlineData("({ abc: 7 })[host]")]
    [InlineData("new Map().set(host, 1)")]
    [InlineData("JSON.stringify(host)")]
    [InlineData("JSON.stringify({ value: host })")]
    [InlineData("JSON.stringify([host])")]
    [InlineData("host + ''")]
    [InlineData("`${host}`")]
    [InlineData("host === 'abd'")]
    [InlineData("host.toUpperCase()")]
    [InlineData("String(host)")]
    public void TheOperationsThatNeedTheCharactersMaterializeExactlyOnce(string script)
    {
        var engine = CreateEngine(out var host);

        engine.Evaluate(script);

        host.MaterializationCount.Should().Be(1);
    }

    [Fact]
    public void MaterializeIsCalledOncePerInstanceAcrossEveryRead()
    {
        var engine = CreateEngine(out var host);

        engine.Evaluate("var o = {}; o[host] = 1; JSON.stringify(o) + host.toUpperCase() + (host === 'abd') + (host + '!')");

        host.MaterializationCount.Should().Be(1);
    }

    [Fact]
    public void ADeclaredLengthThatIsNotAStringLengthIsRefusedAtConstruction()
    {
        Invoking(() => new FixedLengthString(-1, "abc")).Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("length");

        Invoking(() => new FixedLengthString(int.MaxValue, "abc")).Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("length");
    }

    [Fact]
    public void AMaterializeThatReturnsNullIsRefusedByName()
    {
        var engine = new Engine();
        engine.SetValue("host", new NullMaterializingString());

        Invoking(() => engine.Evaluate("String(host)")).Should().Throw<InvalidOperationException>()
            .WithMessage("*NullMaterializingString*Materialize*null*");
    }

    /// <summary>
    /// The declared length is what every length-first shortcut answers from, so a host that declares one
    /// length and produces another breaks them silently: the value compares unequal to a string it equals,
    /// and an index the engine considers in range reads past the end. Host-contract verification catches the
    /// disagreement at the moment both answers exist.
    /// </summary>
    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void ADeclaredLengthThatContradictsTheTextIsCaughtWhenVerifying()
    {
        var engine = new Engine();
        engine.SetValue("host", new FixedLengthString(3, "abcdef"));

        Invoking(() => engine.Evaluate("String(host)")).Should().Throw<InvalidOperationException>()
            .WithMessage("*FixedLengthString*3*6*");
    }

    /// <inheritdoc cref="ADeclaredLengthThatContradictsTheTextIsCaughtWhenVerifying" />
    /// <remarks>
    /// The other side of the same gate: with verification off the check is not merely skipped, it is not
    /// emitted, so the lie goes through and the host sees the broken behaviour the verifier describes.
    /// </remarks>
    [Fact(Skip = "host-contract verification is on in this run", SkipUnless = nameof(NotVerifying))]
    public void ADeclaredLengthThatContradictsTheTextIsNotCheckedWhenNotVerifying()
    {
        var engine = new Engine();
        engine.SetValue("host", new FixedLengthString(3, "abcdef"));

        engine.Evaluate("String(host)").AsString().Should().Be("abcdef");
        engine.Evaluate("host.length").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// Stands in for a host string backed by a native handle: the payload is kept encoded and only decoded
    /// on demand. The only members it declares are the length (to the base constructor),
    /// <see cref="Materialize"/>, and the indexer — which it overrides because reading one byte out of the
    /// buffer is cheaper than decoding the whole value.
    /// </summary>
    private sealed class LazyHostString : LazyJsString
    {
        // ASCII only, which keeps the encoded length equal to the character length so that the declared
        // length is knowable without decoding - the whole point of holding the payload encoded.
        private readonly byte[] _encoded;
        private int _materializationCount;

        public LazyHostString(string value) : base(value.Length)
        {
            _encoded = Encoding.ASCII.GetBytes(value);
        }

        /// <summary>
        /// How many times the flat value had to be produced. Used by the tests to assert that members
        /// which can answer lazily really do, and that the base class memoizes the one that cannot.
        /// </summary>
        public int MaterializationCount => _materializationCount;

        protected override string Materialize()
        {
            _materializationCount++;
            return Encoding.ASCII.GetString(_encoded);
        }

        public override char this[int index] => (char) _encoded[index];
    }

    /// <summary>
    /// Declares whatever length it is told to, so the constructor's validation and the length-agreement
    /// verifier can both be reached from a test.
    /// </summary>
    private sealed class FixedLengthString : LazyJsString
    {
        private readonly string _text;

        public FixedLengthString(int declaredLength, string text) : base(declaredLength)
        {
            _text = text;
        }

        protected override string Materialize() => _text;
    }

    private sealed class NullMaterializingString : LazyJsString
    {
        public NullMaterializingString() : base(3)
        {
        }

        protected override string Materialize() => null;
    }
}
