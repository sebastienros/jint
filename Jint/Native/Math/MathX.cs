// based on https://raw.githubusercontent.com/AnthonyLloyd/CsCheck/master/Tests/MathX.cs

namespace Jint.Native.Math;

internal static class MathX
{
    private static double TwoSum(double a, double b, out double lo)
    {
        var hi = a + b;
        lo = hi - b;
        lo = lo - hi + b + (a - lo);
        return hi;
    }

    /// <summary>Shewchuk summation</summary>
    internal static double FSum(this List<double> values)
    {
        if (values.Count < 3) return values.Count == 2 ? values[0] + values[1] : values.Count == 1 ? values[0] : 0.0;
        Span<double> partials = stackalloc double[16];
        var hi = TwoSum(values[0], values[1], out var lo);
        int count = 0;
        for (int i = 2; i < values.Count; i++)
        {
            var v = TwoSum(values[i], lo, out lo);
            int c = 0;
            for (int j = 0; j < count; j++)
            {
                v = TwoSum(v, partials[j], out var p);
                if (p != 0.0)
                    partials[c++] = p;
            }

            hi = TwoSum(hi, v, out v);
            if (v != 0.0)
            {
                if (c == partials.Length)
                {
                    var newPartials = new double[partials.Length * 2];
                    partials.CopyTo(newPartials);
                    partials = newPartials;
                }

                partials[c++] = v;
            }

            count = c;
        }

        if (count != 0)
        {
            if (lo == 0) // lo has a good chance of being zero
            {
                lo = partials[0];
                if (count == 1) return lo + hi;
                partials = partials.Slice(1, count - 1);
            }
            else
                partials = partials.Slice(0, count);

            foreach (var p in partials)
                lo += p;
        }

        return lo + hi;
    }
}
