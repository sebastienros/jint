#nullable disable

using System.Diagnostics;
using Jint.Runtime;

namespace Jint.Native.Number.Dtoa;

internal sealed class Bignum
{
    // 3584 = 128 * 28. We can represent 2^3584 > 10^1000 accurately.
    // This bignum can encode much bigger numbers, since it contains an
    // exponent.
    private const int kMaxSignificantBits = 3584;

    private const int kChunkSize = sizeof(uint) * 8;
    private const int kDoubleChunkSize = sizeof(ulong) * 8;

    // With bigit size of 28 we loose some bits, but a double still fits easily
    // into two chunks, and more importantly we can use the Comba multiplication.
    private const int kBigitSize = 28;
    private const uint kBigitMask = (1 << kBigitSize) - 1;

    // Every instance allocates kBigitLength chunks on the stack. Bignums cannot
    // grow. There are no checks if the stack-allocated space is sufficient.
    private const int kBigitCapacity = kMaxSignificantBits / kBigitSize;

    private readonly uint[] bigits_ = new uint[kBigitCapacity];

    // The Bignum's value equals value(bigits_) * 2^(exponent_ * kBigitSize).
    private int exponent_;
    private int used_digits_;

    private int BigitLength()
    {
        return used_digits_ + exponent_;
    }

    // Precondition: this/other < 16bit.
    public uint DivideModuloIntBignum(Bignum other)
    {
        Debug.Assert(IsClamped());
        Debug.Assert(other.IsClamped());
        Debug.Assert(other.used_digits_ > 0);

        // Easy case: if we have less digits than the divisor than the result is 0.
        // Note: this handles the case where this == 0, too.
        if (BigitLength() < other.BigitLength()) return 0;

        Align(other);

        uint result = 0;

        // Start by removing multiples of 'other' until both numbers have the same
        // number of digits.
        while (BigitLength() > other.BigitLength())
        {
            // This naive approach is extremely inefficient if the this divided other
            // might be big. This function is implemented for doubleToString where
            // the result should be small (less than 10).
            Debug.Assert(other.bigits_[other.used_digits_ - 1] >= (1 << kBigitSize) / 16);
            // Remove the multiples of the first digit.
            // Example this = 23 and other equals 9. -> Remove 2 multiples.
            result += bigits_[used_digits_ - 1];
            SubtractTimes(other, bigits_[used_digits_ - 1]);
        }

        Debug.Assert(BigitLength() == other.BigitLength());

        // Both bignums are at the same length now.
        // Since other has more than 0 digits we know that the access to
        // bigits_[used_digits_ - 1] is safe.
        var this_bigit = bigits_[used_digits_ - 1];
        var other_bigit = other.bigits_[other.used_digits_ - 1];

        if (other.used_digits_ == 1)
        {
            // Shortcut for easy (and common) case.
            uint quotient = this_bigit / other_bigit;
            bigits_[used_digits_ - 1] = this_bigit - other_bigit * quotient;
            result += quotient;
            Clamp();
            return result;
        }

        uint division_estimate = this_bigit / (other_bigit + 1);
        result += division_estimate;
        SubtractTimes(other, division_estimate);

        if (other_bigit * (division_estimate + 1) > this_bigit) return result;

        while (LessEqual(other, this))
        {
            SubtractBignum(other);
            result++;
        }

        return result;
    }

    void Align(Bignum other)
    {
        if (exponent_ > other.exponent_)
        {
            // If "X" represents a "hidden" digit (by the exponent) then we are in the
            // following case (a == this, b == other):
            // a:  aaaaaaXXXX   or a:   aaaaaXXX
            // b:     bbbbbbX      b: bbbbbbbbXX
            // We replace some of the hidden digits (X) of a with 0 digits.
            // a:  aaaaaa000X   or a:   aaaaa0XX
            int zero_digits = exponent_ - other.exponent_;
            ValidateCapacity(used_digits_ + zero_digits);
            for (int i = used_digits_ - 1; i >= 0; --i)
            {
                bigits_[i + zero_digits] = bigits_[i];
            }

            for (int i = 0; i < zero_digits; ++i)
            {
                bigits_[i] = 0;
            }

            used_digits_ += zero_digits;
            exponent_ -= zero_digits;
            Debug.Assert(used_digits_ >= 0);
            Debug.Assert(exponent_ >= 0);
        }
    }

    private static void ValidateCapacity(int size)
    {
        if (size > kBigitCapacity)
        {
            ExceptionHelper.ThrowInvalidOperationException();
        }
    }

    private void Clamp()
    {
        while (used_digits_ > 0 && bigits_[used_digits_ - 1] == 0) used_digits_--;
        if (used_digits_ == 0) exponent_ = 0;
    }


    private bool IsClamped()
    {
        return used_digits_ == 0 || bigits_[used_digits_ - 1] != 0;
    }


    private void Zero()
    {
        for (var i = 0; i < used_digits_; ++i) bigits_[i] = 0;
        used_digits_ = 0;
        exponent_ = 0;
    }

    // Guaranteed to lie in one Bigit.
    internal void AssignUInt16(uint value)
    {
        Debug.Assert(kBigitSize <= 8 * sizeof(uint));
        Zero();
        if (value == 0) return;

        ValidateCapacity(1);
        bigits_[0] = value;
        used_digits_ = 1;
    }

    internal void AssignUInt64(ulong value)
    {
        const int kUInt64Size = 64;

        Zero();
        if (value == 0) return;

        int needed_bigits = kUInt64Size / kBigitSize + 1;
        ValidateCapacity(needed_bigits);
        for (int i = 0; i < needed_bigits; ++i)
        {
            bigits_[i] = (uint) (value & kBigitMask);
            value = value >> kBigitSize;
        }

        used_digits_ = needed_bigits;
        Clamp();
    }


    internal void AssignBignum(Bignum other)
    {
        exponent_ = other.exponent_;
        for (int i = 0; i < other.used_digits_; ++i)
        {
            bigits_[i] = other.bigits_[i];
        }

        // Clear the excess digits (if there were any).
        for (int i = other.used_digits_; i < used_digits_; ++i)
        {
            bigits_[i] = 0;
        }

        used_digits_ = other.used_digits_;
    }


    void SubtractTimes(Bignum other, uint factor)
    {
#if DEBUG
        var a = new Bignum();
        var b = new Bignum();
        a.AssignBignum(this);
        b.AssignBignum(other);
        b.MultiplyByUInt32(factor);
        a.SubtractBignum(b);
#endif
        Debug.Assert(exponent_ <= other.exponent_);
        if (factor < 3)
        {
            for (int i = 0; i < factor; ++i)
            {
                SubtractBignum(other);
            }

            return;
        }

        uint borrow = 0;
        int exponent_diff = other.exponent_ - exponent_;
        for (int i = 0; i < other.used_digits_; ++i)
        {
            ulong product = factor * other.bigits_[i];
            ulong remove = borrow + product;
            uint difference = bigits_[i + exponent_diff] - (uint) (remove & kBigitMask);
            bigits_[i + exponent_diff] = difference & kBigitMask;
            borrow = (uint) ((difference >> (kChunkSize - 1)) + (remove >> kBigitSize));
        }

        for (int i = other.used_digits_ + exponent_diff; i < used_digits_; ++i)
        {
            if (borrow == 0) return;
            uint difference = bigits_[i] - borrow;
            bigits_[i] = difference & kBigitMask;
            borrow = difference >> (kChunkSize - 1);
        }

        Clamp();

#if DEBUG
        Debug.Assert(Equal(a, this));
#endif
    }


    void SubtractBignum(Bignum other)
    {
        Debug.Assert(IsClamped());
        Debug.Assert(other.IsClamped());
        // We require this to be bigger than other.
        Debug.Assert(LessEqual(other, this));

        Align(other);

        int offset = other.exponent_ - exponent_;
        uint borrow = 0;
        int i;
        for (i = 0; i < other.used_digits_; ++i)
        {
            Debug.Assert((borrow == 0) || (borrow == 1));
            uint difference = bigits_[i + offset] - other.bigits_[i] - borrow;
            bigits_[i + offset] = difference & kBigitMask;
            borrow = difference >> (kChunkSize - 1);
        }

        while (borrow != 0)
        {
            uint difference = bigits_[i + offset] - borrow;
            bigits_[i + offset] = difference & kBigitMask;
            borrow = difference >> (kChunkSize - 1);
            ++i;
        }

        Clamp();
    }

    internal static bool Equal(Bignum a, Bignum b)
    {
        return Compare(a, b) == 0;
    }

    internal static bool LessEqual(Bignum a, Bignum b)
    {
        return Compare(a, b) <= 0;
    }

    internal static bool Less(Bignum a, Bignum b)
    {
        return Compare(a, b) < 0;
    }

    // Returns a + b == c
    static bool PlusEqual(Bignum a, Bignum b, Bignum c)
    {
        return PlusCompare(a, b, c) == 0;
    }

    // Returns a + b <= c
    static bool PlusLessEqual(Bignum a, Bignum b, Bignum c)
    {
        return PlusCompare(a, b, c) <= 0;
    }

    // Returns a + b < c
    static bool PlusLess(Bignum a, Bignum b, Bignum c)
    {
        return PlusCompare(a, b, c) < 0;
    }

    uint BigitAt(int index)
    {
        if (index >= BigitLength()) return 0;
        if (index < exponent_) return 0;
        return bigits_[index - exponent_];
    }


    static int Compare(Bignum a, Bignum b)
    {
        Debug.Assert(a.IsClamped());
        Debug.Assert(b.IsClamped());
        int bigit_length_a = a.BigitLength();
        int bigit_length_b = b.BigitLength();
        if (bigit_length_a < bigit_length_b) return -1;
        if (bigit_length_a > bigit_length_b) return +1;
        for (int i = bigit_length_a - 1; i >= System.Math.Min(a.exponent_, b.exponent_); --i)
        {
            uint bigit_a = a.BigitAt(i);
            uint bigit_b = b.BigitAt(i);
            if (bigit_a < bigit_b) return -1;
            if (bigit_a > bigit_b) return +1;
            // Otherwise they are equal up to this digit. Try the next digit.
        }

        return 0;
    }


    internal static int PlusCompare(Bignum a, Bignum b, Bignum c)
    {
        Debug.Assert(a.IsClamped());
        Debug.Assert(b.IsClamped());
        Debug.Assert(c.IsClamped());
        if (a.BigitLength() < b.BigitLength())
        {
            return PlusCompare(b, a, c);
        }

        if (a.BigitLength() + 1 < c.BigitLength()) return -1;
        if (a.BigitLength() > c.BigitLength()) return +1;
        // The exponent encodes 0-bigits. So if there are more 0-digits in 'a' than
        // 'b' has digits, then the bigit-length of 'a'+'b' must be equal to the one
        // of 'a'.
        if (a.exponent_ >= b.BigitLength() && a.BigitLength() < c.BigitLength())
        {
            return -1;
        }

        uint borrow = 0;
        // Starting at min_exponent all digits are == 0. So no need to compare them.
        int min_exponent = System.Math.Min(System.Math.Min(a.exponent_, b.exponent_), c.exponent_);
        for (int i = c.BigitLength() - 1; i >= min_exponent; --i)
        {
            uint chunk_a = a.BigitAt(i);
            uint chunk_b = b.BigitAt(i);
            uint chunk_c = c.BigitAt(i);
            uint sum = chunk_a + chunk_b;
            if (sum > chunk_c + borrow)
            {
                return +1;
            }
            else
            {
                borrow = chunk_c + borrow - sum;
                if (borrow > 1) return -1;
                borrow <<= kBigitSize;
            }
        }

        if (borrow == 0) return 0;
        return -1;
    }

    internal void Times10()
    {
        MultiplyByUInt32(10);
    }

    internal void MultiplyByUInt32(uint factor)
    {
        if (factor == 1) return;
        if (factor == 0)
        {
            Zero();
            return;
        }

        if (used_digits_ == 0) return;

        // The product of a bigit with the factor is of size kBigitSize + 32.
        // Assert that this number + 1 (for the carry) fits into double chunk.
        Debug.Assert(kDoubleChunkSize >= kBigitSize + 32 + 1);
        ulong carry = 0;
        for (int i = 0; i < used_digits_; ++i)
        {
            ulong product = ((ulong) factor) * bigits_[i] + carry;
            bigits_[i] = (uint) (product & kBigitMask);
            carry = (product >> kBigitSize);
        }

        while (carry != 0)
        {
            ValidateCapacity(used_digits_ + 1);
            bigits_[used_digits_] = (uint) (carry & kBigitMask);
            used_digits_++;
            carry >>= kBigitSize;
        }
    }

    internal void MultiplyByUInt64(ulong factor)
    {
        if (factor == 1) return;
        if (factor == 0)
        {
            Zero();
            return;
        }
        Debug.Assert(kBigitSize < 32);
        ulong carry = 0;
        ulong low = factor & 0xFFFFFFFF;
        ulong high = factor >> 32;
        for (int i = 0; i < used_digits_; ++i)
        {
            ulong product_low = low * bigits_[i];
            ulong product_high = high * bigits_[i];
            ulong tmp = (carry & kBigitMask) + product_low;
            bigits_[i] = (uint) (tmp & kBigitMask);
            carry = (carry >> kBigitSize) + (tmp >> kBigitSize) +
                    (product_high << (32 - kBigitSize));
        }
        while (carry != 0)
        {
            ValidateCapacity(used_digits_ + 1);
            bigits_[used_digits_] = (uint) (carry & kBigitMask);
            used_digits_++;
            carry >>= kBigitSize;
        }
    }

    internal void ShiftLeft(int shift_amount)
    {
        if (used_digits_ == 0) return;
        exponent_ += shift_amount / kBigitSize;
        int local_shift = shift_amount % kBigitSize;
        ValidateCapacity(used_digits_ + 1);
        BigitsShiftLeft(local_shift);
    }

    void BigitsShiftLeft(int shift_amount)
    {
        Debug.Assert(shift_amount < kBigitSize);
        Debug.Assert(shift_amount >= 0);
        uint carry = 0;
        for (int i = 0; i < used_digits_; ++i)
        {
            uint new_carry = bigits_[i] >> (kBigitSize - shift_amount);
            bigits_[i] = ((bigits_[i] << shift_amount) + carry) & kBigitMask;
            carry = new_carry;
        }

        if (carry != 0)
        {
            bigits_[used_digits_] = carry;
            used_digits_++;
        }
    }


    internal void AssignPowerUInt16(uint baseValue, int power_exponent)
    {
        Debug.Assert(baseValue != 0);
        Debug.Assert(power_exponent >= 0);
        if (power_exponent == 0)
        {
            AssignUInt16(1);
            return;
        }

        Zero();
        int shifts = 0;
        // We expect baseValue to be in range 2-32, and most often to be 10.
        // It does not make much sense to implement different algorithms for counting
        // the bits.
        while ((baseValue & 1) == 0)
        {
            baseValue >>= 1;
            shifts++;
        }

        int bit_size = 0;
        uint tmp_base = baseValue;
        while (tmp_base != 0)
        {
            tmp_base >>= 1;
            bit_size++;
        }

        int final_size = bit_size * power_exponent;
        // 1 extra bigit for the shifting, and one for rounded final_size.
        ValidateCapacity(final_size / kBigitSize + 2);

        // Left to Right exponentiation.
        int mask = 1;
        while (power_exponent >= mask) mask <<= 1;

        // The mask is now pointing to the bit above the most significant 1-bit of
        // power_exponent.
        // Get rid of first 1-bit;
        mask >>= 2;
        ulong this_value = baseValue;

        bool delayed_multipliciation = false;
        const ulong max_32bits = 0xFFFFFFFF;
        while (mask != 0 && this_value <= max_32bits)
        {
            this_value = this_value * this_value;
            // Verify that there is enough space in this_value to perform the
            // multiplication.  The first bit_size bits must be 0.
            if ((power_exponent & mask) != 0)
            {
                ulong base_bits_mask = ~((((ulong) 1) << (64 - bit_size)) - 1);
                bool high_bits_zero = (this_value & base_bits_mask) == 0;
                if (high_bits_zero)
                {
                    this_value *= baseValue;
                }
                else
                {
                    delayed_multipliciation = true;
                }
            }

            mask >>= 1;
        }

        AssignUInt64(this_value);
        if (delayed_multipliciation)
        {
            MultiplyByUInt32(baseValue);
        }

        // Now do the same thing as a bignum.
        while (mask != 0)
        {
            Square();
            if ((power_exponent & mask) != 0)
            {
                MultiplyByUInt32(baseValue);
            }

            mask >>= 1;
        }

        // And finally add the saved shifts.
        ShiftLeft(shifts * power_exponent);
    }

    void Square()
    {
        Debug.Assert(IsClamped());
        int product_length = 2 * used_digits_;
        ValidateCapacity(product_length);

        // Comba multiplication: compute each column separately.
        // Example: r = a2a1a0 * b2b1b0.
        //    r =  1    * a0b0 +
        //        10    * (a1b0 + a0b1) +
        //        100   * (a2b0 + a1b1 + a0b2) +
        //        1000  * (a2b1 + a1b2) +
        //        10000 * a2b2
        //
        // In the worst case we have to accumulate nb-digits products of digit*digit.
        //
        // Assert that the additional number of bits in a DoubleChunk are enough to
        // sum up used_digits of Bigit*Bigit.
        if ((1 << (2 * (kChunkSize - kBigitSize))) <= used_digits_)
        {
            ExceptionHelper.ThrowNotImplementedException();
        }

        ulong accumulator = 0;
        // First shift the digits so we don't overwrite them.
        int copy_offset = used_digits_;
        for (int i = 0; i < used_digits_; ++i)
        {
            bigits_[copy_offset + i] = bigits_[i];
        }

        // We have two loops to avoid some 'if's in the loop.
        for (int i = 0; i < used_digits_; ++i)
        {
            // Process temporary digit i with power i.
            // The sum of the two indices must be equal to i.
            int bigit_index1 = i;
            int bigit_index2 = 0;
            // Sum all of the sub-products.
            while (bigit_index1 >= 0)
            {
                uint chunk1 = bigits_[copy_offset + bigit_index1];
                uint chunk2 = bigits_[copy_offset + bigit_index2];
                accumulator += (ulong) chunk1 * chunk2;
                bigit_index1--;
                bigit_index2++;
            }

            bigits_[i] = (uint) accumulator & kBigitMask;
            accumulator >>= kBigitSize;
        }

        for (int i = used_digits_; i < product_length; ++i)
        {
            int bigit_index1 = used_digits_ - 1;
            int bigit_index2 = i - bigit_index1;
            // Invariant: sum of both indices is again equal to i.
            // Inner loop runs 0 times on last iteration, emptying accumulator.
            while (bigit_index2 < used_digits_)
            {
                uint chunk1 = bigits_[copy_offset + bigit_index1];
                uint chunk2 = bigits_[copy_offset + bigit_index2];
                accumulator += (ulong) chunk1 * chunk2;
                bigit_index1--;
                bigit_index2++;
            }

            // The overwritten bigits_[i] will never be read in further loop iterations,
            // because bigit_index1 and bigit_index2 are always greater
            // than i - used_digits_.
            bigits_[i] = (uint) accumulator & kBigitMask;
            accumulator >>= kBigitSize;
        }

        // Since the result was guaranteed to lie inside the number the
        // accumulator must be 0 now.
        Debug.Assert(accumulator == 0);

        // Don't forget to update the used_digits and the exponent.
        used_digits_ = product_length;
        exponent_ *= 2;
        Clamp();
    }
}
