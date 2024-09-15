using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jint.Native.Math;

/// <summary>
/// https://raw.githubusercontent.com/es-shims/Math.sumPrecise/main/sum.js
/// adapted from https://github.com/tc39/proposal-math-sum/blob/f4286d0a9d8525bda61be486df964bf2527c8789/polyfill/polyfill.mjs
/// https://www-2.cs.cmu.edu/afs/cs/project/quake/public/papers/robust-arithmetic.ps
/// Shewchuk's algorithm for exactly floating point addition
/// as implemented in Python's fsum: https://github.com/python/cpython/blob/48dfd74a9db9d4aa9c6f23b4a67b461e5d977173/Modules/mathmodule.c#L1359-L1474
/// adapted to handle overflow via an additional "biased" partial, representing 2**1024 times its actual value
/// </summary>
internal static class SumPrecise
{
    // exponent 11111111110, significand all 1s
    private const double MaxDouble = 1.79769313486231570815e+308; // i.e. (2**1024 - 2**(1023 - 52))

    // exponent 11111111110, significand all 1s except final 0
    private const double PenultimateDouble = 1.79769313486231550856e+308; // i.e. (2**1024 - 2 * 2**(1023 - 52))

    private const double Two1023 = 8.98846567431158e+307; // 2 ** 1023

    // exponent 11111001010, significand all 0s
    private const double MaxUlp = MaxDouble - PenultimateDouble; // 1.99584030953471981166e+292, i.e. 2**(1023 - 52)

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TwoSumResult(double Hi, double Lo);

    // prerequisite: $abs(x) >= $abs(y)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TwoSumResult TwoSum(double x, double y)
    {
        var hi = x + y;
        var lo = y - (hi - x);
        return new TwoSumResult(hi, lo);
    }

    // preconditions:
    //  - array only contains numbers
    //  - none of them are -0, NaN, or ±Infinity
    //  - all of them are finite
    [MethodImpl(512)]
    internal static double Sum(List<double> array)
    {
        double hi, lo;

        List<double> partials = [];

        var overflow = 0; // conceptually 2**1024 times this value; the final partial is biased by this amount

        var index = -1;

        // main loop
        while (index + 1 < array.Count)
        {
            var x = +array[++index];

            var actuallyUsedPartials = 0;
            for (var j = 0; j < partials.Count; j += 1)
            {
                var y = partials[j];

                if (System.Math.Abs(x) < System.Math.Abs(y))
                {
                    var tmp = x;
                    x = y;
                    y = tmp;
                }

                (hi, lo) = TwoSum(x, y);

                if (double.IsPositiveInfinity(System.Math.Abs(hi)))
                {
                    var sign = double.IsPositiveInfinity(hi) ? 1 : -1;
                    overflow += sign;

                    x = x - sign * Two1023 - sign * Two1023;
                    if (System.Math.Abs(x) < System.Math.Abs(y))
                    {
                        var tmp2 = x;
                        x = y;
                        y = tmp2;
                    }

                    var s2 = TwoSum(x, y);
                    hi = s2.Hi;
                    lo = s2.Lo;
                }

                if (lo != 0)
                {
                    partials[actuallyUsedPartials] = lo;
                    actuallyUsedPartials += 1;
                }

                x = hi;
            }

            while (partials.Count > actuallyUsedPartials)
            {
                partials.RemoveAt(partials.Count - 1);
            }

            if (x != 0)
            {
                partials.Add(x);
            }
        }

        // compute the exact sum of partials, stopping once we lose precision
        var n = partials.Count - 1;
        hi = 0;
        lo = 0;

        if (overflow != 0)
        {
            var next = n >= 0 ? partials[n] : 0;
            n -= 1;
            if (System.Math.Abs(overflow) > 1 || (overflow > 0 && next > 0) || (overflow < 0 && next < 0))
            {
                return overflow > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            }

            // here we actually have to do the arithmetic
            // drop a factor of 2 so we can do it without overflow
            // assert($abs(overflow) === 1)
            var s = TwoSum(overflow * Two1023, next / 2);
            hi = s.Hi;
            lo = s.Lo;
            lo *= 2;
            if (double.IsPositiveInfinity(System.Math.Abs(2 * hi)))
            {
                // stupid edge case: rounding to the maximum value
                // MAX_DOUBLE has a 1 in the last place of its significand, so if we subtract exactly half a ULP from 2**1024, the result rounds away from it (i.e. to infinity) under ties-to-even
                // but if the next partial has the opposite sign of the current value, we need to round towards MAX_DOUBLE instead
                // this is the same as the "handle rounding" case below, but there's only one potentially-finite case we need to worry about, so we just hardcode that one
                if (hi > 0)
                {
                    if (hi == Two1023 && lo == -(MaxUlp / 2) && n >= 0 && partials[n] < 0)
                    {
                        return MaxDouble;
                    }

                    return double.PositiveInfinity;
                }

                if (hi == -Two1023 && lo == (MaxUlp / 2) && n >= 0 && partials[n] > 0)
                {
                    return -MaxDouble;
                }

                return double.NegativeInfinity;
            }

            if (lo != 0)
            {
                partials[n + 1] = lo;
                n += 1;
                lo = 0;
            }

            hi *= 2;
        }

        while (n >= 0)
        {
            var x1 = hi;
            var y1 = partials[n];
            n -= 1;
            // assert: $abs(x1) > $abs(y1)
            (hi, lo) = TwoSum(x1, y1);
            if (lo != 0)
            {
                break; // eslint-disable-line no-restricted-syntax
            }
        }

        // handle rounding
        // when the roundoff error is exactly half of the ULP for the result, we need to check one more partial to know which way to round
        if (n >= 0 && ((lo < 0.0 && partials[n] < 0.0) || (lo > 0.0 && partials[n] > 0.0)))
        {
            var y2 = lo * 2.0;
            var x2 = hi + y2;
            var yr = x2 - hi;
            if (y2 == yr)
            {
                hi = x2;
            }
        }

        return hi;
    }
}
