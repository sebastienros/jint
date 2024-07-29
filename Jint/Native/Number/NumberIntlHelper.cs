// Ideally, internacionalization formats implemented through the ECMAScript standards would follow this:
// https://tc39.es/ecma402/#sec-initializedatetimeformat
// https://tc39.es/ecma402/#sec-canonicalizelocalelist
// Along with the implementations of whatever is subsequenlty called.

// As this is not in place (See TODOS in NumberFormatConstructor and DateTimeFormatConstructor) we can arrange
// values that will match the JS behavior using the host logic. This bypasses the ECMAScript standards but can
// do the job for the most common use cases and cultures meanwhile.

namespace Jint.Native.Number;

internal class NumberIntlHelper
{
    // Obtined empirically. For all cultures tested, we get a maximum of 3 decimal digits.
    private const int JS_MAX_DECIMAL_DIGIT_COUNT = 3;

    /// <summary>
    /// Checks the powers of 10 of number to count the number of decimal digits.
    /// Returns a clamped JS_MAX_DECIMAL_DIGIT_COUNT count.
    /// JavaScript will use the shortest representation that accurately represents the value
    /// and clamp the decimal digits to JS_MAX_DECIMAL_DIGIT_COUNT.
    /// C# fills the digits with zeros up to the culture's numberFormat.NumberDecimalDigits
    /// and does not provide the same max (numberFormat.NumberDecimalDigits != JS_MAX_DECIMAL_DIGIT_COUNT).
    /// This function matches the JS behaviour for the decimal digits returned, this is the actual decimal
    /// digits for a number (with no zeros fill) clamped to JS_MAX_DECIMAL_DIGIT_COUNT.
    /// </summary>
    public static int GetDecimalDigitCount(double number)
    {
        for (int i = 0; i < JS_MAX_DECIMAL_DIGIT_COUNT; i++)
        {
            var powOf10 = number * System.Math.Pow(10, i);
            bool isInteger = powOf10 == ((int) powOf10);
            if (isInteger)
            {
                return i;
            }
        }

        return JS_MAX_DECIMAL_DIGIT_COUNT;
    }
}
