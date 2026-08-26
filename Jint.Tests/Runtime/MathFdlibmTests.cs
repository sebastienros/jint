using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins Math.acosh / asinh / atanh / cbrt / expm1 / log1p to the fdlibm algorithms ECMA-262 21.3.2
/// recommends by name, which Jint ports in <c>Jint/Native/Math/Fdlibm.cs</c>.
/// <para>
/// These six used to be open-coded from their textbook identities — <c>log(x + sqrt(x*x - 1))</c>,
/// <c>exp(x) - 1</c>, <c>log(1 + x)</c>, <c>pow(|x|, 1/3)</c> — every one of which loses all
/// significant digits somewhere in its domain: <c>acosh(1e300)</c> and <c>asinh(1e300)</c> answered
/// Infinity because <c>x*x</c> overflows, and <c>asinh</c>, <c>atanh</c>, <c>expm1</c> and
/// <c>log1p</c> all flushed 1e-300 to zero, which is what 21.3.2.14 and 21.3.2.21 forbid in so many
/// words ("The result is computed in a way that is accurate even when the value of x is close to
/// 0"). <c>log1p(1e-15)</c> was 11% out and <c>cbrt(1e-300)</c> some 58 ULP out.
/// </para>
/// <para>
/// Expectations are exact bit patterns. Every one of them was checked against the correctly rounded
/// result computed in 900-digit decimal arithmetic; the port never differs from it by more than
/// 1 ULP, and each row that is not exactly the correctly rounded double says so in a comment.
/// The <c>*ReferenceTable</c> sets instead carry test262's own reference values, sampled from
/// <c>test/staging/sm/Math/*-approx.js</c>, and are asserted with each file's own ULP tolerance.
/// </para>
/// </summary>
public class MathFdlibmTests
{
    private readonly Engine _engine = new();

    private double Eval(string source) => _engine.Evaluate(source).AsNumber();

    /// <summary>
    /// The distance between two doubles in representable values, which is how test262's
    /// <c>sm/non262-Math-shell.js</c> harness measures "near".
    /// </summary>
    private static long UlpDistance(double actual, double expected)
    {
        if (double.IsNaN(actual) || double.IsNaN(expected))
        {
            return double.IsNaN(actual) && double.IsNaN(expected) ? 0 : long.MaxValue;
        }

        var a = BitConverter.DoubleToInt64Bits(actual);
        var b = BitConverter.DoubleToInt64Bits(expected);
        if (a < 0 != b < 0)
        {
            return long.MaxValue;
        }

        return System.Math.Abs(a - b);
    }

    private static void ShouldBeExactly(double actual, double expected, string what)
    {
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue($"{what} should be NaN but was {actual:R}");
            return;
        }

        BitConverter.DoubleToInt64Bits(actual).Should().Be(
            BitConverter.DoubleToInt64Bits(expected),
            $"{what} should be exactly {expected:R} but was {actual:R}");
    }

    // ---------------------------------------------------------------------------------------
    // The eight values from the bug report: every one of them used to be catastrophically wrong.
    // ---------------------------------------------------------------------------------------

    [Test]
    public void AcoshDoesNotOverflowOnLargeArguments()
    {
        // was Infinity, because x*x overflows for x >= ~1.34e154
        ShouldBeExactly(Eval("Math.acosh(1e300)"), 691.4686750787736, "Math.acosh(1e300)");

        // 1 ULP below the correctly rounded 691.4686750787737, which is the value test262's
        // acosh-approx.js names (with its own tolerance of 9).
        UlpDistance(Eval("Math.acosh(1e300)"), 691.4686750787737).Should().BeLessThanOrEqualTo(1);
    }

    [Test]
    public void AsinhDoesNotOverflowOnLargeArguments()
    {
        // was Infinity
        ShouldBeExactly(Eval("Math.asinh(1e300)"), 691.4686750787736, "Math.asinh(1e300)");
        UlpDistance(Eval("Math.asinh(1e300)"), 691.4686750787737).Should().BeLessThanOrEqualTo(1);
    }

    [Test]
    public void SmallArgumentsAreNotFlushedToZero()
    {
        // all four were 0
        ShouldBeExactly(Eval("Math.asinh(1e-300)"), 1e-300, "Math.asinh(1e-300)");
        ShouldBeExactly(Eval("Math.atanh(1e-300)"), 1e-300, "Math.atanh(1e-300)");
        ShouldBeExactly(Eval("Math.expm1(1e-300)"), 1e-300, "Math.expm1(1e-300)");
        ShouldBeExactly(Eval("Math.log1p(1e-300)"), 1e-300, "Math.log1p(1e-300)");
    }

    [Test]
    public void Log1pIsAccurateNearZero()
    {
        // was 1.110223024625156e-15, an 11% error
        ShouldBeExactly(Eval("Math.log1p(1e-15)"), 9.999999999999995e-16, "Math.log1p(1e-15)");
    }

    [Test]
    public void CbrtIsNotComputedThroughPow()
    {
        // was 1.0000000000000128e-100, some 58 ULP out
        ShouldBeExactly(Eval("Math.cbrt(1e-300)"), 1e-100, "Math.cbrt(1e-300)");
    }

    // ---------------------------------------------------------------------------------------
    // Ordinary values, domain edges and every special case ECMA-262 21.3.2 fixes.
    // ---------------------------------------------------------------------------------------

    [TestCaseSource(nameof(AcoshValues))]
    public void Acosh(string argument, double expected)
        => ShouldBeExactly(Eval($"Math.acosh({argument})"), expected, $"Math.acosh({argument})");

    [TestCaseSource(nameof(AsinhValues))]
    public void Asinh(string argument, double expected)
        => ShouldBeExactly(Eval($"Math.asinh({argument})"), expected, $"Math.asinh({argument})");

    [TestCaseSource(nameof(AtanhValues))]
    public void Atanh(string argument, double expected)
        => ShouldBeExactly(Eval($"Math.atanh({argument})"), expected, $"Math.atanh({argument})");

    [TestCaseSource(nameof(CbrtValues))]
    public void Cbrt(string argument, double expected)
        => ShouldBeExactly(Eval($"Math.cbrt({argument})"), expected, $"Math.cbrt({argument})");

    [TestCaseSource(nameof(Expm1Values))]
    public void Expm1(string argument, double expected)
        => ShouldBeExactly(Eval($"Math.expm1({argument})"), expected, $"Math.expm1({argument})");

    [TestCaseSource(nameof(Log1pValues))]
    public void Log1p(string argument, double expected)
        => ShouldBeExactly(Eval($"Math.log1p({argument})"), expected, $"Math.log1p({argument})");

    // ---------------------------------------------------------------------------------------
    // test262 reference values, sampled from test/staging/sm/Math/*-approx.js. Those files are
    // excluded from nothing any more; this keeps a representative slice inside the unit suite.
    // ---------------------------------------------------------------------------------------

    [TestCaseSource(nameof(AcoshReferenceTable))]
    public void AcoshMatchesTest262References(string argument, double expected, int tolerance)
        => ShouldBeNear("Math.acosh", argument, expected, tolerance);

    [TestCaseSource(nameof(AsinhReferenceTable))]
    public void AsinhMatchesTest262References(string argument, double expected, int tolerance)
        => ShouldBeNear("Math.asinh", argument, expected, tolerance);

    [TestCaseSource(nameof(AtanhReferenceTable))]
    public void AtanhMatchesTest262References(string argument, double expected, int tolerance)
        => ShouldBeNear("Math.atanh", argument, expected, tolerance);

    [TestCaseSource(nameof(CbrtReferenceTable))]
    public void CbrtMatchesTest262References(string argument, double expected, int tolerance)
        => ShouldBeNear("Math.cbrt", argument, expected, tolerance);

    [TestCaseSource(nameof(Expm1ReferenceTable))]
    public void Expm1MatchesTest262References(string argument, double expected, int tolerance)
        => ShouldBeNear("Math.expm1", argument, expected, tolerance);

    [TestCaseSource(nameof(Log1pReferenceTable))]
    public void Log1pMatchesTest262References(string argument, double expected, int tolerance)
        => ShouldBeNear("Math.log1p", argument, expected, tolerance);

    private void ShouldBeNear(string function, string argument, double expected, int tolerance)
    {
        var actual = Eval($"{function}({argument})");
        UlpDistance(actual, expected).Should().BeLessThanOrEqualTo(
            tolerance,
            $"{function}({argument}) should be within {tolerance} ULP of {expected:R} but was {actual:R}");
    }

    // ---------------------------------------------------------------------------------------
    // Round trips. Each of these is a loop test262's *-approx.js files also run.
    // ---------------------------------------------------------------------------------------

    // Tolerances here are a couple of ULP wider than measured, because the inner Math.cosh /
    // Math.sinh / Math.tanh are still BCL calls and may differ by a ULP between operating systems.
    // Each range was checked to survive a +-1 ULP perturbation of the inner function; acosh starts
    // at |x| = 1 because inverting cosh is arbitrarily ill-conditioned as x approaches 0 (cosh(0.2)
    // is 1.0200667..., where an eight-ULP round trip error is expected and test262 allows nine).

    [Test]
    public void AcoshInvertsCosh()
        => AssertRoundTrip(
            """
            (function () {
                var out = [];
                for (var i = 0; i <= 100; i++) {
                    var x = (i - 50) / 5;
                    if (Math.abs(x) < 1) continue;
                    out.push(Math.acosh(Math.cosh(x)), Math.abs(x));
                }
                for (var i = 1; i < 20; i++) {
                    out.push(Math.acosh(Math.cosh(i)), i);
                }
                return out;
            })()
            """,
            tolerance: 6);

    [Test]
    public void AsinhInvertsSinh()
        => AssertRoundTrip(
            """
            (function () {
                var out = [];
                for (var i = 0; i <= 80; i++) {
                    var x = (i - 40) / 4;
                    out.push(Math.asinh(Math.sinh(x)), x);
                }
                for (var i = -20; i < 20; i++) {
                    out.push(Math.asinh(Math.sinh(i)), i);
                }
                return out;
            })()
            """,
            tolerance: 4);

    [Test]
    public void AtanhInvertsTanh()
        => AssertRoundTrip(
            """
            (function () {
                var out = [];
                for (var i = -1; i < 1; i += 0.05) {
                    out.push(Math.atanh(Math.tanh(i)), i);
                }
                return out;
            })()
            """,
            tolerance: 5);

    [Test]
    public void CbrtInvertsCubing()
        => AssertRoundTrip(
            """
            (function () {
                var out = [];
                for (var i = -30; i <= 30; i++) {
                    var x = i / 3;
                    out.push(Math.cbrt(x * x * x), x);
                }
                return out;
            })()
            """,
            tolerance: 2);

    [Test]
    public void Expm1AndLog1pInvertEachOther()
        => AssertRoundTrip(
            """
            (function () {
                var out = [];
                for (var i = -8; i <= 8; i++) {
                    var x = i / 8;
                    out.push(Math.log1p(Math.expm1(x)), x);
                }
                for (var i = -7; i <= 40; i++) {
                    var x = i / 8;
                    out.push(Math.expm1(Math.log1p(x)), x);
                }
                return out;
            })()
            """,
            tolerance: 4);

    private void AssertRoundTrip(string script, int tolerance)
    {
        var pairs = _engine.Evaluate(script).AsArray();
        var length = (int) pairs.Length;
        length.Should().BeGreaterThan(0);

        for (var i = 0; i < length; i += 2)
        {
            var actual = pairs[i].AsNumber();
            var expected = pairs[i + 1].AsNumber();
            UlpDistance(actual, expected).Should().BeLessThanOrEqualTo(
                tolerance,
                $"round trip #{i / 2} should be within {tolerance} ULP of {expected:R} but was {actual:R}");
        }
    }

    public static TestCases<string, double> AcoshValues =>
        new()
        {
            { "1.0", 0.0 },
            { "1.0000000000000002", 2.1073424255447017e-08 }, // 1 ULP from correctly rounded 2.1073424255447014e-08
            { "1.0000000001", 1.4142136208675862e-05 },
            { "1.0009765625", 0.044190578083110096 },
            { "1.25", 0.6931471805599453 },
            { "1.5", 0.9624236501192069 },
            { "1.9999999999999998", 1.3169578969248166 },
            { "2.0", 1.3169578969248166 }, // 1 ULP from correctly rounded 1.3169578969248168
            { "2.0000000000000004", 1.316957896924817 },
            { "2.0000001", 1.3169579546598416 },
            { "3.0", 1.7627471740390859 }, // 1 ULP from correctly rounded 1.762747174039086
            { "4.0", 2.0634370688955608 }, // 1 ULP from correctly rounded 2.0634370688955603
            { "10.0", 2.993222846126381 },
            { "100.0", 5.298292365610484 }, // 1 ULP from correctly rounded 5.298292365610485
            { "12345.678", 10.114208500211527 },
            { "100000000.0", 19.11382792451231 },
            { "268435456.0", 20.10126823623841 }, // 1 ULP from correctly rounded 20.101268236238415
            { "268435456.00000006", 20.10126823623841 }, // 1 ULP from correctly rounded 20.101268236238415
            { "536870912.0", 20.79441541679836 },
            { "1000000000000000.0", 35.23192357547063 },
            { "1e+100", 230.95165647996453 },
            { "1e+300", 691.4686750787736 }, // 1 ULP from correctly rounded 691.4686750787737
            { "1.7976931348623157e+308", 710.4758600739439 }, // 1 ULP from correctly rounded 710.475860073944
            { "Infinity", double.PositiveInfinity },
            { "NaN", double.NaN },
            { "0", double.NaN },
            { "-0", double.NaN },
            { "-1.0", double.NaN },
            { "0.9999999999999999", double.NaN },
            { "5e-324", double.NaN },
            { "-5e-324", double.NaN },
            { "-1e+300", double.NaN },
            { "-Infinity", double.NaN },
        };

    public static TestCases<string, double> AsinhValues =>
        new()
        {
            { "0", 0.0 },
            { "-0", -0.0 },
            { "5e-324", 5e-324 },
            { "-5e-324", -5e-324 },
            { "1e-300", 1e-300 },
            { "-1e-300", -1e-300 },
            { "1.862645149230957e-09", 1.862645149230957e-09 },
            { "3.725290298461914e-09", 3.725290298461914e-09 },
            { "3.7252902984619136e-09", 3.7252902984619136e-09 },
            { "1e-10", 1e-10 },
            { "0.25", 0.24746646154726346 },
            { "0.5", 0.48121182505960347 },
            { "1.0", 0.881373587019543 },
            { "1.5", 1.1947632172871094 }, // 1 ULP from correctly rounded 1.1947632172871092
            { "1.9999999999999998", 1.4436354751788103 },
            { "2.0", 1.4436354751788103 },
            { "2.0000000000000004", 1.4436354751788105 },
            { "3.0", 1.8184464592320668 },
            { "10.0", 2.99822295029797 },
            { "100.0", 5.298342365610589 },
            { "12345.678", 10.114208503492028 },
            { "100000000.0", 19.11382792451231 },
            { "268435456.0", 20.101268236238415 },
            { "268435456.00000006", 20.101268236238415 },
            { "536870912.0", 20.79441541679836 },
            { "1000000000000000.0", 35.23192357547063 },
            { "1e+100", 230.95165647996453 },
            { "1e+300", 691.4686750787736 }, // 1 ULP from correctly rounded 691.4686750787737
            { "1.7976931348623157e+308", 710.4758600739439 }, // 1 ULP from correctly rounded 710.475860073944
            { "Infinity", double.PositiveInfinity },
            { "-Infinity", double.NegativeInfinity },
            { "NaN", double.NaN },
            { "-0.5", -0.48121182505960347 },
            { "-1.0", -0.881373587019543 },
            { "-2.5", -1.6472311463710958 },
            { "-100.0", -5.298342365610589 },
            { "-1e+300", -691.4686750787736 }, // 1 ULP from correctly rounded -691.4686750787737
        };

    public static TestCases<string, double> AtanhValues =>
        new()
        {
            { "0", 0.0 },
            { "-0", -0.0 },
            { "5e-324", 5e-324 },
            { "-5e-324", -5e-324 },
            { "1e-300", 1e-300 },
            { "-1e-300", -1e-300 },
            { "1.862645149230957e-09", 1.862645149230957e-09 },
            { "3.725290298461914e-09", 3.725290298461914e-09 },
            { "3.7252902984619136e-09", 3.7252902984619136e-09 },
            { "1e-10", 1e-10 },
            { "0.125", 0.12565721414045303 },
            { "0.25", 0.25541281188299536 },
            { "0.49999999999999994", 0.5493061443340548 },
            { "0.5", 0.5493061443340548 }, // 1 ULP from correctly rounded 0.5493061443340549
            { "0.5000000000000001", 0.549306144334055 },
            { "0.75", 0.9729550745276566 },
            { "0.9", 1.4722194895832204 },
            { "0.99", 2.6466524123622457 },
            { "0.999999", 7.254328619247669 },
            { "0.9999999999999999", 18.714973875118524 },
            { "1.0", double.PositiveInfinity },
            { "-1.0", double.NegativeInfinity },
            { "-0.5", -0.5493061443340548 }, // 1 ULP from correctly rounded -0.5493061443340549
            { "-0.9999999999999999", -18.714973875118524 },
            { "-0.25", -0.25541281188299536 },
            { "1.0000000000000002", double.NaN },
            { "-1.0000000000000002", double.NaN },
            { "2.0", double.NaN },
            { "-2.0", double.NaN },
            { "1e+300", double.NaN },
            { "Infinity", double.NaN },
            { "-Infinity", double.NaN },
            { "NaN", double.NaN },
        };

    public static TestCases<string, double> CbrtValues =>
        new()
        {
            { "0", 0.0 },
            { "-0", -0.0 },
            { "5e-324", 1.7031839360032603e-108 },
            { "-5e-324", -1.7031839360032603e-108 },
            { "1e-320", 2.1544266950262728e-107 },
            { "1e-300", 1e-100 },
            { "-1e-300", -1e-100 },
            { "2.2250738585072014e-308", 2.812644285236262e-103 },
            { "2.225073858507201e-308", 2.8126442852362615e-103 },
            { "1e-100", 4.641588833612779e-34 },
            { "0.001", 0.1 },
            { "0.125", 0.5 },
            { "0.5", 0.7937005259840998 },
            { "1.0", 1.0 },
            { "-1.0", -1.0 },
            { "2.0", 1.2599210498948732 },
            { "2.718281828459045", 1.3956124250860895 },
            { "3.141592653589793", 1.4645918875615231 },
            { "0.6931471805599453", 0.8849970445005177 },
            { "1.4142135623730951", 1.122462048309373 },
            { "8.0", 2.0 },
            { "27.0", 3.0 },
            { "64.0", 4.0 },
            { "1000.0", 10.0 },
            { "1000000.0", 100.0 },
            { "12345.678", 23.11204184662531 }, // 1 ULP from correctly rounded 23.112041846625313
            { "1e+100", 2.1544346900318838e+33 },
            { "1e+300", 1e+100 },
            { "1.7976931348623157e+308", 5.643803094122362e+102 },
            { "-1.7976931348623157e+308", -5.643803094122362e+102 },
            { "Infinity", double.PositiveInfinity },
            { "-Infinity", double.NegativeInfinity },
            { "NaN", double.NaN },
            { "-8.0", -2.0 },
            { "-27.0", -3.0 },
            { "-1e+300", -1e+100 },
        };

    public static TestCases<string, double> Expm1Values =>
        new()
        {
            { "0", 0.0 },
            { "-0", -0.0 },
            { "5e-324", 5e-324 },
            { "-5e-324", -5e-324 },
            { "1e-300", 1e-300 },
            { "-1e-300", -1e-300 },
            { "2.7755575615628914e-17", 2.7755575615628914e-17 },
            { "5.551115123125783e-17", 5.551115123125783e-17 },
            { "5.551115123125782e-17", 5.551115123125782e-17 },
            { "1e-14", 1.000000000000005e-14 },
            { "1e-10", 1.00000000005e-10 },
            { "1e-06", 1.0000005000001665e-06 },
            { "0.1", 0.10517091807564763 },
            { "0.25", 0.2840254166877415 },
            { "0.34657359027997264", 0.41421356237309503 },
            { "0.3465735902799727", 0.41421356237309515 }, // 1 ULP from correctly rounded 0.4142135623730951
            { "0.5", 0.6487212707001282 },
            { "0.75", 1.1170000166126748 }, // 1 ULP from correctly rounded 1.1170000166126746
            { "1.0", 1.718281828459045 }, // 1 ULP from correctly rounded 1.7182818284590453
            { "1.0397207708399179", 1.8284271247461898 },
            { "1.039720770839918", 1.8284271247461905 },
            { "1.5", 3.481689070338065 },
            { "2.0", 6.38905609893065 },
            { "5.0", 147.4131591025766 },
            { "10.0", 22025.465794806718 },
            { "100.0", 2.6881171418161356e+43 },
            { "216.85489905212918", 1.5096839294759838e+94 },
            { "700.0", 1.0142320547350045e+304 },
            { "709.0", 8.218407461554972e+307 },
            { "709.782712893384", 1.7976931348622732e+308 },
            { "709.7827128933841", double.PositiveInfinity },
            { "710.0", double.PositiveInfinity },
            { "1e+300", double.PositiveInfinity },
            { "Infinity", double.PositiveInfinity },
            { "-0.1", -0.09516258196404043 },
            { "-0.25", -0.22119921692859512 },
            { "-0.5", -0.3934693402873666 },
            { "-0.6931471805599453", -0.5 },
            { "-1.0", -0.6321205588285577 },
            { "-2.0", -0.8646647167633873 },
            { "-5.0", -0.9932620530009145 },
            { "-10.0", -0.9999546000702375 },
            { "-38.81622659593048", -1.0 },
            { "-38.9", -1.0 },
            { "-50.0", -1.0 },
            { "-100.0", -1.0 },
            { "-745.0", -1.0 },
            { "-1e+300", -1.0 },
            { "-Infinity", -1.0 },
            { "NaN", double.NaN },
        };

    public static TestCases<string, double> Log1pValues =>
        new()
        {
            { "0", 0.0 },
            { "-0", -0.0 },
            { "5e-324", 5e-324 },
            { "-5e-324", -5e-324 },
            { "1e-300", 1e-300 },
            { "-1e-300", -1e-300 },
            { "2.7755575615628914e-17", 2.7755575615628914e-17 },
            { "5.551115123125783e-17", 5.551115123125783e-17 },
            { "5.551115123125782e-17", 5.551115123125782e-17 },
            { "9.313225746154785e-10", 9.313225741817976e-10 },
            { "1.862645149230957e-09", 1.8626451474962336e-09 },
            { "1.8626451492309568e-09", 1.8626451474962333e-09 },
            { "1e-15", 9.999999999999995e-16 },
            { "1e-09", 9.999999995e-10 },
            { "1e-06", 9.999995000003334e-07 },
            { "0.1", 0.09531017980432487 },
            { "0.25", 0.22314355131420976 },
            { "0.4142135623730951", 0.3465735902799727 },
            { "0.41421356237309515", 0.3465735902799727 },
            { "0.5", 0.4054651081081644 },
            { "1.0", 0.6931471805599453 },
            { "2.0", 1.0986122886681096 }, // 1 ULP from correctly rounded 1.0986122886681098
            { "10.0", 2.3978952727983707 },
            { "1000000.0", 13.815511557963774 },
            { "10000000000.0", 23.025850930040455 },
            { "9007199254740992.0", 36.7368005696771 },
            { "1.8014398509481984e+16", 37.42994775023705 },
            { "1e+100", 230.25850929940458 },
            { "1e+300", 690.7755278982137 },
            { "1.7976931348623157e+308", 709.782712893384 },
            { "Infinity", double.PositiveInfinity },
            { "-0.1", -0.10536051565782631 },
            { "-0.25", -0.2876820724517809 },
            { "-0.2928932188134524", -0.3465735902799726 },
            { "-0.29289321881345237", -0.34657359027997253 },
            { "-0.5", -0.6931471805599453 },
            { "-0.75", -1.3862943611198906 },
            { "-0.9", -2.302585092994046 },
            { "-0.999999", -13.815510557935518 },
            { "-0.9999999999999999", -36.7368005696771 },
            { "-1.0", double.NegativeInfinity },
            { "-1.0000000000000002", double.NaN },
            { "-1.1", double.NaN },
            { "-2.0", double.NaN },
            { "-1e+300", double.NaN },
            { "-Infinity", double.NaN },
            { "NaN", double.NaN },
        };

    public static TestCases<string, double, int> AcoshReferenceTable =>
        new()
        {
            { "1.0000014305114746", 0.0016914556651292944, 9 },
            { "1.024169921875", 0.21942279004958354, 9 },
            { "16.88458251953125", 3.51867003468025, 9 },
            { "57.119781494140625", 4.73822104001982, 9 },
            { "86.42214965820312", 5.152357710985635, 9 },
            { "126.29083251953125", 5.531718947357248, 9 },
            { "169.22418212890625", 5.824362807770767, 9 },
            { "199.97052001953125", 5.991310884439669, 9 },
            { "243.202392578125", 6.187036941752032, 9 },
            { "284.3428955078125", 6.343324976847916, 9 },
            { "328.214599609375", 6.486812521370483, 9 },
            { "363.7503662109375", 6.5896131163651415, 9 },
            { "396.3114013671875", 6.675345858154136, 9 },
            { "410.8016357421875", 6.711256159037373, 9 },
            { "457.9520263671875", 6.819910421197311, 9 },
            { "479.9122314453125", 6.8667493311188395, 9 },
            { "5824.533203125", 9.36298131161099, 9 },
            { "1875817529344", 28.953212876533797, 9 },
        };

    public static TestCases<string, double, int> AsinhReferenceTable =>
        new()
        {
            { "-497.181640625", -6.902103625349695, 1 },
            { "-402.4595947265625", -6.690743430063694, 1 },
            { "-337.3883056640625", -6.514383886150781, 1 },
            { "-245.71783447265625", -6.197335184435549, 1 },
            { "-150.01629638671875", -5.703902219845274, 1 },
            { "-78.23873901367188", -5.052952927749896, 1 },
            { "1.6504814008555524e-12", 1.6504814008555524e-12, 1 },
            { "1.166764889148908e-7", 1.1667648891489053e-07, 1 },
            { "0.001962467096745968", 0.0019624658370807177, 1 },
            { "29.58606719970703", 4.080736210902826, 1 },
            { "116.044677734375", 5.447141014648796, 1 },
            { "195.23284912109375", 5.967346683696556, 1 },
            { "257.7401123046875", 6.245102704489327, 1 },
            { "307.531005859375", 6.421725738171608, 1 },
            { "378.4306640625", 6.629181796246806, 1 },
            { "457.5068359375", 6.818940201487998, 1 },
            { "716.154052734375", 7.2670429692740965, 1 },
            { "1581915832320", 28.78280496108106, 1 },
        };

    public static TestCases<string, double, int> AtanhReferenceTable =>
        new()
        {
            { "-0.9999983310699463", -6.998237084679027, 2 },
            { "-0.990715742111206", -2.6839646283308363, 2 },
            { "-0.864722728729248", -1.3117705583444539, 2 },
            { "-0.7017018795013428", -0.8706453720344796, 2 },
            { "-0.5654678344726562", -0.6408350116350283, 2 },
            { "-0.40029656887054443", -0.4240020382545707, 2 },
            { "-0.2253856658935547", -0.2293228153248168, 2 },
            { "-0.08401328325271606", -0.08421178632314608, 2 },
            { "1.6399812063916386e-11", 1.6399812063916386e-11, 2 },
            { "9.382699772686465e-7", 9.382699772689218e-07, 2 },
            { "0.008691128343343735", 0.008691347183450786, 2 },
            { "0.14749455451965332", 0.14857829980464834, 2 },
            { "0.3271782398223877", 0.3396649461699478, 2 },
            { "0.48124635219573975", 0.5246050193978663, 2 },
            { "0.5871362686157227", 0.6732844960442398, 2 },
            { "0.6437420845031738", 0.7645378650643101, 2 },
            { "0.8313877582550049", 1.1926138225701433, 2 },
            { "1e-10", 1e-10, 2 },
        };

    public static TestCases<string, double, int> CbrtReferenceTable =>
        new()
        {
            { "Math.E", 1.3956124250860895, 3 },
            { "Math.PI", 1.4645918875615231, 3 },
            { "Math.LN2", 0.8849970445005177, 3 },
            { "Math.SQRT2", 1.1224620483093728, 3 },
            { "1e-300", 1e-100, 1 },
            { "-1e-300", -1e-100, 1 },
        };

    public static TestCases<string, double, int> Expm1ReferenceTable =>
        new()
        {
            { "-1.875817529344e-70", -1.875817529344e-70, 1 },
            { "-2.114990849122478e-10", -2.1149908488988187e-10, 1 },
            { "-0.0000011039855962733358", -1.1039849868814618e-06, 1 },
            { "-0.000033870281179478836", -3.3869707587981166e-05, 1 },
            { "-0.005553725496786973", -0.005538332073473123, 1 },
            { "-0.4721357117742938", -0.3763311320344197, 1 },
            { "1.875817529344e-70", 1.875817529344e-70, 1 },
            { "7.09962844069878e-15", 7.099628440698805e-15, 1 },
            { "2.114990849122478e-10", 2.1149908493461373e-10, 1 },
            { "0.0000011039855962733358", 1.1039862056656584e-06, 1 },
            { "0.000033870281179478836", 3.387085478392845e-05, 1 },
            { "0.005553725496786973", 0.005569176019645543, 1 },
            { "0.4721357117742938", 0.6034149712523235, 1 },
            { "3.0693960800487883", 20.528897017773147, 1 },
            { "7.4227656046482595", 1672.6557833191303, 1 },
            { "20.11881628179155", 546375092.2355127, 34 },
            { "46.43974518513109", 1.4740936483807671e+20, 34 },
            { "216.85489905212918", 1.5096839294759775e+94, 34 },
        };

    public static TestCases<string, double, int> Log1pReferenceTable =>
        new()
        {
            { "1.875817529344e-70", 1.875817529344e-70, 1 },
            { "6.261923313140869e-30", 6.261923313140869e-30, 1 },
            { "7.09962844069878e-15", 7.099628440698755e-15, 1 },
            { "1.3671879628418538e-12", 1.3671879628409192e-12, 1 },
            { "2.114990849122478e-10", 2.1149908488988187e-10, 1 },
            { "1.6900931765206906e-8", 1.690093162238616e-08, 1 },
            { "0.0000709962844069878", 7.099376429006658e-05, 1 },
            { "0.0016793412882520897", 0.00167793277137076, 1 },
            { "0.011404608812881634", 0.011340066517988035, 1 },
        };
}
