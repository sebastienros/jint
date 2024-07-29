#nullable disable

// Copyright 2010 the V8 project authors. All rights reserved.
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above
//       copyright notice, this list of conditions and the following
//       disclaimer in the documentation and/or other materials provided
//       with the distribution.
//     * Neither the name of Google Inc. nor the names of its
//       contributors may be used to endorse or promote products derived
//       from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

// Ported to Java from Mozilla's version of V8-dtoa by Hannes Wallnoefer.
// The original revision was 67d1049b0bf9 from the mozilla-central tree.

using System.Diagnostics;
using Jint.Runtime;

namespace Jint.Native.Number.Dtoa;

internal sealed class FastDtoa
{
    // FastDtoa will produce at most kFastDtoaMaximalLength digits.
    public const int KFastDtoaMaximalLength = 17;

    // The minimal and maximal target exponent define the range of w's binary
    // exponent, where 'w' is the result of multiplying the input by a cached power
    // of ten.
    //
    // A different range might be chosen on a different platform, to optimize digit
    // generation, but a smaller range requires more powers of ten to be cached.
    private const int MinimalTargetExponent = -60;
    private const int MaximalTargetExponent = -32;

    // Adjusts the last digit of the generated number, and screens out generated
    // solutions that may be inaccurate. A solution may be inaccurate if it is
    // outside the safe interval, or if we ctannot prove that it is closer to the
    // input than a neighboring representation of the same length.
    //
    // Input: * buffer containing the digits of too_high / 10^kappa
    //        * distance_too_high_w == (too_high - w).f() * unit
    //        * unsafe_interval == (too_high - too_low).f() * unit
    //        * rest = (too_high - buffer * 10^kappa).f() * unit
    //        * ten_kappa = 10^kappa * unit
    //        * unit = the common multiplier
    // Output: returns true if the buffer is guaranteed to contain the closest
    //    representable number to the input.
    //  Modifies the generated digits in the buffer to approach (round towards) w.
    private static bool RoundWeed(
        ref  DtoaBuilder buffer,
        ulong distanceTooHighW,
        ulong unsafeInterval,
        ulong rest,
        ulong tenKappa,
        ulong unit)
    {
        ulong smallDistance = distanceTooHighW - unit;
        ulong bigDistance = distanceTooHighW + unit;
        // Let w_low  = too_high - big_distance, and
        //     w_high = too_high - small_distance.
        // Note: w_low < w < w_high
        //
        // The real w (* unit) must lie somewhere inside the interval
        // ]w_low; w_low[ (often written as "(w_low; w_low)")

        // Basically the buffer currently contains a number in the unsafe interval
        // ]too_low; too_high[ with too_low < w < too_high
        //
        //  too_high - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        //                     ^v 1 unit            ^      ^                 ^      ^
        //  boundary_high ---------------------     .      .                 .      .
        //                     ^v 1 unit            .      .                 .      .
        //   - - - - - - - - - - - - - - - - - - -  +  - - + - - - - - -     .      .
        //                                          .      .         ^       .      .
        //                                          .  big_distance  .       .      .
        //                                          .      .         .       .    rest
        //                              small_distance     .         .       .      .
        //                                          v      .         .       .      .
        //  w_high - - - - - - - - - - - - - - - - - -     .         .       .      .
        //                     ^v 1 unit                   .         .       .      .
        //  w ----------------------------------------     .         .       .      .
        //                     ^v 1 unit                   v         .       .      .
        //  w_low  - - - - - - - - - - - - - - - - - - - - -         .       .      .
        //                                                           .       .      v
        //  buffer --------------------------------------------------+-------+--------
        //                                                           .       .
        //                                                  safe_interval    .
        //                                                           v       .
        //   - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -     .
        //                     ^v 1 unit                                     .
        //  boundary_low -------------------------                     unsafe_interval
        //                     ^v 1 unit                                     v
        //  too_low  - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        //
        //
        // Note that the value of buffer could lie anywhere inside the range too_low
        // to too_high.
        //
        // boundary_low, boundary_high and w are approximations of the real boundaries
        // and v (the input number). They are guaranteed to be precise up to one unit.
        // In fact the error is guaranteed to be strictly less than one unit.
        //
        // Anything that lies outside the unsafe interval is guaranteed not to round
        // to v when read again.
        // Anything that lies inside the safe interval is guaranteed to round to v
        // when read again.
        // If the number inside the buffer lies inside the unsafe interval but not
        // inside the safe interval then we simply do not know and bail out (returning
        // false).
        //
        // Similarly we have to take into account the imprecision of 'w' when rounding
        // the buffer. If we have two potential representations we need to make sure
        // that the chosen one is closer to w_low and w_high since v can be anywhere
        // between them.
        //
        // By generating the digits of too_high we got the largest (closest to
        // too_high) buffer that is still in the unsafe interval. In the case where
        // w_high < buffer < too_high we try to decrement the buffer.
        // This way the buffer approaches (rounds towards) w.
        // There are 3 conditions that stop the decrementation process:
        //   1) the buffer is already below w_high
        //   2) decrementing the buffer would make it leave the unsafe interval
        //   3) decrementing the buffer would yield a number below w_high and farther
        //      away than the current number. In other words:
        //              (buffer{-1} < w_high) && w_high - buffer{-1} > buffer - w_high
        // Instead of using the buffer directly we use its distance to too_high.
        // Conceptually rest ~= too_high - buffer
        while (rest < smallDistance && // Negated condition 1
               unsafeInterval - rest >= tenKappa && // Negated condition 2
               (rest + tenKappa < smallDistance || // buffer{-1} > w_high
                smallDistance - rest >= rest + tenKappa - smallDistance))
        {
            buffer.DecreaseLast();
            rest += tenKappa;
        }

        // We have approached w+ as much as possible. We now test if approaching w-
        // would require changing the buffer. If yes, then we have two possible
        // representations close to w, but we cannot decide which one is closer.
        if (rest < bigDistance &&
            unsafeInterval - rest >= tenKappa &&
            (rest + tenKappa < bigDistance ||
             bigDistance - rest > rest + tenKappa - bigDistance))
        {
            return false;
        }

        // Weeding test.
        //   The safe interval is [too_low + 2 ulp; too_high - 2 ulp]
        //   Since too_low = too_high - unsafe_interval this is equivalent to
        //      [too_high - unsafe_interval + 4 ulp; too_high - 2 ulp]
        //   Conceptually we have: rest ~= too_high - buffer
        return (2*unit <= rest) && (rest <= unsafeInterval - 4*unit);
    }

    // Rounds the buffer upwards if the result is closer to v by possibly adding
    // 1 to the buffer. If the precision of the calculation is not sufficient to
    // round correctly, return false.
    // The rounding might shift the whole buffer in which case the kappa is
    // adjusted. For example "99", kappa = 3 might become "10", kappa = 4.
    //
    // If 2*rest > ten_kappa then the buffer needs to be round up.
    // rest can have an error of +/- 1 unit. This function accounts for the
    // imprecision and returns false, if the rounding direction cannot be
    // unambiguously determined.
    //
    // Precondition: rest < ten_kappa.
    static bool RoundWeedCounted(
        ref  DtoaBuilder buffer,
        ulong rest,
        ulong ten_kappa,
        ulong unit,
        ref int kappa)
    {
        Debug.Assert(rest < ten_kappa);
        // The following tests are done in a specific order to avoid overflows. They
        // will work correctly with any uint64 values of rest < ten_kappa and unit.
        //
        // If the unit is too big, then we don't know which way to round. For example
        // a unit of 50 means that the real number lies within rest +/- 50. If
        // 10^kappa == 40 then there is no way to tell which way to round.
        if (unit >= ten_kappa) return false;
        // Even if unit is just half the size of 10^kappa we are already completely
        // lost. (And after the previous test we know that the expression will not
        // over/underflow.)
        if (ten_kappa - unit <= unit) return false;
        // If 2 * (rest + unit) <= 10^kappa we can safely round down.
        if ((ten_kappa - rest > rest) && (ten_kappa - 2 * rest >= 2 * unit))
        {
            return true;
        }

        // If 2 * (rest - unit) >= 10^kappa, then we can safely round up.
        if ((rest > unit) && (ten_kappa - (rest - unit) <= (rest - unit)))
        {
            // Increment the last digit recursively until we find a non '9' digit.
            buffer._chars[buffer.Length - 1]++;
            for (int i = buffer.Length - 1; i > 0; --i)
            {
                if (buffer._chars[i] != '0' + 10) break;
                buffer._chars[i] = '0';
                buffer._chars[i - 1]++;
            }

            // If the first digit is now '0'+ 10 we had a buffer with all '9's. With the
            // exception of the first digit all digits are now '0'. Simply switch the
            // first digit to '1' and adjust the kappa. Example: "99" becomes "10" and
            // the power (the kappa) is increased.
            if (buffer._chars[0] == '0' + 10)
            {
                buffer._chars[0] = '1';
                kappa += 1;
            }

            return true;
        }

        return false;
    }

    private const int KTen4 = 10000;
    private const int KTen5 = 100000;
    private const int KTen6 = 1000000;
    private const int KTen7 = 10000000;
    private const int KTen8 = 100000000;
    private const int KTen9 = 1000000000;

    // Returns the biggest power of ten that is less than or equal than the given
    // number. We furthermore receive the maximum number of bits 'number' has.
    // If number_bits == 0 then 0^-1 is returned
    // The number of bits must be <= 32.
    // Precondition: (1 << number_bits) <= number < (1 << (number_bits + 1)).
    private static void BiggestPowerTen(uint number, int numberBits, out uint power, out int exponent)
    {
        switch (numberBits)
        {
            case 32:
            case 31:
            case 30:
                if (KTen9 <= number)
                {
                    power = KTen9;
                    exponent = 9;
                    break;
                } // else fallthrough

                goto case 29;
            case 29:
            case 28:
            case 27:
                if (KTen8 <= number)
                {
                    power = KTen8;
                    exponent = 8;
                    break;
                } // else fallthrough
                goto case 26;
            case 26:
            case 25:
            case 24:
                if (KTen7 <= number)
                {
                    power = KTen7;
                    exponent = 7;
                    break;
                } // else fallthrough
                goto case 23;
            case 23:
            case 22:
            case 21:
            case 20:
                if (KTen6 <= number)
                {
                    power = KTen6;
                    exponent = 6;
                    break;
                } // else fallthrough
                goto case 19;
            case 19:
            case 18:
            case 17:
                if (KTen5 <= number)
                {
                    power = KTen5;
                    exponent = 5;
                    break;
                } // else fallthrough
                goto case 16;
            case 16:
            case 15:
            case 14:
                if (KTen4 <= number)
                {
                    power = KTen4;
                    exponent = 4;
                    break;
                } // else fallthrough
                goto case 13;
            case 13:
            case 12:
            case 11:
            case 10:
                if (1000 <= number)
                {
                    power = 1000;
                    exponent = 3;
                    break;
                } // else fallthrough
                goto case 9;
            case 9:
            case 8:
            case 7:
                if (100 <= number)
                {
                    power = 100;
                    exponent = 2;
                    break;
                } // else fallthrough
                goto case 6;
            case 6:
            case 5:
            case 4:
                if (10 <= number)
                {
                    power = 10;
                    exponent = 1;
                    break;
                } // else fallthrough
                goto case 3;
            case 3:
            case 2:
            case 1:
                if (1 <= number)
                {
                    power = 1;
                    exponent = 0;
                    break;
                } // else fallthrough
                goto case 0;
            case 0:
                power = 0;
                exponent = -1;
                break;
            default:
                // Following assignments are here to silence compiler warnings.
                power = 0;
                exponent = 0;
                // UNREACHABLE();
                break;
        }
    }

    // Generates the digits of input number w.
    // w is a floating-point number (DiyFp), consisting of a significand and an
    // exponent. Its exponent is bounded by minimal_target_exponent and
    // maximal_target_exponent.
    //       Hence -60 <= w.e() <= -32.
    //
    // Returns false if it fails, in which case the generated digits in the buffer
    // should not be used.
    // Preconditions:
    //  * low, w and high are correct up to 1 ulp (unit in the last place). That
    //    is, their error must be less that a unit of their last digits.
    //  * low.e() == w.e() == high.e()
    //  * low < w < high, and taking into account their error: low~ <= high~
    //  * minimal_target_exponent <= w.e() <= maximal_target_exponent
    // Postconditions: returns false if procedure fails.
    //   otherwise:
    //     * buffer is not null-terminated, but len contains the number of digits.
    //     * buffer contains the shortest possible decimal digit-sequence
    //       such that LOW < buffer * 10^kappa < HIGH, where LOW and HIGH are the
    //       correct values of low and high (without their error).
    //     * if more than one decimal representation gives the minimal number of
    //       decimal digits then the one closest to W (where W is the correct value
    //       of w) is chosen.
    // Remark: this procedure takes into account the imprecision of its input
    //   numbers. If the precision is not enough to guarantee all the postconditions
    //   then false is returned. This usually happens rarely (~0.5%).
    //
    // Say, for the sake of example, that
    //   w.e() == -48, and w.f() == 0x1234567890abcdef
    // w's value can be computed by w.f() * 2^w.e()
    // We can obtain w's integral digits by simply shifting w.f() by -w.e().
    //  -> w's integral part is 0x1234
    //  w's fractional part is therefore 0x567890abcdef.
    // Printing w's integral part is easy (simply print 0x1234 in decimal).
    // In order to print its fraction we repeatedly multiply the fraction by 10 and
    // get each digit. Example the first digit after the point would be computed by
    //   (0x567890abcdef * 10) >> 48. -> 3
    // The whole thing becomes slightly more complicated because we want to stop
    // once we have enough digits. That is, once the digits inside the buffer
    // represent 'w' we can stop. Everything inside the interval low - high
    // represents w. However we have to pay attention to low, high and w's
    // imprecision.
    private static bool DigitGen(
        in DiyFp low,
        in DiyFp w,
        in DiyFp high,
        ref  DtoaBuilder buffer,
        int mk,
        out int kappa)
    {
        // low, w and high are imprecise, but by less than one ulp (unit in the last
        // place).
        // If we remove (resp. add) 1 ulp from low (resp. high) we are certain that
        // the new numbers are outside of the interval we want the final
        // representation to lie in.
        // Inversely adding (resp. removing) 1 ulp from low (resp. high) would yield
        // numbers that are certain to lie in the interval. We will use this fact
        // later on.
        // We will now start by generating the digits within the uncertain
        // interval. Later we will weed out representations that lie outside the safe
        // interval and thus _might_ lie outside the correct interval.
        ulong unit = 1;
        var tooLow = new DiyFp(low.F - unit, low.E);
        var tooHigh = new DiyFp(high.F + unit, high.E);
        // too_low and too_high are guaranteed to lie outside the interval we want the
        // generated number in.
        var unsafeInterval = DiyFp.Minus(tooHigh, tooLow);
        // We now cut the input number into two parts: the integral digits and the
        // fractionals. We will not write any decimal separator though, but adapt
        // kappa instead.
        // Reminder: we are currently computing the digits (stored inside the buffer)
        // such that:   too_low < buffer * 10^kappa < too_high
        // We use too_high for the digit_generation and stop as soon as possible.
        // If we stop early we effectively round down.
        var one = new DiyFp(((ulong) 1) << -w.E, w.E);
        // Division by one is a shift.
        var integrals = (uint) (tooHigh.F.UnsignedShift(-one.E) & 0xffffffffL);
        // Modulo by one is an and.
        ulong fractionals = tooHigh.F & (one.F - 1);
        BiggestPowerTen(
            integrals,
            DiyFp.KSignificandSize - (-one.E),
            out var divider,
            out var dividerExponent);

        kappa = dividerExponent + 1;
        // Loop invariant: buffer = too_high / 10^kappa  (integer division)
        // The invariant holds for the first iteration: kappa has been initialized
        // with the divider exponent + 1. And the divider is the biggest power of ten
        // that is smaller than integrals.
        while (kappa > 0)
        {
            int digit = (int) (integrals/divider);
            buffer.Append((char) ('0' + digit));
            integrals %= divider;
            kappa--;
            // Note that kappa now equals the exponent of the divider and that the
            // invariant thus holds again.
            ulong rest = ((ulong) integrals << -one.E) + fractionals;
            // Invariant: too_high = buffer * 10^kappa + DiyFp(rest, one.e())
            // Reminder: unsafe_interval.e() == one.e()
            if (rest < unsafeInterval.F)
            {
                // Rounding down (by not emitting the remaining digits) yields a number
                // that lies within the unsafe interval.
                return RoundWeed(
                    ref buffer,
                    DiyFp.Minus(tooHigh, w).F,
                    unsafeInterval.F,
                    rest,
                    (ulong) divider << -one.E,
                    unit);
            }
            divider /= 10;
        }

        // The integrals have been generated. We are at the point of the decimal
        // separator. In the following loop we simply multiply the remaining digits by
        // 10 and divide by one. We just need to pay attention to multiply associated
        // data (like the interval or 'unit'), too.
        // Instead of multiplying by 10 we multiply by 5 (cheaper operation) and
        // increase its (imaginary) exponent. At the same time we decrease the
        // divider's (one's) exponent and shift its significand.
        // Basically, if fractionals was a DiyFp (with fractionals.e == one.e):
        //      fractionals.f *= 10;
        //      fractionals.f >>= 1; fractionals.e++; // value remains unchanged.
        //      one.f >>= 1; one.e++;                 // value remains unchanged.
        //      and we have again fractionals.e == one.e which allows us to divide
        //           fractionals.f() by one.f()
        // We simply combine the *= 10 and the >>= 1.
        while (true)
        {
            fractionals *= 5;
            unit *= 5;
            unsafeInterval = new DiyFp(unsafeInterval.F*5, unsafeInterval.E + 1); // Will be optimized out.
            one = new DiyFp(one.F.UnsignedShift(1), one.E + 1);
            // Integer division by one.
            var digit = (int) ((fractionals.UnsignedShift(-one.E)) & 0xffffffffL);
            buffer.Append((char) ('0' + digit));
            fractionals &= one.F - 1; // Modulo by one.
            kappa--;
            if (fractionals < unsafeInterval.F)
            {
                return RoundWeed(
                    ref buffer,
                    DiyFp.Minus(tooHigh, w).F*unit,
                    unsafeInterval.F,
                    fractionals,
                    one.F,
                    unit);
            }
        }
    }

    // Generates (at most) requested_digits of input number w.
    // w is a floating-point number (DiyFp), consisting of a significand and an
    // exponent. Its exponent is bounded by kMinimalTargetExponent and
    // kMaximalTargetExponent.
    //       Hence -60 <= w.e() <= -32.
    //
    // Returns false if it fails, in which case the generated digits in the buffer
    // should not be used.
    // Preconditions:
    //  * w is correct up to 1 ulp (unit in the last place). That
    //    is, its error must be strictly less than a unit of its last digit.
    //  * kMinimalTargetExponent <= w.e() <= kMaximalTargetExponent
    //
    // Postconditions: returns false if procedure fails.
    //   otherwise:
    //     * buffer is not null-terminated, but length contains the number of
    //       digits.
    //     * the representation in buffer is the most precise representation of
    //       requested_digits digits.
    //     * buffer contains at most requested_digits digits of w. If there are less
    //       than requested_digits digits then some trailing '0's have been removed.
    //     * kappa is such that
    //            w = buffer * 10^kappa + eps with |eps| < 10^kappa / 2.
    //
    // Remark: This procedure takes into account the imprecision of its input
    //   numbers. If the precision is not enough to guarantee all the postconditions
    //   then false is returned. This usually happens rarely, but the failure-rate
    //   increases with higher requested_digits.
    static bool DigitGenCounted(
        in DiyFp w,
        int requested_digits,
        ref  DtoaBuilder buffer,
        out int kappa)
    {
        Debug.Assert(MinimalTargetExponent <= w.E && w.E <= MaximalTargetExponent);

        // w is assumed to have an error less than 1 unit. Whenever w is scaled we
        // also scale its error.
        ulong w_error = 1;
        // We cut the input number into two parts: the integral digits and the
        // fractional digits. We don't emit any decimal separator, but adapt kappa
        // instead. Example: instead of writing "1.2" we put "12" into the buffer and
        // increase kappa by 1.
        DiyFp one = new DiyFp(((ulong) 1) << -w.E, w.E);
        // Division by one is a shift.
        uint integrals = (uint) (w.F >> -one.E);
        // Modulo by one is an and.
        ulong fractionals = w.F & (one.F - 1);
        BiggestPowerTen(integrals, DiyFp.KSignificandSize - (-one.E), out var divisor, out var divisor_exponent);
        kappa = divisor_exponent + 1;

        // Loop invariant: buffer = w / 10^kappa  (integer division)
        // The invariant holds for the first iteration: kappa has been initialized
        // with the divisor exponent + 1. And the divisor is the biggest power of ten
        // that is smaller than 'integrals'.
        while (kappa > 0)
        {
            int digit = (int) (integrals / divisor);
            buffer.Append((char) ('0' + digit));
            requested_digits--;
            integrals %= divisor;
            kappa--;
            // Note that kappa now equals the exponent of the divisor and that the
            // invariant thus holds again.
            if (requested_digits == 0) break;
            divisor /= 10;
        }

        if (requested_digits == 0)
        {
            ulong rest = (((ulong) integrals) << -one.E) + fractionals;
            return RoundWeedCounted(ref buffer, rest,(ulong) divisor << -one.E, w_error, ref kappa);
        }

        // The integrals have been generated. We are at the point of the decimal
        // separator. In the following loop we simply multiply the remaining digits by
        // 10 and divide by one. We just need to pay attention to multiply associated
        // data (the 'unit'), too.
        // Note that the multiplication by 10 does not overflow, because w.e >= -60
        // and thus one.e >= -60.
        Debug.Assert(one.E >= -60);
        Debug.Assert(fractionals < one.F);

        while (requested_digits > 0 && fractionals > w_error) {
            fractionals *= 10;
            w_error *= 10;
            // Integer division by one.
            int digit = (int) (fractionals >> -one.E);
            buffer.Append((char) ('0' + digit));
            requested_digits--;
            fractionals &= one.F - 1;  // Modulo by one.
            (kappa)--;
        }
        if (requested_digits != 0) return false;
        return RoundWeedCounted(ref buffer, fractionals, one.F, w_error, ref kappa);
    }

    // Provides a decimal representation of v.
    // Returns true if it succeeds, otherwise the result cannot be trusted.
    // There will be *length digits inside the buffer (not null-terminated).
    // If the function returns true then
    //        v == (double) (buffer * 10^decimal_exponent).
    // The digits in the buffer are the shortest representation possible: no
    // 0.09999999999999999 instead of 0.1. The shorter representation will even be
    // chosen even if the longer one would be closer to v.
    // The last digit will be closest to the actual v. That is, even if several
    // digits might correctly yield 'v' when read again, the closest will be
    // computed.
    private static bool Grisu3(double v, ref  DtoaBuilder buffer, out int decimal_exponent)
    {
        ulong bits = (ulong) BitConverter.DoubleToInt64Bits(v);
        DiyFp w = DoubleHelper.AsNormalizedDiyFp(bits);
        // boundary_minus and boundary_plus are the boundaries between v and its
        // closest floating-point neighbors. Any number strictly between
        // boundary_minus and boundary_plus will round to v when convert to a double.
        // Grisu3 will never output representations that lie exactly on a boundary.
        var boundaries = DoubleHelper.NormalizedBoundaries(bits);
        var boundaryMinus = boundaries.Minus;
        var boundaryPlus = boundaries.Plus;

        Debug.Assert(boundaryPlus.E == w.E);

        var result = CachedPowers.GetCachedPowerForBinaryExponentRange(
            MinimalTargetExponent - (w.E + DiyFp.KSignificandSize),
            MaximalTargetExponent - (w.E + DiyFp.KSignificandSize));

        var mk = result.decimalExponent;
        var tenMk = result.cMk;

        Debug.Assert(MinimalTargetExponent <= w.E + tenMk.E +
                     DiyFp.KSignificandSize &&
                     MaximalTargetExponent >= w.E + tenMk.E +
                     DiyFp.KSignificandSize);
        // Note that ten_mk is only an approximation of 10^-k. A DiyFp only contains a
        // 64 bit significand and ten_mk is thus only precise up to 64 bits.

        // The DiyFp::Times procedure rounds its result, and ten_mk is approximated
        // too. The variable scaled_w (as well as scaled_boundary_minus/plus) are now
        // off by a small amount.
        // In fact: scaled_w - w*10^k < 1ulp (unit in the last place) of scaled_w.
        // In other words: let f = scaled_w.f() and e = scaled_w.e(), then
        //           (f-1) * 2^e < w*10^k < (f+1) * 2^e
        DiyFp scaledW = DiyFp.Times(w, tenMk);
        Debug.Assert(scaledW.E ==
                     boundaryPlus.E + tenMk.E + DiyFp.KSignificandSize);
        // In theory it would be possible to avoid some recomputations by computing
        // the difference between w and boundary_minus/plus (a power of 2) and to
        // compute scaled_boundary_minus/plus by subtracting/adding from
        // scaled_w. However the code becomes much less readable and the speed
        // enhancements are not terriffic.
        DiyFp scaledBoundaryMinus = DiyFp.Times(boundaryMinus, tenMk);
        DiyFp scaledBoundaryPlus = DiyFp.Times(boundaryPlus, tenMk);

        // DigitGen will generate the digits of scaled_w. Therefore we have
        // v == (double) (scaled_w * 10^-mk).
        // Set decimal_exponent == -mk and pass it to DigitGen. If scaled_w is not an
        // integer than it will be updated. For instance if scaled_w == 1.23 then
        // the buffer will be filled with "123" und the decimal_exponent will be
        // decreased by 2.
        int kappa;
        var digitGen = DigitGen(scaledBoundaryMinus, scaledW, scaledBoundaryPlus, ref buffer, mk, out kappa);
        decimal_exponent = -mk + kappa;
        return digitGen;
    }


    // The "counted" version of grisu3 (see above) only generates requested_digits
    // number of digits. This version does not generate the shortest representation,
    // and with enough requested digits 0.1 will at some point print as 0.9999999...
    // Grisu3 is too imprecise for real halfway cases (1.5 will not work) and
    // therefore the rounding strategy for halfway cases is irrelevant.
    static bool Grisu3Counted(
        double v,
        int requested_digits,
        ref  DtoaBuilder buffer,
        out int decimal_exponent)
    {
        ulong bits = (ulong) BitConverter.DoubleToInt64Bits(v);
        DiyFp w = DoubleHelper.AsNormalizedDiyFp(bits);

        var powerResult = CachedPowers.GetCachedPowerForBinaryExponentRange(
            MinimalTargetExponent - (w.E + DiyFp.KSignificandSize),
            MaximalTargetExponent - (w.E + DiyFp.KSignificandSize));

        var mk = powerResult.decimalExponent;
        var ten_mk = powerResult.cMk;

        Debug.Assert((MinimalTargetExponent <= w.E + ten_mk.E + DiyFp.KSignificandSize) && (MaximalTargetExponent >= w.E + ten_mk.E + DiyFp.KSignificandSize));
        // Note that ten_mk is only an approximation of 10^-k. A DiyFp only contains a
        // 64 bit significand and ten_mk is thus only precise up to 64 bits.

        // The DiyFp::Times procedure rounds its result, and ten_mk is approximated
        // too. The variable scaled_w (as well as scaled_boundary_minus/plus) are now
        // off by a small amount.
        // In fact: scaled_w - w*10^k < 1ulp (unit in the last place) of scaled_w.
        // In other words: let f = scaled_w.f() and e = scaled_w.e(), then
        //           (f-1) * 2^e < w*10^k < (f+1) * 2^e
        DiyFp scaled_w = DiyFp.Times(w, ten_mk);

        // We now have (double) (scaled_w * 10^-mk).
        // DigitGen will generate the first requested_digits digits of scaled_w and
        // return together with a kappa such that scaled_w ~= buffer * 10^kappa. (It
        // will not always be exactly the same since DigitGenCounted only produces a
        // limited number of digits.)
        bool result = DigitGenCounted(scaled_w, requested_digits, ref buffer, out var kappa);
        decimal_exponent = -mk + kappa;
        return result;
    }

    public static bool NumberToString(
        double v,
        DtoaMode mode,
        int requested_digits,
        out int decimal_point,
        ref  DtoaBuilder buffer)
    {
        Debug.Assert(v > 0);
        Debug.Assert(!double.IsNaN(v));
        Debug.Assert(!double.IsInfinity(v));

        var result = false;
        var decimal_exponent = 0;
        switch (mode)
        {
            case DtoaMode.Shortest:
                result = Grisu3(v, ref buffer, out decimal_exponent);
                break;
            case DtoaMode.Precision:
                result = Grisu3Counted(v, requested_digits, ref buffer, out decimal_exponent);
                break;
            default:
                ExceptionHelper.ThrowArgumentOutOfRangeException();
                break;
        }

        if (result)
        {
            decimal_point = buffer.Length + decimal_exponent;
            return true;
        }

        decimal_point = -1;
        return false;
    }
}
