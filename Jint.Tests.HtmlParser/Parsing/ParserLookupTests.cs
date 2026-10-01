#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Parsing;

internal delegate object? ParserLookup(ReadOnlySpan<char> input);

internal sealed record ParserLookupCase(string Name, ParserLookup Match, object? Unknown,
    bool IgnoreCase, IReadOnlyDictionary<string, object?> Values);

public class ParserLookupTests
{
    [Test]
    public void EveryVocabularyAgreesWithItsReferenceForKnownAndMutatedInputs()
    {
        char[] mutations = ['\0', 'a', 'A', 'z', 'Z', '-', '/', ' ', '\u007f', '\u0080', '\u0100', '\ud800', '\udfff', '\uffff'];
        foreach (var lookup in ParserLookupCases.All)
        {
            Check(lookup, default);
            foreach (var name in lookup.Values.Keys)
            {
                Check(lookup, name);
                Check(lookup, ("!" + name + "?").AsSpan(1, name.Length));
                Check(lookup, name.ToUpperInvariant());
                Check(lookup, name.ToLowerInvariant());
                Check(lookup, name.AsSpan(1));
                Check(lookup, name.AsSpan(0, name.Length - 1));
                Check(lookup, name + "!");
                Check(lookup, "!" + name);
                var buffer = name.ToCharArray();
                for (var position = 0; position < buffer.Length; position++)
                {
                    var original = buffer[position];
                    foreach (var mutation in mutations)
                    {
                        buffer[position] = mutation;
                        Check(lookup, buffer);
                    }
                    buffer[position] = original;
                }
            }
        }
    }

    [Test]
    public void RandomUtf16InputNeverCreatesAPartialMatch()
    {
        var random = new Random(41);
        var buffer = new char[100];
        foreach (var lookup in ParserLookupCases.All)
        {
            for (var sample = 0; sample < 2000; sample++)
            {
                var length = random.Next(buffer.Length);
                for (var i = 0; i < length; i++)
                    buffer[i] = (char) random.Next(sample % 2 == 0 ? 128 : 65536);
                Check(lookup, buffer.AsSpan(0, length));
            }
        }
    }

    [Test]
    public void GeneratedCatalogsStayEquivalentToTheirMetadata()
    {
        foreach (var pair in CssPropertyRegistry.Completed)
        {
            CssPropertyRegistry.Find(pair.Key, CssDeclarationContext.Style).Should().BeSameAs(pair.Value);
            CssPropertyRegistry.Find(pair.Key, CssDeclarationContext.FontFace).Should().BeNull();
        }
    }

    [Test]
    public void TypedLookupsDoNotAllocate()
    {
        ReadTypedValues();
        ulong checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) checksum += ReadTypedValues();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        checksum.Should().BeGreaterThan(0);
    }

    private static ulong ReadTypedValues() =>
        (ulong) CssUnitLookup.Match("PX");

    [Test]
    public void XmlCatalogNamesRequireTheTerminalSemicolonWithoutConcatenatingIt()
    {
        foreach (var pair in HtmlEntities.Values)
        {
            if (!pair.Key.EndsWith(';')) continue;
            var name = pair.Key.AsSpan(0, pair.Key.Length - 1);
            HtmlEntities.Lookup.FindTerminatedName(name).Should().BeSameAs(pair.Value);
            HtmlEntities.Lookup.FindTerminatedName(pair.Key).Should().BeNull();
        }
        HtmlEntities.Lookup.FindTerminatedName("not-a-named-entity").Should().BeNull();
        HtmlEntities.Lookup.FindTerminatedName(default).Should().BeNull();
    }

    private static void Check(ParserLookupCase lookup, ReadOnlySpan<char> input)
    {
        var expected = Reference(lookup, input);
        var actual = lookup.Match(input);
        if (!Equals(actual, expected))
            Assert.Fail($"{lookup.Name}: unexpected recognition of {Convert.ToHexString(System.Runtime.InteropServices.MemoryMarshal.AsBytes(input))}");
        if (expected is string && !ReferenceEquals(actual, expected))
            Assert.Fail($"{lookup.Name}: recognition must reuse its canonical string literal.");
    }

    private static object? Reference(ParserLookupCase lookup, ReadOnlySpan<char> input)
    {
        foreach (var pair in lookup.Values)
        {
            var name = pair.Key;
            if (name.Length != input.Length) continue;
            var match = true;
            for (var i = 0; i < name.Length; i++)
            {
                var a = input[i];
                var b = name[i];
                if (lookup.IgnoreCase)
                {
                    if (a is >= 'A' and <= 'Z') a = (char) (a + 32);
                    if (b is >= 'A' and <= 'Z') b = (char) (b + 32);
                }
                if (a == b) continue;
                match = false;
                break;
            }
            if (match) return pair.Value;
        }
        return lookup.Unknown;
    }
}
