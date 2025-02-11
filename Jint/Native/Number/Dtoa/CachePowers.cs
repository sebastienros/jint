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
using System.Runtime.InteropServices;

namespace Jint.Native.Number.Dtoa;

internal static class CachedPowers
{
    private const double Kd1Log210 = 0.30102999566398114; //  1 / lg(10)

    private sealed class CachedPower
    {
        internal readonly ulong Significand;
        internal readonly short BinaryExponent;
        internal readonly short DecimalExponent;

        internal CachedPower(ulong significand, short binaryExponent, short decimalExponent)
        {
            Significand =  significand;
            BinaryExponent = binaryExponent;
            DecimalExponent = decimalExponent;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct GetCachedPowerResult
    {
        public GetCachedPowerResult(short decimalExponent, DiyFp cMk)
        {
            this.decimalExponent = decimalExponent;
            this.cMk = cMk;
        }

        internal readonly short decimalExponent;
        internal readonly DiyFp cMk;
    }

    internal static GetCachedPowerResult GetCachedPowerForBinaryExponentRange(int min_exponent, int max_exponent)
    {
        const int kQ = DiyFp.KSignificandSize;
        double k = System.Math.Ceiling((min_exponent + kQ - 1) * Kd1Log210);
        int foo = kCachedPowersOffset;
        int index =
            (foo + (int) k - 1) / kDecimalExponentDistance + 1;
        Debug.Assert(0 <= index && index < CACHED_POWERS.Length);
        CachedPower cachedPower = CACHED_POWERS[index];
        Debug.Assert(min_exponent <= cachedPower.BinaryExponent);
        Debug.Assert(cachedPower.BinaryExponent <= max_exponent);

        var cMk = new DiyFp(cachedPower.Significand, cachedPower.BinaryExponent);
        return new GetCachedPowerResult(cachedPower.DecimalExponent, cMk);
    }

    // Code below is converted from GRISU_CACHE_NAME(8) in file "powers-ten.h"
    // Regexp to convert this from original C++ source:
    // \{GRISU_UINT64_C\((\w+), (\w+)\), (\-?\d+), (\-?\d+)\}

    private static readonly CachedPower[] CACHED_POWERS =
    [
        new CachedPower(0xFA8FD5A0081C0288, -1220, -348),
        new CachedPower(0xBAAEE17FA23EBF76, -1193, -340),
        new CachedPower(0x8B16FB203055AC76, -1166, -332),
        new CachedPower(0xCF42894A5DCE35EA, -1140, -324),
        new CachedPower(0x9A6BB0AA55653B2D, -1113, -316),
        new CachedPower(0xE61ACF033D1A45DF, -1087, -308),
        new CachedPower(0xAB70FE17C79AC6CA, -1060, -300),
        new CachedPower(0xFF77B1FCBEBCDC4F, -1034, -292),
        new CachedPower(0xBE5691EF416BD60C, -1007, -284),
        new CachedPower(0x8DD01FAD907FFC3C, -980, -276),
        new CachedPower(0xD3515C2831559A83, -954, -268),
        new CachedPower(0x9D71AC8FADA6C9B5, -927, -260),
        new CachedPower(0xEA9C227723EE8BCB, -901, -252),
        new CachedPower(0xAECC49914078536D, -874, -244),
        new CachedPower(0x823C12795DB6CE57, -847, -236),
        new CachedPower(0xC21094364DFB5637, -821, -228),
        new CachedPower(0x9096EA6F3848984F, -794, -220),
        new CachedPower(0xD77485CB25823AC7, -768, -212),
        new CachedPower(0xA086CFCD97BF97F4, -741, -204),
        new CachedPower(0xEF340A98172AACE5, -715, -196),
        new CachedPower(0xB23867FB2A35B28E, -688, -188),
        new CachedPower(0x84C8D4DFD2C63F3B, -661, -180),
        new CachedPower(0xC5DD44271AD3CDBA, -635, -172),
        new CachedPower(0x936B9FCEBB25C996, -608, -164),
        new CachedPower(0xDBAC6C247D62A584, -582, -156),
        new CachedPower(0xA3AB66580D5FDAF6, -555, -148),
        new CachedPower(0xF3E2F893DEC3F126, -529, -140),
        new CachedPower(0xB5B5ADA8AAFF80B8, -502, -132),
        new CachedPower(0x87625F056C7C4A8B, -475, -124),
        new CachedPower(0xC9BCFF6034C13053, -449, -116),
        new CachedPower(0x964E858C91BA2655, -422, -108),
        new CachedPower(0xDFF9772470297EBD, -396, -100),
        new CachedPower(0xA6DFBD9FB8E5B88F, -369, -92),
        new CachedPower(0xF8A95FCF88747D94, -343, -84),
        new CachedPower(0xB94470938FA89BCF, -316, -76),
        new CachedPower(0x8A08F0F8BF0F156B, -289, -68),
        new CachedPower(0xCDB02555653131B6, -263, -60),
        new CachedPower(0x993FE2C6D07B7FAC, -236, -52),
        new CachedPower(0xE45C10C42A2B3B06, -210, -44),
        new CachedPower(0xAA242499697392D3, -183, -36),
        new CachedPower(0xFD87B5F28300CA0E, -157, -28),
        new CachedPower(0xBCE5086492111AEB, -130, -20),
        new CachedPower(0x8CBCCC096F5088CC, -103, -12),
        new CachedPower(0xD1B71758E219652C, -77, -4),
        new CachedPower(0x9C40000000000000, -50, 4),
        new CachedPower(0xE8D4A51000000000, -24, 12),
        new CachedPower(0xAD78EBC5AC620000, 3, 20),
        new CachedPower(0x813F3978F8940984, 30, 28),
        new CachedPower(0xC097CE7BC90715B3, 56, 36),
        new CachedPower(0x8F7E32CE7BEA5C70, 83, 44),
        new CachedPower(0xD5D238A4ABE98068, 109, 52),
        new CachedPower(0x9F4F2726179A2245, 136, 60),
        new CachedPower(0xED63A231D4C4FB27, 162, 68),
        new CachedPower(0xB0DE65388CC8ADA8, 189, 76),
        new CachedPower(0x83C7088E1AAB65DB, 216, 84),
        new CachedPower(0xC45D1DF942711D9A, 242, 92),
        new CachedPower(0x924D692CA61BE758, 269, 100),
        new CachedPower(0xDA01EE641A708DEA, 295, 108),
        new CachedPower(0xA26DA3999AEF774A, 322, 116),
        new CachedPower(0xF209787BB47D6B85, 348, 124),
        new CachedPower(0xB454E4A179DD1877, 375, 132),
        new CachedPower(0x865B86925B9BC5C2, 402, 140),
        new CachedPower(0xC83553C5C8965D3D, 428, 148),
        new CachedPower(0x952AB45CFA97A0B3, 455, 156),
        new CachedPower(0xDE469FBD99A05FE3, 481, 164),
        new CachedPower(0xA59BC234DB398C25, 508, 172),
        new CachedPower(0xF6C69A72A3989F5C, 534, 180),
        new CachedPower(0xB7DCBF5354E9BECE, 561, 188),
        new CachedPower(0x88FCF317F22241E2, 588, 196),
        new CachedPower(0xCC20CE9BD35C78A5, 614, 204),
        new CachedPower(0x98165AF37B2153DF, 641, 212),
        new CachedPower(0xE2A0B5DC971F303A, 667, 220),
        new CachedPower(0xA8D9D1535CE3B396, 694, 228),
        new CachedPower(0xFB9B7CD9A4A7443C, 720, 236),
        new CachedPower(0xBB764C4CA7A44410, 747, 244),
        new CachedPower(0x8BAB8EEFB6409C1A, 774, 252),
        new CachedPower(0xD01FEF10A657842C, 800, 260),
        new CachedPower(0x9B10A4E5E9913129, 827, 268),
        new CachedPower(0xE7109BFBA19C0C9D, 853, 276),
        new CachedPower(0xAC2820D9623BF429, 880, 284),
        new CachedPower(0x80444B5E7AA7CF85, 907, 292),
        new CachedPower(0xBF21E44003ACDD2D, 933, 300),
        new CachedPower(0x8E679C2F5E44FF8F, 960, 308),
        new CachedPower(0xD433179D9C8CB841, 986, 316),
        new CachedPower(0x9E19DB92B4E31BA9, 1013, 324),
        new CachedPower(0xEB96BF6EBADF77D9, 1039, 332),
        new CachedPower(0xAF87023B9BF0EE6B, 1066, 340)
    ];

    const int kCachedPowersOffset = 348;  // -1 * the first decimal_exponent.
    const int kDecimalExponentDistance = 8;
    const int kMinDecimalExponent = -348;
    const int kMaxDecimalExponent = 340;
}
