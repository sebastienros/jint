namespace Jint;

internal static class Shims
{
    public static byte[] BytesFromHexString(this ReadOnlySpan<char> value)
    {
#if NET6_0_OR_GREATER
        return Convert.FromHexString(value);
#else
        if ((value.Length & 1) != 0)
        {
            throw new FormatException();
        }

        var byteCount = value.Length >> 1;
        var result = new byte[byteCount];
        var index = 0;
        for (var i = 0; i < byteCount; i++)
        {
            int hi, lo;
            if ((hi = GetDigitValue(value[index++])) < 0
                || (lo = GetDigitValue(value[index++])) < 0)
            {
                throw new FormatException();
            }

            result[i] = (byte) (hi << 4 | lo);
        }

        return result;

        static int GetDigitValue(char ch) => ch switch
        {
            >= '0' and <= '9' => ch - 0x30,
            >= 'a' and <= 'f' => ch - 0x57,
            >= 'A' and <= 'F' => ch - 0x37,
            _ => -1
        };
#endif
    }
}
