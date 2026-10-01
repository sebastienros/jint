#nullable enable
using System.Text;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class GeneratedNameLookupTests
{
    [Test]
    public void ExactNamesReturnTheCanonicalLiteralsFromSlicedInputs()
    {
        foreach (var name in HtmlKnownNames.Values)
        {
            HtmlKnownNames.Match(name).Should().BeSameAs(name);
            HtmlKnownNames.Match(("!" + name + "?").AsSpan(1, name.Length)).Should().BeSameAs(name);
            CheckHtml(name.AsSpan(1));
            CheckHtml(name.AsSpan(0, name.Length - 1));
            CheckHtml(name + "x");
            CheckHtml("x" + name);
            CheckHtml(name.ToUpperInvariant());
        }
        HtmlKnownNames.Match(default).Should().BeNull();
    }

    [Test]
    public void EverySingleByteMutationAgreesWithTheReference()
    {
        foreach (var name in HtmlKnownNames.Values)
        {
            var input = name.ToCharArray();
            for (var position = 0; position < input.Length; position++)
            {
                var original = input[position];
                for (var shift = 0; shift <= 8; shift += 8)
                {
                    for (var value = 0; value < 256; value++)
                    {
                        input[position] = (char) ((original & ~(255 << shift)) | (value << shift));
                        CheckHtml(input);
                    }
                }
                input[position] = original;
            }
        }
    }

    [Test]
    public void RandomUtf16SpansAgreeWithTheReference()
    {
        var random = new Random(20260927);
        var buffer = new char[82];
        for (var sample = 0; sample < 50000; sample++)
        {
            var length = random.Next(81);
            for (var i = 0; i < length; i++)
                buffer[i + 1] = (char) random.Next(sample % 2 == 0 ? 128 : 65536);
            CheckHtml(buffer.AsSpan(1, length));
        }
    }

    [Test]
    public void RecognitionDoesNotAllocateForHitsOrMisses()
    {
        foreach (var name in HtmlKnownNames.Values) HtmlKnownNames.Match(name);
        HtmlKnownNames.Match("unknown");
        var checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            foreach (var name in HtmlKnownNames.Values) checksum += HtmlKnownNames.Match(name)!.Length;
            checksum += HtmlKnownNames.Match("unknown")?.Length ?? 0;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        checksum.Should().Be(HtmlKnownNames.Values.ToArray().Sum(name => name.Length) * 1000);
    }

    [Test]
    public void AsciiCaseMasksPreservePunctuationAndNonAsciiBits()
    {
        foreach (var name in HeaderCharNames.Values)
        {
            foreach (var variant in new[] { name, name.ToLowerInvariant(), name.ToUpperInvariant() })
            {
                HeaderCharNames.Match(variant).Should().BeSameAs(name);
                HeaderByteNames.Match(Encoding.ASCII.GetBytes(variant)).Should().BeSameAs(name);
            }
            var input = name.ToCharArray();
            for (var position = 0; position < input.Length; position++)
            {
                var original = input[position];
                input[position] = (char) (original | 0x100);
                HeaderCharNames.Match(input).Should().BeNull();
                input[position] = (char) (original ^ 0x20);
                var expected = char.IsAsciiLetter(original) ? name : null;
                HeaderCharNames.Match(input).Should().Be(expected);
                HeaderByteNames.Match(Encoding.ASCII.GetBytes(input)).Should().Be(expected);
                input[position] = original;
            }
        }
    }

    [Test]
    public void MaskedSwitchCasesRecheckPunctuationThatCollidesWithALetter()
    {
        Span<char> text = stackalloc char[2];
        Span<byte> bytes = stackalloc byte[2];
        for (var first = 0; first < 0x200; first++)
        {
            for (var second = 0; second < 0x200; second++)
            {
                text[0] = (char) first;
                text[1] = (char) second;
                var expected = Reference(text, PunctuationCharNames.Values, ignoreCase: true);
                if (!ReferenceEquals(PunctuationCharNames.Match(text), expected))
                    Assert.Fail($"Char lookup disagrees with the reference for U+{first:X4} U+{second:X4}");
                if (first > 0xFF || second > 0xFF) continue;
                bytes[0] = (byte) first;
                bytes[1] = (byte) second;
                if (!ReferenceEquals(PunctuationByteNames.Match(bytes), expected))
                    Assert.Fail($"Byte lookup disagrees with the reference for {first:X2} {second:X2}");
            }
        }
    }

    [Test]
    public void GeneratedExamplesAndRandomByteInputsAgreeWithTheirReference()
    {
        foreach (var name in SharedPrefixNames.Values)
        {
            SharedPrefixNames.Match(name).Should().BeSameAs(name);
            for (var position = 0; position < name.Length; position++)
            {
                var input = name.ToCharArray();
                input[position] = '!';
                SharedPrefixNames.Match(input).Should().BeNull();
            }
        }
        foreach (var name in KeywordByteNames.Values)
            KeywordByteNames.Match(Encoding.ASCII.GetBytes(name)).Should().BeSameAs(name);

        var random = new Random(42);
        var bytes = new byte[40];
        var characters = new char[40];
        for (var sample = 0; sample < 30000; sample++)
        {
            random.NextBytes(bytes);
            var length = random.Next(bytes.Length + 1);
            for (var i = 0; i < length; i++) characters[i] = (char) bytes[i];
            var input = bytes.AsSpan(0, length);
            var text = characters.AsSpan(0, length);
            var header = Reference(text, HeaderCharNames.Values, ignoreCase: true);
            if (HeaderByteNames.Match(input) != header || HeaderCharNames.Match(text) != header ||
                KeywordByteNames.Match(input) != Reference(text, KeywordByteNames.Values))
                Assert.Fail($"Mismatch for bytes {Convert.ToHexString(input)}");
        }
    }

    private static void CheckHtml(ReadOnlySpan<char> input)
    {
        var expected = Reference(input, HtmlKnownNames.Values);
        var actual = HtmlKnownNames.Match(input);
        if (!ReferenceEquals(actual, expected))
            Assert.Fail($"Lookup disagrees with the reference for {Convert.ToHexString(System.Runtime.InteropServices.MemoryMarshal.AsBytes(input))}");
    }

    private static string? Reference(ReadOnlySpan<char> input, ReadOnlySpan<string> names, bool ignoreCase = false)
    {
        foreach (var name in names)
        {
            if (input.Length != name.Length) continue;
            var matches = true;
            for (var i = 0; i < input.Length; i++)
            {
                var actual = input[i];
                var expected = name[i];
                if (ignoreCase)
                {
                    if (actual is >= 'a' and <= 'z') actual = (char) (actual - 32);
                    if (expected is >= 'a' and <= 'z') expected = (char) (expected - 32);
                }
                if (actual == expected) continue;
                matches = false;
                break;
            }
            if (matches) return name;
        }
        return null;
    }
}
