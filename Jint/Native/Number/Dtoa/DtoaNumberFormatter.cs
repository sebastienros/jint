#nullable disable

using System.Diagnostics;
using Jint.Runtime;

namespace Jint.Native.Number.Dtoa;

internal static class DtoaNumberFormatter
{
    public static void DoubleToAscii(
        ref  DtoaBuilder buffer,
        double v,
        DtoaMode mode,
        int requested_digits,
        out bool negative,
        out int point)
    {
        Debug.Assert(!double.IsNaN(v));
        Debug.Assert(!double.IsInfinity(v));
        Debug.Assert(mode == DtoaMode.Shortest || requested_digits >= 0);

        point = 0;
        negative = false;
        buffer.Reset();

        if (v < 0)
        {
            negative = true;
            v = -v;
        }

        if (v == 0)
        {
            buffer[0] = '0';
            point = 1;
            return;
        }

        if (mode == DtoaMode.Precision && requested_digits == 0)
        {
            return;
        }

        bool fast_worked = false;
        switch (mode) {
            case DtoaMode.Shortest:
                fast_worked = FastDtoa.NumberToString(v, DtoaMode.Shortest, 0, out point, ref buffer);
                break;
            case DtoaMode.Fixed:
                //fast_worked = FastFixedDtoa(v, requested_digits, buffer, length, point);
                ExceptionHelper.ThrowNotImplementedException();
                break;
            case DtoaMode.Precision:
                fast_worked = FastDtoa.NumberToString(v, DtoaMode.Precision, requested_digits, out point, ref buffer);
                break;
            default:
                ExceptionHelper.ThrowArgumentOutOfRangeException();
                return;
        }

        if (fast_worked)
        {
            return;
        }

        // If the fast dtoa didn't succeed use the slower bignum version.
        buffer.Reset();
        BignumDtoa.NumberToString(v, mode, requested_digits, ref buffer, out point);
    }
}
