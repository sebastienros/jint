// ====================================================
// Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
//
// Developed at SunSoft/SunPro, a Sun Microsystems, Inc. business.
// Permission to use, copy, modify, and distribute this
// software is freely granted, provided that this notice
// is preserved.
// ====================================================
//
// This file is a C# port of the following files from FreeBSD's libm (msun),
// taken from https://github.com/freebsd/freebsd-src/tree/main/lib/msun/src at
// commit aab7bd4d9903b9da0a7474aab47c003c904ef3dd (2026-08-16):
//
//   e_acosh.c   0dd5a5603e7a33d976f8e6015620bbc79839c609
//   s_asinh.c   0dd5a5603e7a33d976f8e6015620bbc79839c609
//   e_atanh.c   0dd5a5603e7a33d976f8e6015620bbc79839c609
//   s_cbrt.c    f887d0215fb48e682acccf4cb95f3794974e1a9d  (optimized by Bruce D. Evans)
//   s_expm1.c   0dd5a5603e7a33d976f8e6015620bbc79839c609
//   s_log1p.c   0dd5a5603e7a33d976f8e6015620bbc79839c609
//   e_log.c     0dd5a5603e7a33d976f8e6015620bbc79839c609
//
// The structure, the magic constants and the branch thresholds are those of the
// originals; only the word-access idioms (EXTRACT_WORDS / INSERT_WORDS /
// SET_HIGH_WORD, which the C sources spell with a union) are rewritten in terms
// of BitConverter, and the C tricks that exist purely to raise the IEEE inexact
// flag are dropped because .NET exposes no floating-point status word. Every such
// deviation is called out at its site and none of them changes a returned value.
//
// The only BCL calls left on these paths are Math.Sqrt and Math.Abs. IEEE 754
// mandates a correctly rounded square root and defines abs as a sign-bit clear, so
// unlike log/exp/pow neither of them can differ between runtimes: the results below
// are bit-identical on every target framework and every operating system.

using System.Runtime.CompilerServices;

namespace Jint.Native.Math;

/// <summary>
/// The transcendental functions ECMA-262 21.3.2 recommends be implemented "using the approximation
/// algorithms for IEEE 754-2019 arithmetic contained in fdlibm".
/// <para>
/// These are not delegated to the BCL. <c>Math.Acosh</c>/<c>Asinh</c>/<c>Atanh</c>/<c>Cbrt</c>
/// resolve to the platform libm, whose last-place results differ between Windows, Linux and macOS,
/// and the reference tables Jint is measured against (test262's <c>staging/sm/Math/*-approx.js</c>)
/// leave no ULP of margin. <c>double.ExpM1</c> and <c>double.LogP1</c> are worse than that: they are
/// literally <c>Exp(x) - 1</c> and <c>Log(x + 1)</c>, which is exactly the cancellation both
/// functions exist to avoid. Porting fdlibm makes the answer identical on every target framework and
/// every operating system, which is what a conformance suite requires.
/// </para>
/// </summary>
internal static class Fdlibm
{
    // 0x3FE62E42FEFA39EF
    private const double Ln2 = 6.93147180559945286227e-01;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HighWord(double x) => (int) (BitConverter.DoubleToInt64Bits(x) >> 32);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LowWord(double x) => (uint) BitConverter.DoubleToInt64Bits(x);

    /// <summary>Replaces the high 32 bits of <paramref name="x"/>, keeping the low 32.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SetHighWord(double x, uint high)
        => BitConverter.Int64BitsToDouble((long) high << 32 | (uint) BitConverter.DoubleToInt64Bits(x));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FromWords(uint high, uint low)
        => BitConverter.Int64BitsToDouble((long) high << 32 | low);

    private const double Two54 = 1.80143985094819840000e+16; // 0x4350000000000000

    // The log/log1p minimax polynomial; msun spells these Lg1..Lg7 in e_log.c and Lp1..Lp7 in
    // s_log1p.c, with identical values.
    private const double Lg1 = 6.666666666666735130e-01; // 0x3FE5555555555593
    private const double Lg2 = 3.999999999940941908e-01; // 0x3FD999999997FA04
    private const double Lg3 = 2.857142874366239149e-01; // 0x3FD2492494229359
    private const double Lg4 = 2.222219843214978396e-01; // 0x3FCC71C51D8E78AF
    private const double Lg5 = 1.818357216161805012e-01; // 0x3FC7466496CB03DE
    private const double Lg6 = 1.531383769920937332e-01; // 0x3FC39A09D078C69F
    private const double Lg7 = 1.479819860511658591e-01; // 0x3FC2F112DF3E5244

    /// <summary>
    /// log(x), ported from msun's <c>e_log.c</c>.
    /// <para>
    /// This is <b>not</b> what <c>Math.log</c> is implemented with — that stays on the BCL. It
    /// exists because <see cref="Acosh"/> and <see cref="Asinh"/> reduce their large-argument cases
    /// to a natural logarithm, and a platform <c>log</c> that is 1 ULP off in the wrong direction
    /// takes the composed result 2 ULP off its reference value. Measured against the test262
    /// <c>staging/sm/Math</c> tables, 15 of the 695 acosh/asinh assertions that reach a logarithm
    /// fail somewhere inside a ±1 ULP envelope around a correctly rounded <c>log</c>, which is
    /// precisely the latitude a libm has. Keeping the logarithm in-tree is what makes those two
    /// functions answer identically on every operating system.
    /// </para>
    /// </summary>
    internal static double Log(double x)
    {
        var hx = HighWord(x);
        var lx = LowWord(x);

        var k = 0;
        if (hx < 0x00100000)
        {
            // x < 2**-1022
            if (((hx & 0x7fffffff) | (int) lx) == 0)
            {
                return double.NegativeInfinity; // log(+-0) = -inf
            }

            if (hx < 0)
            {
                return double.NaN; // log(-#) = NaN
            }

            k -= 54;
            x *= Two54; // subnormal number, scale up x
            hx = HighWord(x);
        }

        if (hx >= 0x7ff00000)
        {
            return x + x;
        }

        k += (hx >> 20) - 1023;
        hx &= 0x000fffff;
        var i = (hx + 0x95f64) & 0x100000;
        x = SetHighWord(x, (uint) (hx | (i ^ 0x3ff00000))); // normalize x or x/2
        k += i >> 20;
        var f = x - 1.0;

        double dk;
        if ((0x000fffff & (2 + hx)) < 3)
        {
            // -2**-20 <= f < 2**-20
            if (f == 0.0)
            {
                if (k == 0)
                {
                    return 0.0;
                }

                dk = k;
                return dk * Ln2Hi + dk * Ln2Lo;
            }

            var r0 = f * f * (0.5 - 0.33333333333333333 * f);
            if (k == 0)
            {
                return f - r0;
            }

            dk = k;
            return dk * Ln2Hi - ((r0 - dk * Ln2Lo) - f);
        }

        var s = f / (2.0 + f);
        dk = k;
        var z = s * s;
        i = hx - 0x6147a;
        var w = z * z;
        var j = 0x6b851 - hx;
        var t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
        var t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
        i |= j;
        var r = t2 + t1;

        if (i > 0)
        {
            var hfsq = 0.5 * f * f;
            if (k == 0)
            {
                return f - (hfsq - s * (hfsq + r));
            }

            return dk * Ln2Hi - ((hfsq - (s * (hfsq + r) + dk * Ln2Lo)) - f);
        }

        if (k == 0)
        {
            return f - s * (f - r);
        }

        return dk * Ln2Hi - ((s * (f - r) - dk * Ln2Lo) - f);
    }

    /// <summary>
    /// acosh(x), ported from msun's <c>e_acosh.c</c>.
    /// <code>
    ///   acosh(x) := log(x)+ln2,                       if x is large; else
    ///   acosh(x) := log(2x-1/(sqrt(x*x-1)+x))         if x>2; else
    ///   acosh(x) := log1p(t+sqrt(2.0*t+t*t))          where t=x-1.
    /// </code>
    /// Every ECMA-262 21.3.2.3 special case falls out of the branches below: NaN and x&lt;1 (which
    /// includes ±0 and every negative) give NaN, +∞ gives +∞, and 1 gives +0.
    /// </summary>
    internal static double Acosh(double x)
    {
        var hx = HighWord(x);
        var lx = LowWord(x);

        if (hx < 0x3ff00000)
        {
            // x < 1 (a signed comparison, so this also catches every negative and NaN with the
            // sign bit set). fdlibm computes (x-x)/(x-x) to raise invalid; .NET has no flag to
            // raise, and the value is NaN either way.
            return double.NaN;
        }

        if (hx >= 0x41b00000)
        {
            // x > 2**28
            if (hx >= 0x7ff00000)
            {
                return x + x; // x is +inf or NaN
            }

            return Log(x) + Ln2; // acosh(huge)=log(2x)
        }

        if (((hx - 0x3ff00000) | (int) lx) == 0)
        {
            return 0.0; // acosh(1) = 0
        }

        if (hx > 0x40000000)
        {
            // 2**28 > x > 2
            var t = x * x;
            return Log(2.0 * x - 1.0 / (x + System.Math.Sqrt(t - 1.0)));
        }

        // 1 < x < 2
        var u = x - 1.0;
        return Log1p(u + System.Math.Sqrt(2.0 * u + u * u));
    }

    /// <summary>
    /// asinh(x), ported from msun's <c>s_asinh.c</c>.
    /// <code>
    ///   asinh(x) := x                                              if 1+x*x=1,
    ///            := sign(x)*(log(x)+ln2)                           for large |x|, else
    ///            := sign(x)*log(2|x|+1/(|x|+sqrt(x*x+1)))          if |x|>2, else
    ///            := sign(x)*log1p(|x| + x^2/(1 + sqrt(1+x^2)))
    /// </code>
    /// ECMA-262 21.3.2.5 returns x unchanged for NaN, ±∞ and ±0; the first two branches below do
    /// exactly that.
    /// </summary>
    internal static double Asinh(double x)
    {
        var hx = HighWord(x);
        var ix = hx & 0x7fffffff;

        if (ix >= 0x7ff00000)
        {
            return x + x; // x is inf or NaN
        }

        if (ix < 0x3e300000)
        {
            // |x| < 2**-28. fdlibm guards this with (huge+x>one), which only exists to raise
            // inexact; the value returned is x, sign of zero included.
            return x;
        }

        double w;
        if (ix > 0x41b00000)
        {
            // |x| > 2**28
            w = Log(System.Math.Abs(x)) + Ln2;
        }
        else if (ix > 0x40000000)
        {
            // 2**28 > |x| > 2.0
            var t = System.Math.Abs(x);
            w = Log(2.0 * t + 1.0 / (System.Math.Sqrt(x * x + 1.0) + t));
        }
        else
        {
            // 2.0 > |x| > 2**-28
            var t = x * x;
            w = Log1p(System.Math.Abs(x) + t / (1.0 + System.Math.Sqrt(1.0 + t)));
        }

        return hx > 0 ? w : -w;
    }

    /// <summary>
    /// atanh(x), ported from msun's <c>e_atanh.c</c>.
    /// <code>
    ///   for x &gt;= 0.5   atanh(x) = 0.5 * log1p(2 * x/(1-x))
    ///   for x &lt;  0.5   atanh(x) = 0.5 * log1p(2x + 2x*x/(1-x))
    /// </code>
    /// ECMA-262 21.3.2.7 wants NaN for NaN and for |x|&gt;1, ±∞ for ±1 and x itself for ±0; the
    /// three leading branches cover all four.
    /// </summary>
    internal static double Atanh(double x)
    {
        var hx = HighWord(x);
        var lx = LowWord(x);
        var ix = hx & 0x7fffffff;

        // |x| > 1, which also catches NaN. fdlibm writes this as
        // (ix|((lx|(-lx))>>31)) > 0x3ff00000 to fold the "low word non-zero" test into the
        // comparison; spelled out, it is the same predicate.
        if (ix > 0x3ff00000 || (ix == 0x3ff00000 && lx != 0))
        {
            return double.NaN;
        }

        if (ix == 0x3ff00000)
        {
            return x / 0.0; // atanh(+-1) = +-inf
        }

        if (ix < 0x3e300000)
        {
            // |x| < 2**-28, again only guarded in C to raise inexact.
            return x;
        }

        x = SetHighWord(x, (uint) ix); // x = |x|

        double t;
        if (ix < 0x3fe00000)
        {
            // x < 0.5
            t = x + x;
            t = 0.5 * Log1p(t + t * x / (1.0 - x));
        }
        else
        {
            t = 0.5 * Log1p((x + x) / (1.0 - x));
        }

        return hx >= 0 ? t : -t;
    }

    // B1 = (1023-1023/3-0.03306235651)*2**20
    private const uint CbrtB1 = 715094163;

    // B2 = (1023-1023/3-54/3-0.03306235651)*2**20
    private const uint CbrtB2 = 696219795;

    // |1/cbrt(x) - p(x)| < 2**-23.5 (~[-7.93e-8, 7.929e-8]).
    private const double CbrtP0 = 1.87595182427177009643;   // 0x3ffe03e60f61e692
    private const double CbrtP1 = -1.88497979543377169875;  // 0xbffe28e092f02420
    private const double CbrtP2 = 1.621429720105354466140;  // 0x3ff9f1604a49d6c2
    private const double CbrtP3 = -0.758397934778766047437; // 0xbfe844cbbee751d9
    private const double CbrtP4 = 0.145996192886612446982;  // 0x3fc2b000d4e4edd7

    /// <summary>
    /// cbrt(x), ported from msun's <c>s_cbrt.c</c>: a 5-bit estimate straight out of the exponent
    /// bits, a degree-4 polynomial taking it to 23 bits, then one Halley step to 53 bits with an
    /// error below 0.667 ULP.
    /// <para>
    /// ECMA-262 21.3.2.9 returns x unchanged for NaN, ±∞ and ±0, which is what the two leading
    /// branches do.
    /// </para>
    /// </summary>
    internal static double Cbrt(double x)
    {
        var hx = HighWord(x);
        var low = LowWord(x);
        var sign = (uint) hx & 0x80000000;
        hx ^= (int) sign;

        if (hx >= 0x7ff00000)
        {
            return x + x; // cbrt(NaN,INF) is itself
        }

        // Rough cbrt to 5 bits:
        //    cbrt(2**e*(1+m) ~= 2**(e/3)*(1+(e%3+m)/3)
        // where e is integral and >= 0, m is real and in [0, 1), and "/" and "%" are integer
        // division and modulus with rounding towards minus infinity. The RHS is always >= the LHS
        // and has a maximum relative error of about 1 in 16. Adding a bias of -0.03306235651 to the
        // (e%3+m)/3 term reduces the error to about 1 in 32. With the IEEE floating point
        // representation, for finite positive normal values, ordinary integer division of the value
        // in bits magically gives almost exactly the RHS of the above provided we first subtract the
        // exponent bias (1023 for doubles) and later add it back. We do the subtraction virtually to
        // keep e >= 0 so that ordinary integer division rounds towards minus infinity; this is also
        // efficient.
        double t;
        if (hx < 0x00100000)
        {
            // zero or subnormal?
            if ((hx | (int) low) == 0)
            {
                return x; // cbrt(0) is itself
            }

            t = FromWords(0x43500000, 0) * x; // t = 2**54 * x
            var high = (uint) HighWord(t);
            t = FromWords(sign | ((high & 0x7fffffff) / 3 + CbrtB2), 0);
        }
        else
        {
            t = FromWords(sign | ((uint) (hx / 3) + CbrtB1), 0);
        }

        // New cbrt to 23 bits:
        //    cbrt(x) = t*cbrt(x/t**3) ~= t*P(t**3/x)
        // where P(r) is a polynomial of degree 4 that approximates 1/cbrt(r) to within 2**-23.5 when
        // |r - 1| < 1/10. The rough approximation has produced t such than |t/cbrt(x) - 1| ~< 1/32,
        // and cubing this gives us bounds for r = t**3/x.
        var r = t * t * (t / x);
        t = t * ((CbrtP0 + r * (CbrtP1 + r * CbrtP2)) + ((r * r) * r) * (CbrtP3 + r * CbrtP4));

        // Round t away from zero to 23 bits (sloppily except for ensuring that the result is larger
        // in magnitude than cbrt(x) but not much more than 2 23-bit ulps larger).
        t = BitConverter.Int64BitsToDouble(
            (long) (((ulong) BitConverter.DoubleToInt64Bits(t) + 0x80000000UL) & 0xffffffffc0000000UL));

        // one step Halley iteration to 53 bits with error < 0.667 ulps
        var s = t * t;    // t*t is exact
        r = x / s;        // error <= 0.5 ulps; |r| < |t|
        var w = t + t;    // t+t is exact
        r = (r - t) / (w + r); // r-t is exact; w+r ~= 3*t
        t = t + t * r;    // error <= (0.5 + 0.5/3) * ulp

        return t;
    }

    private const double Expm1OverflowThreshold = 7.09782712893383973096e+02; // 0x40862E42FEFA39EF
    private const double Ln2Hi = 6.93147180369123816490e-01;                  // 0x3fe62e42fee00000
    private const double Ln2Lo = 1.90821492927058770002e-10;                  // 0x3dea39ef35793c76
    private const double InvLn2 = 1.44269504088896338700e+00;                 // 0x3ff71547652b82fe

    // Scaled Q's: Qn_here = 2**n * Qn_above, for R(2*z) where z = hxs = x*x/2:
    private const double ExpQ1 = -3.33333333333331316428e-02; // 0xBFA11111111110F4
    private const double ExpQ2 = 1.58730158725481460165e-03;  // 0x3F5A01A019FE5585
    private const double ExpQ3 = -7.93650757867487942473e-05; // 0xBF14CE199EAADBB7
    private const double ExpQ4 = 4.00821782732936239552e-06;  // 0x3ED0CFCA86E65239
    private const double ExpQ5 = -2.01099218183624371326e-07; // 0xBE8AFDB76E09C32D

    /// <summary>
    /// expm1(x) = exp(x)-1, ported from msun's <c>s_expm1.c</c>. Argument reduction
    /// x = k*ln2 + r with |r| &lt;= 0.5*ln2, a degree-5 minimax rational in r*r, then a scale back
    /// by 2^k chosen per k so that the "-1" never cancels the significant digits away. Accurate to
    /// under 1 ULP, which is what ECMA-262 21.3.2.14's "computed in a way that is accurate even when
    /// the value of x is close to 0" asks for.
    /// <para>
    /// The special cases 21.3.2.14 lists — NaN, ±0 and +∞ returning x, -∞ returning -1 — are all
    /// produced by the filter at the top.
    /// </para>
    /// </summary>
    internal static double Expm1(double x)
    {
        var hx = (uint) HighWord(x);
        var xsb = hx & 0x80000000; // sign bit of x
        hx &= 0x7fffffff;          // high word of |x|

        // filter out huge and non-finite argument
        if (hx >= 0x4043687A)
        {
            // |x| >= 56*ln2
            if (hx >= 0x40862E42)
            {
                // |x| >= 709.78...
                if (hx >= 0x7ff00000)
                {
                    var low = LowWord(x);
                    if (((hx & 0xfffff) | low) != 0)
                    {
                        return x + x; // NaN
                    }

                    return xsb == 0 ? x : -1.0; // expm1(+-inf) = {inf,-1}
                }

                if (x > Expm1OverflowThreshold)
                {
                    return double.PositiveInfinity; // overflow
                }
            }

            if (xsb != 0)
            {
                // x < -56*ln2; fdlibm returns tiny-one, which is exactly -1.
                return -1.0;
            }
        }

        // argument reduction
        int k;
        double c = 0.0;
        if (hx > 0x3fd62e42)
        {
            double hi, lo;
            if (hx < 0x3FF0A2B2)
            {
                // 0.5 ln2 < |x| < 1.5 ln2
                if (xsb == 0)
                {
                    hi = x - Ln2Hi;
                    lo = Ln2Lo;
                    k = 1;
                }
                else
                {
                    hi = x + Ln2Hi;
                    lo = -Ln2Lo;
                    k = -1;
                }
            }
            else
            {
                k = (int) (InvLn2 * x + (xsb == 0 ? 0.5 : -0.5));
                double t0 = k;
                hi = x - t0 * Ln2Hi; // t*ln2_hi is exact here
                lo = t0 * Ln2Lo;
            }

            x = hi - lo;
            c = (hi - x) - lo;
        }
        else if (hx < 0x3c900000)
        {
            // |x| < 2**-54: fdlibm returns x - (t-(huge+x)), an identity whose only purpose is to
            // raise inexact. The value is x, sign of zero included.
            return x;
        }
        else
        {
            k = 0;
        }

        // x is now in primary range
        var hfx = 0.5 * x;
        var hxs = x * hfx;
        var r1 = 1.0 + hxs * (ExpQ1 + hxs * (ExpQ2 + hxs * (ExpQ3 + hxs * (ExpQ4 + hxs * ExpQ5))));
        var t = 3.0 - r1 * hfx;
        var e = hxs * ((r1 - t) / (6.0 - x * t));

        if (k == 0)
        {
            return x - (x * e - hxs); // c is 0
        }

        var twopk = FromWords((uint) (0x3ff + k) << 20, 0); // 2^k
        e = x * (e - c) - c;
        e -= hxs;

        if (k == -1)
        {
            return 0.5 * (x - e) - 0.5;
        }

        if (k == 1)
        {
            if (x < -0.25)
            {
                return -2.0 * (e - (x + 0.5));
            }

            return 1.0 + 2.0 * (x - e);
        }

        double y;
        if (k <= -2 || k > 56)
        {
            // suffice to return exp(x)-1
            y = 1.0 - (e - x);
            if (k == 1024)
            {
                y = y * 2.0 * 8.98846567431158e+307; // 0x1p1023
            }
            else
            {
                y = y * twopk;
            }

            return y - 1.0;
        }

        t = 1.0;
        if (k < 20)
        {
            t = SetHighWord(t, (uint) (0x3ff00000 - (0x200000 >> k))); // t = 1-2^-k
            y = t - (e - x);
            y = y * twopk;
        }
        else
        {
            t = SetHighWord(t, (uint) ((0x3ff - k) << 20)); // t = 2^-k
            y = x - (e + t);
            y += 1.0;
            y = y * twopk;
        }

        return y;
    }

    /// <summary>
    /// log1p(x) = log(1+x), ported from msun's <c>s_log1p.c</c>. 1+x is formed with an explicit
    /// correction term c so that the leading digits x carries are not lost, then log is evaluated by
    /// the usual argument reduction and odd-polynomial approximation. This is what makes
    /// ECMA-262 21.3.2.21's "accurate even when the value of x is close to 0" true — log1p(1e-300)
    /// is 1e-300, not 0.
    /// <para>
    /// The listed special cases — NaN, ±0 and +∞ returning x, -1 returning -∞, x&lt;-1 returning
    /// NaN — all come out of the branches below.
    /// </para>
    /// </summary>
    internal static double Log1p(double x)
    {
        var hx = HighWord(x);
        var ax = hx & 0x7fffffff;

        var k = 1;
        var f = 0.0;
        var hu = 0;
        var c = 0.0;

        if (hx < 0x3FDA827A)
        {
            // 1+x < sqrt(2)+
            if (ax >= 0x3ff00000)
            {
                // x <= -1.0
                if (x == -1.0)
                {
                    return double.NegativeInfinity; // fdlibm: -two54/vzero
                }

                return double.NaN; // log1p(x<-1) = NaN
            }

            if (ax < 0x3e200000)
            {
                // |x| < 2**-29
                if (ax < 0x3c900000)
                {
                    // |x| < 2**-54 (the C guard two54+x>zero only raises inexact)
                    return x;
                }

                return x - x * x * 0.5;
            }

            if (hx > 0 || hx <= unchecked((int) 0xbfd2bec4))
            {
                // sqrt(2)/2- <= 1+x < sqrt(2)+
                k = 0;
                f = x;
                hu = 1;
            }
        }

        if (hx >= 0x7ff00000)
        {
            return x + x;
        }

        if (k != 0)
        {
            double u;
            if (hx < 0x43400000)
            {
                u = 1.0 + x;
                hu = HighWord(u);
                k = (hu >> 20) - 1023;
                c = k > 0 ? 1.0 - (u - x) : x - (u - 1.0); // correction term
                c /= u;
            }
            else
            {
                u = x;
                hu = HighWord(u);
                k = (hu >> 20) - 1023;
                c = 0;
            }

            hu &= 0x000fffff;

            // The approximation to sqrt(2) used in thresholds is not critical. However, the ones
            // used above must give less strict bounds than the one here so that the k==0 case is
            // never reached from here, since here we have committed to using the correction term but
            // don't use it if k==0.
            if (hu < 0x6a09e)
            {
                u = SetHighWord(u, (uint) (hu | 0x3ff00000)); // normalize u
            }
            else
            {
                k += 1;
                u = SetHighWord(u, (uint) (hu | 0x3fe00000)); // normalize u/2
                hu = (0x00100000 - hu) >> 2;
            }

            f = u - 1.0;
        }

        var hfsq = 0.5 * f * f;
        if (hu == 0)
        {
            // |f| < 2**-20
            if (f == 0.0)
            {
                if (k == 0)
                {
                    return 0.0;
                }

                c += k * Ln2Lo;
                return k * Ln2Hi + c;
            }

            var r0 = hfsq * (1.0 - 0.66666666666666666 * f);
            if (k == 0)
            {
                return f - r0;
            }

            return k * Ln2Hi - ((r0 - (k * Ln2Lo + c)) - f);
        }

        var s = f / (2.0 + f);
        var z = s * s;
        var r = z * (Lg1 + z * (Lg2 + z * (Lg3 + z * (Lg4 + z * (Lg5 + z * (Lg6 + z * Lg7))))));

        if (k == 0)
        {
            return f - (hfsq - s * (hfsq + r));
        }

        return k * Ln2Hi - ((hfsq - (s * (hfsq + r) + (k * Ln2Lo + c))) - f);
    }
}
