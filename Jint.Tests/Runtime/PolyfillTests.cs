namespace Jint.Tests.Runtime;

/// <summary>
/// A polyfill has to match the downlevel API's behaviour, not merely its signature. Every test here
/// binds the real BCL member on net10.0 and the backfilled one on net472, so a single assertion that
/// holds on both legs is the whole point: an assertion that has to be written twice would mean the
/// two have already diverged.
/// </summary>
public class PolyfillTests
{
    [Test]
    public void MathClampRejectsInvertedBounds()
    {
        // Math.Clamp validates its bounds -- "'1' cannot be greater than 0." -- rather than quietly
        // preferring one of them. A polyfill returning min instead would make the same call answer
        // differently per target framework, and nothing that executes could see the split, since the
        // real Math.Clamp binds from netstandard2.1 upwards.
        Invoking(() => System.Math.Clamp(5, 1, 0)).Should().Throw<ArgumentException>();
    }

    [TestCase(5, 1, 10, 5)]
    [TestCase(-5, 1, 10, 1)]
    [TestCase(50, 1, 10, 10)]
    [TestCase(7, 7, 7, 7)]
    public void MathClampAgreesOnValidBounds(int value, int min, int max, int expected)
    {
        System.Math.Clamp(value, min, max).Should().Be(expected);
    }
}
