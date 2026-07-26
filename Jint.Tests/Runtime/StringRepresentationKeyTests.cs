using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="JsString"/> has more than one internal representation: a flat value, a lazily
/// materialized view over a larger string, and a growable buffer built up by repeated concatenation.
/// All of them must be usable interchangeably as a key, which means each one has to hash its
/// <em>content</em> — the same hash a plain <see cref="JsString"/> with those characters produces.
/// Hashing anything else (buffer identity, for instance) breaks the equals/hash-code contract:
/// two values compare equal but land in different buckets, so a lookup misses.
/// </summary>
public class StringRepresentationKeyTests
{
    /// <summary>
    /// Builds "abc" as a growable-buffer string and leaves it where a built-in can read it back
    /// without the interpreter's copy-on-assign step flattening it first. Two appends are needed:
    /// the first produces a buffer-free value, the second grows the buffer.
    /// </summary>
    private const string ConcatenatedInArray = "var a = ['a']; a[0] += 'b'; a[0] += 'c';";

    private static Engine CreateEngine() => new();

    [Fact]
    public void ConcatenatedStringIsBuiltUpWithALiveBuffer()
    {
        // guards the premise of every other test here: if the interpreter ever starts flattening
        // this shape, these tests would silently stop covering the growable-buffer representation
        var engine = CreateEngine();

        var value = engine.Evaluate(ConcatenatedInArray + " a[0]");

        value.Should().BeOfType<JsString.ConcatenatedString>();
        value.ToString().Should().Be("abc");
    }

    [Fact]
    public void ConcatenatedStringHashesLikeTheEquivalentFlatString()
    {
        var engine = CreateEngine();

        var concatenated = engine.Evaluate(ConcatenatedInArray + " a[0]");

        concatenated.Equals(new JsString("abc")).Should().BeTrue();
        concatenated.GetHashCode().Should().Be(new JsString("abc").GetHashCode());
    }

    [Fact]
    public void StringConcatResultHashesLikeTheEquivalentFlatString()
    {
        var engine = CreateEngine();

        var concatenated = engine.Evaluate("'a'.concat('b', 'c')");

        concatenated.Should().BeOfType<JsString.ConcatenatedString>();
        concatenated.Equals(new JsString("abc")).Should().BeTrue();
        concatenated.GetHashCode().Should().Be(new JsString("abc").GetHashCode());
    }

    [Fact]
    public void ConcatenatedStringHashesLikeTheFlatStringAfterMaterializing()
    {
        // asking for the flat text caches it, but the growable buffer is still around; the hash
        // must not depend on which of the two the value happens to be carrying
        var engine = CreateEngine();

        var concatenated = engine.Evaluate(ConcatenatedInArray + " String(a[0]); a[0]");

        concatenated.ToString().Should().Be("abc");
        concatenated.GetHashCode().Should().Be(new JsString("abc").GetHashCode());
    }

    [Fact]
    public void ConcatenatedStringWorksAsAHostDictionaryKey()
    {
        // a host that keys its own dictionary by JsValue relies on the same contract
        var engine = CreateEngine();
        var concatenated = engine.Evaluate(ConcatenatedInArray + " a[0]");

        var set = new HashSet<JsValue> { concatenated };

        set.Contains(new JsString("abc")).Should().BeTrue();
        set.Add(new JsString("abc")).Should().BeFalse("an equal string must not create a second entry");
    }

    [Fact]
    public void ConcatenatedStringIsFoundInASetByALiteral()
    {
        var engine = CreateEngine();

        engine.Evaluate(ConcatenatedInArray + " new Set(a).has('abc')").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConcatenatedStringIsFoundInAMapByALiteral()
    {
        var engine = CreateEngine();

        engine.Evaluate(ConcatenatedInArray + " new Map(new Set(a).entries()).get('abc')")
            .AsString().Should().Be("abc");
    }

    [Fact]
    public void ConcatenatedStringDoesNotCreateADuplicateSetEntry()
    {
        var engine = CreateEngine();

        // the array holds the growable-buffer "abc" and a plain literal "abc"
        engine.Evaluate("var a = ['a', 'abc']; a[0] += 'b'; a[0] += 'c'; new Set(a).size")
            .AsNumber().Should().Be(1);
    }

    [Fact]
    public void ALiteralSetFindsAConcatenatedString()
    {
        // the other direction: the stored keys are flat and the probed value is concatenated
        var engine = CreateEngine();

        engine.Evaluate(ConcatenatedInArray + " new Set(['abc']).intersection(new Set(a)).size")
            .AsNumber().Should().Be(1);
        engine.Evaluate(ConcatenatedInArray + " new Set(a).intersection(new Set(['abc'])).size")
            .AsNumber().Should().Be(1);
        engine.Evaluate(ConcatenatedInArray + " new Set(a).isSubsetOf(new Set(['abc']))")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate(ConcatenatedInArray + " new Set(['abc']).union(new Set(a)).size")
            .AsNumber().Should().Be(1);
    }

    [Fact]
    public void AppendChainIsFoundInASetByALiteral()
    {
        var engine = CreateEngine();

        engine.Evaluate("var a = ['x']; for (var i = 0; i < 5; i++) { a[0] += i; } new Set(a).has('x01234')")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConcatenatedObjectPropertyIsFoundInASetByALiteral()
    {
        var engine = CreateEngine();

        engine.Evaluate("var o = { p: 'a' }; o.p += 'b'; o.p += 'c'; new Set(Object.values(o)).has('abc')")
            .AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConcatenatedStringRemovedFromASetByALiteral()
    {
        // removal happened to work already (the ordering list compares by content), but it left the
        // lookup structure holding a stale entry; assert the observable end state either way
        var engine = CreateEngine();

        engine.Evaluate(ConcatenatedInArray + " var s = new Set(a); s.delete('abc'); s.size")
            .AsNumber().Should().Be(0);
        engine.Evaluate(ConcatenatedInArray + " var s = new Set(a); s.delete('abc'); s.has('abc')")
            .AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ConcatenatedStringWorksAsAPropertyKey()
    {
        // property keys are converted to a flat .NET string before they are hashed, so they were
        // never affected; pinned here to bound the blast radius of the representation choice
        var engine = CreateEngine();

        engine.Evaluate(ConcatenatedInArray + " var o = {}; o[a[0]] = 7; o.abc").AsNumber().Should().Be(7);
        engine.Evaluate(ConcatenatedInArray + " var o = { abc: 7 }; o[a[0]]").AsNumber().Should().Be(7);
        engine.Evaluate(ConcatenatedInArray + " var o = { abc: 7 }; a[0] in o").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConcatenatedStringComparesEqualToTheEquivalentLiteral()
    {
        // equality was always content based -- that is precisely why an identity hash is a contract
        // violation rather than merely an unusual choice
        var engine = CreateEngine();

        engine.Evaluate(ConcatenatedInArray + " a[0] === 'abc'").AsBoolean().Should().BeTrue();
        engine.Evaluate(ConcatenatedInArray + " 'abc' === a[0]").AsBoolean().Should().BeTrue();
        engine.Evaluate(ConcatenatedInArray + " a.includes('abc')").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void SlicedStringIsUsableAsACollectionKey()
    {
        // the sibling lazily materialized representation already hashes its content; regression guard
        var engine = CreateEngine();
        const string Source = "var big = 'abcde'; while (big.length < 2000) { big = big + big; } big = big.substring(0, 2000);";

        engine.Evaluate(Source + " new Set([big.substring(0, 1500), big.substring(0, 1500)]).size")
            .AsNumber().Should().Be(1);
        engine.Evaluate(Source + " var flat = big.split('').slice(0, 1500).join(''); new Set([big.substring(0, 1500), flat]).size")
            .AsNumber().Should().Be(1);
        engine.Evaluate(Source + " var flat = big.split('').slice(0, 1500).join(''); new Map([[big.substring(0, 1500), 1]]).get(flat)")
            .AsNumber().Should().Be(1);
    }
}
