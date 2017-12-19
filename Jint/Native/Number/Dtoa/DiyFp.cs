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

namespace Jint.Native.Number.Dtoa
{

// This "Do It Yourself Floating Point" class implements a floating-point number
// with a uint64 significand and an int exponent. Normalized DiyFp numbers will
// have the most significant bit of the significand set.
// Multiplication and Subtraction do not normalize their results.
// DiyFp are not designed to contain special doubles (NaN and Infinity).
    internal struct DiyFp
    {
        internal const int KSignificandSize = 64;
        private const ulong KUint64MSB = 0x8000000000000000L;

        internal DiyFp(long f, int e)
        {
            F = f;
            E = e;
        }

        public long F { get; }
        public int E { get; }

        private static bool Uint64Gte(long a, long b)
        {
            // greater-or-equal for unsigned int64 in java-style...
            return (a == b) || ((a > b) ^ (a < 0) ^ (b < 0));
        }

        // Returns a - b.
        // The exponents of both numbers must be the same and this must be bigger
        // than other. The result will not be normalized.
        internal static DiyFp Minus(DiyFp a, DiyFp b)
        {
            Debug.Assert(a.E == b.E);
            Debug.Assert(Uint64Gte(a.F, b.F));

            return new DiyFp(a.F - b.F, a.E);
        }

        // this = this * other.

        // returns a * b;
        internal static DiyFp Times(DiyFp a, DiyFp b)
        {
            DiyFp result = new DiyFp(a.F, a.E);
            // Simply "emulates" a 128 bit multiplication.
            // However: the resulting number only contains 64 bits. The least
            // significant 64 bits are only used for rounding the most significant 64
            // bits.
            const long kM32 = 0xFFFFFFFFL;
            long a1 = result.F.UnsignedShift(32);
            long b1 = result.F & kM32;
            long c = b.F.UnsignedShift(32);
            long d = b.F & kM32;
            long ac = a1*c;
            long bc = b1*c;
            long ad = a1*d;
            long bd = b1*d;
            long tmp = bd.UnsignedShift(32) + (ad & kM32) + (bc & kM32);
            // By adding 1U << 31 to tmp we round the final result.
            // Halfway cases will be round up.
            tmp += 1L << 31;
            long resultF = ac + ad.UnsignedShift(32) + bc.UnsignedShift(32) + tmp.UnsignedShift(32);
            return new DiyFp(resultF, result.E + b.E + 64);
        }

        internal static DiyFp Normalize(long f, int e)
        {
            // This method is mainly called for normalizing boundaries. In general
            // boundaries need to be shifted by 10 bits. We thus optimize for this case.
            const long k10MsBits = 0xFFC00000L << 32;
            while ((f & k10MsBits) == 0)
            {
                f <<= 10;
                e -= 10;
            }
            while (((ulong) f & KUint64MSB) == 0)
            {
                f <<= 1;
                e--;
            }

            return new DiyFp(f, e);
        }

        public override string ToString()
        {
            return "[DiyFp f:" + F + ", e:" + E + "]";
        }
    }
}