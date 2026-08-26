namespace Jint.Tests.Runtime;

/// <summary>
/// Test262 covers <c>Uint8Array.prototype.toBase64</c> and <c>fromBase64</c> thoroughly, but it runs on
/// net10.0 only — so the downlevel encoding path has never been executed by anything. <c>Jint.Tests</c> is
/// the one suite that also runs on net472, where it binds Jint's net472 asset, which is why these live here.
/// <para>
/// Every expectation is computed from <see cref="Convert"/> on the running framework rather than written out,
/// so the assertions say "the built-in agrees with the BCL's own base64" instead of restating one particular
/// implementation's output. That keeps them meaningful if the encoder underneath is ever swapped.
/// </para>
/// </summary>
public class Uint8ArrayBase64Tests
{
    private readonly Engine _engine = new();

    private static readonly byte[][] Inputs =
    [
        // empty, then each remainder-mod-3, so all three padding shapes are covered
        [],
        [1],
        [1, 2],
        [1, 2, 3],
        // 0xFB/0xFF/0xBF are the byte patterns that produce '+' and '/', the two characters the alphabets differ on
        [0xFB, 0xFF],
        [0xFB, 0xFF, 0xBF],
        [0xFB, 0xFF, 0xBF, 0xFB, 0xFF, 0xBF],
        // every byte value, and something long enough to leave a block-at-a-time encoder nowhere to hide
        [.. Enumerable.Range(0, 256).Select(i => (byte) i)],
        [.. Enumerable.Range(0, 1000).Select(i => (byte) (i * 7))],
    ];

    private static string Literal(byte[] bytes) => "new Uint8Array([" + string.Join(",", bytes.Select(b => b.ToString())) + "])";

    [Test]
    public void EncodesWhatTheBclEncodes()
    {
        foreach (var bytes in Inputs)
        {
            var because = $"input of {bytes.Length} bytes";
            var base64 = Convert.ToBase64String(bytes);
            var base64Url = base64.Replace('+', '-').Replace('/', '_');
            var literal = Literal(bytes);

            _engine.Evaluate($"{literal}.toBase64()").AsString().Should().Be(base64, because);
            _engine.Evaluate($"{literal}.toBase64({{ omitPadding: true }})").AsString().Should().Be(base64.TrimEnd('='), because);
            _engine.Evaluate($"{literal}.toBase64({{ alphabet: 'base64url' }})").AsString().Should().Be(base64Url, because);
            _engine.Evaluate($"{literal}.toBase64({{ alphabet: 'base64url', omitPadding: true }})").AsString().Should().Be(base64Url.TrimEnd('='), because);
        }
    }

    [Test]
    public void RoundTripsThroughEveryAlphabetAndPaddingCombination()
    {
        string[] optionSets = ["{}", "{ omitPadding: true }", "{ alphabet: 'base64url' }", "{ alphabet: 'base64url', omitPadding: true }"];

        foreach (var bytes in Inputs)
        {
            var literal = Literal(bytes);
            foreach (var options in optionSets)
            {
                var because = $"{bytes.Length} bytes encoded with {options}";
                var alphabet = options.Contains("base64url") ? "{ alphabet: 'base64url' }" : "{}";
                var decoded = _engine.Evaluate($"Array.from(Uint8Array.fromBase64({literal}.toBase64({options}), {alphabet}))").AsArray();

                decoded.Length.Should().Be((uint) bytes.Length, because);
                for (var i = 0; i < bytes.Length; i++)
                {
                    decoded[(uint) i].AsNumber().Should().Be(bytes[i], because);
                }
            }
        }
    }

    /// <summary>
    /// The alphabets stay separate: the characters one uses are rejected by the other.
    /// </summary>
    [TestCase("'+/8='", "{ alphabet: 'base64url' }")]
    [TestCase("'-_8='", "{}")]
    public void RejectsTheOtherAlphabetsCharacters(string input, string options)
    {
        Invoking(() => _engine.Evaluate($"Uint8Array.fromBase64({input}, {options})"))
            .Should().Throw<Jint.Runtime.JavaScriptException>();
    }
}
