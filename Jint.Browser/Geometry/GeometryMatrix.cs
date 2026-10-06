namespace Jint.Browser.Geometry;

/// <summary>
/// The 4×4 matrix arithmetic behind <c>DOMMatrix</c>, over sixteen doubles in the order Geometry names them:
/// <c>m11, m12, m13, m14, m21, …, m44</c>.
/// </summary>
/// <remarks>
/// https://drafts.fxtf.org/geometry/#matrix. <c>m<i>c</i><i>r</i></c> is column <i>c</i>, row <i>r</i>, and
/// points are column vectors, so element <c>[(c - 1) * 4 + (r - 1)]</c> multiplies coordinate <i>c</i> into
/// output row <i>r</i>. "Post-multiply B" is <c>this = this · B</c>.
/// </remarks>
internal static class GeometryMatrix
{
    internal const int M11 = 0, M12 = 1, M13 = 2, M14 = 3;
    internal const int M21 = 4, M22 = 5, M23 = 6, M24 = 7;
    internal const int M31 = 8, M32 = 9, M33 = 10, M34 = 11;
    internal const int M41 = 12, M42 = 13, M43 = 14, M44 = 15;

    internal static double[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    internal static bool IsIdentity(double[] m)
    {
        for (var i = 0; i < 16; i++)
        {
            if (m[i] != (i % 5 == 0 ? 1 : 0))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary><c>a · b</c>, written into <paramref name="a"/>.</summary>
    internal static void PostMultiply(double[] a, double[] b)
    {
        Span<double> result = stackalloc double[16];
        for (var c = 0; c < 4; c++)
        {
            for (var r = 0; r < 4; r++)
            {
                result[c * 4 + r] = a[r] * b[c * 4] + a[4 + r] * b[c * 4 + 1] + a[8 + r] * b[c * 4 + 2] + a[12 + r] * b[c * 4 + 3];
            }
        }

        result.CopyTo(a);
    }

    /// <summary><c>b · a</c>, written into <paramref name="a"/>.</summary>
    internal static void PreMultiply(double[] a, double[] b)
    {
        var copy = (double[]) b.Clone();
        PostMultiply(copy, a);
        Array.Copy(copy, a, 16);
    }

    internal static void Translate(double[] m, double x, double y, double z)
    {
        var t = Identity();
        t[M41] = x;
        t[M42] = y;
        t[M43] = z;
        PostMultiply(m, t);
    }

    internal static void Scale(double[] m, double x, double y, double z)
    {
        var s = Identity();
        s[M11] = x;
        s[M22] = y;
        s[M33] = z;
        PostMultiply(m, s);
    }

    /// <summary>A rotation about one axis (0 = x, 1 = y, 2 = z), which <c>rotate3d</c> reduces to without its rounding.</summary>
    internal static void RotateAboutAxis(double[] m, int axis, double degrees)
    {
        var (sin, cos) = SinCosDegrees(degrees);
        var r = Identity();
        switch (axis)
        {
            case 0:
                r[M22] = cos;
                r[M23] = sin;
                r[M32] = -sin;
                r[M33] = cos;
                break;
            case 1:
                r[M11] = cos;
                r[M13] = -sin;
                r[M31] = sin;
                r[M33] = cos;
                break;
            default:
                r[M11] = cos;
                r[M12] = sin;
                r[M21] = -sin;
                r[M22] = cos;
                break;
        }

        PostMultiply(m, r);
    }

    /// <summary>https://drafts.csswg.org/css-transforms-2/#Rotate3dDefined.</summary>
    internal static void Rotate3d(double[] m, double x, double y, double z, double degrees)
    {
        if (y == 0 && z == 0 && x != 0 && !double.IsNaN(x))
        {
            RotateAboutAxis(m, 0, x > 0 ? degrees : -degrees);
            return;
        }

        if (x == 0 && z == 0 && y != 0 && !double.IsNaN(y))
        {
            RotateAboutAxis(m, 1, y > 0 ? degrees : -degrees);
            return;
        }

        if (x == 0 && y == 0 && z != 0 && !double.IsNaN(z))
        {
            RotateAboutAxis(m, 2, z > 0 ? degrees : -degrees);
            return;
        }

        var length = Math.Sqrt(x * x + y * y + z * z);
        if (length == 0)
        {
            // A zero vector names no axis; engines leave the matrix as it is.
            return;
        }

        x /= length;
        y /= length;
        z /= length;
        var half = degrees * Math.PI / 360;
        var sc = Math.Sin(half) * Math.Cos(half);
        var sq = Math.Sin(half) * Math.Sin(half);
        var r = Identity();
        r[M11] = 1 - 2 * (y * y + z * z) * sq;
        r[M12] = 2 * (x * y * sq + z * sc);
        r[M13] = 2 * (x * z * sq - y * sc);
        r[M21] = 2 * (x * y * sq - z * sc);
        r[M22] = 1 - 2 * (x * x + z * z) * sq;
        r[M23] = 2 * (y * z * sq + x * sc);
        r[M31] = 2 * (x * z * sq + y * sc);
        r[M32] = 2 * (y * z * sq - x * sc);
        r[M33] = 1 - 2 * (x * x + y * y) * sq;
        PostMultiply(m, r);
    }

    internal static void Skew(double[] m, double xDegrees, double yDegrees)
    {
        var s = Identity();
        s[M21] = Math.Tan(xDegrees * Math.PI / 180);
        s[M12] = Math.Tan(yDegrees * Math.PI / 180);
        PostMultiply(m, s);
    }

    /// <summary>https://drafts.csswg.org/css-transforms-2/#funcdef-perspective; <c>none</c> is positive infinity.</summary>
    internal static void Perspective(double[] m, double length)
    {
        if (double.IsPositiveInfinity(length))
        {
            return;
        }

        var p = Identity();
        p[M34] = -1 / Math.Max(length, 1);
        PostMultiply(m, p);
    }

    /// <summary>Inverts <paramref name="m"/> in place; <see langword="false"/> when it is singular, which leaves it untouched.</summary>
    internal static bool Invert(double[] m, bool is2D)
    {
        if (is2D)
        {
            // The 2D affine inverse keeps exact the zeros the general cofactor expansion would round.
            var det = m[M11] * m[M22] - m[M12] * m[M21];
            if (det == 0 || !double.IsFinite(det))
            {
                return false;
            }

            var a = m[M11];
            var b = m[M12];
            var c = m[M21];
            var d = m[M22];
            var e = m[M41];
            var f = m[M42];
            m[M11] = d / det;
            m[M12] = -b / det;
            m[M21] = -c / det;
            m[M22] = a / det;
            m[M41] = (c * f - d * e) / det;
            m[M42] = (b * e - a * f) / det;
            return true;
        }

        Span<double> inv = stackalloc double[16];
        inv[0] = m[5] * m[10] * m[15] - m[5] * m[11] * m[14] - m[9] * m[6] * m[15] + m[9] * m[7] * m[14] + m[13] * m[6] * m[11] - m[13] * m[7] * m[10];
        inv[4] = -m[4] * m[10] * m[15] + m[4] * m[11] * m[14] + m[8] * m[6] * m[15] - m[8] * m[7] * m[14] - m[12] * m[6] * m[11] + m[12] * m[7] * m[10];
        inv[8] = m[4] * m[9] * m[15] - m[4] * m[11] * m[13] - m[8] * m[5] * m[15] + m[8] * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
        inv[12] = -m[4] * m[9] * m[14] + m[4] * m[10] * m[13] + m[8] * m[5] * m[14] - m[8] * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
        inv[1] = -m[1] * m[10] * m[15] + m[1] * m[11] * m[14] + m[9] * m[2] * m[15] - m[9] * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
        inv[5] = m[0] * m[10] * m[15] - m[0] * m[11] * m[14] - m[8] * m[2] * m[15] + m[8] * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
        inv[9] = -m[0] * m[9] * m[15] + m[0] * m[11] * m[13] + m[8] * m[1] * m[15] - m[8] * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
        inv[13] = m[0] * m[9] * m[14] - m[0] * m[10] * m[13] - m[8] * m[1] * m[14] + m[8] * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
        inv[2] = m[1] * m[6] * m[15] - m[1] * m[7] * m[14] - m[5] * m[2] * m[15] + m[5] * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
        inv[6] = -m[0] * m[6] * m[15] + m[0] * m[7] * m[14] + m[4] * m[2] * m[15] - m[4] * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
        inv[10] = m[0] * m[5] * m[15] - m[0] * m[7] * m[13] - m[4] * m[1] * m[15] + m[4] * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
        inv[14] = -m[0] * m[5] * m[14] + m[0] * m[6] * m[13] + m[4] * m[1] * m[14] - m[4] * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
        inv[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
        inv[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
        inv[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
        inv[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];

        var determinant = m[0] * inv[0] + m[1] * inv[4] + m[2] * inv[8] + m[3] * inv[12];
        if (determinant == 0 || !double.IsFinite(determinant))
        {
            return false;
        }

        for (var i = 0; i < 16; i++)
        {
            m[i] = inv[i] / determinant;
        }

        return true;
    }

    /// <summary>
    /// Sine and cosine of an angle in degrees, exact at multiples of 90° where the radian conversion would
    /// leave a 6e-17 residue in the zero.
    /// </summary>
    internal static (double Sin, double Cos) SinCosDegrees(double degrees)
    {
        if (double.IsFinite(degrees) && Math.IEEERemainder(degrees, 90) == 0)
        {
            var quadrant = (int) (((degrees / 90 % 4) + 4) % 4);
            return quadrant switch
            {
                0 => (0, 1),
                1 => (1, 0),
                2 => (0, -1),
                _ => (-1, 0),
            };
        }

        var radians = degrees * Math.PI / 180;
        return (Math.Sin(radians), Math.Cos(radians));
    }
}
