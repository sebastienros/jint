#nullable disable
#pragma warning disable CA1510

namespace Jint.Native.Date;

// This file contains code extracted from excellent MimeKit library
// https://github.com/jstedfast/MimeKit , see above copyright which applies to all code
// Jint version has adjusted namespaces and made members visible / removed unused ones

// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2022 .NET Foundation and Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[Flags]
internal enum DateTokenFlags : byte
{
    None = 0,
    NonNumeric = (1 << 0),
    NonWeekday = (1 << 1),
    NonMonth = (1 << 2),
    NonTime = (1 << 3),
    NonAlphaZone = (1 << 4),
    NonNumericZone = (1 << 5),
    HasColon = (1 << 6),
    HasSign = (1 << 7),
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DateToken
{
    public DateToken(DateTokenFlags flags, int start, int length)
    {
        Flags = flags;
        Start = start;
        Length = length;
    }

    public DateTokenFlags Flags { get; }

    public int Start { get; }

    public int Length { get; }

    public bool IsNumeric
    {
        get { return (Flags & DateTokenFlags.NonNumeric) == DateTokenFlags.None; }
    }

    public bool IsWeekday
    {
        get { return (Flags & DateTokenFlags.NonWeekday) == DateTokenFlags.None; }
    }

    public bool IsMonth
    {
        get { return (Flags & DateTokenFlags.NonMonth) == DateTokenFlags.None; }
    }

    public bool IsTimeOfDay
    {
        get { return (Flags & DateTokenFlags.NonTime) == DateTokenFlags.None && (Flags & DateTokenFlags.HasColon) != DateTokenFlags.None; }
    }

    public bool IsNumericZone
    {
        get { return (Flags & DateTokenFlags.NonNumericZone) == DateTokenFlags.None && (Flags & DateTokenFlags.HasSign) != DateTokenFlags.None; }
    }

    public bool IsAlphaZone
    {
        get { return (Flags & DateTokenFlags.NonAlphaZone) == DateTokenFlags.None; }
    }

    public bool IsTimeZone
    {
        get { return IsNumericZone || IsAlphaZone; }
    }
}

/// <summary>
/// Utility methods to parse and format rfc822 date strings.
/// </summary>
/// <remarks>
/// Utility methods to parse and format rfc822 date strings.
/// </remarks>
internal static class DateUtils
{
    private const string MonthCharacters = "JanuaryFebruaryMarchAprilMayJuneJulyAugustSeptemberOctoberNovemberDecember";
    private const string WeekdayCharacters = "SundayMondayTuesdayWednesdayThursdayFridaySaturday";
    private const string AlphaZoneCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string NumericZoneCharacters = "+-0123456789";
    private const string NumericCharacters = "0123456789";
    private const string TimeCharacters = "0123456789:";

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly string[] WeekDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private static readonly Dictionary<string, int> timezones;
    private static readonly DateTokenFlags[] datetok;

    static DateUtils()
    {
        timezones = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "UT", 0 },
            { "UTC", 0 },
            { "GMT", 0 },
            { "EDT", -400 },
            { "EST", -500 },
            { "CDT", -500 },
            { "CST", -600 },
            { "MDT", -600 },
            { "MST", -700 },
            { "PDT", -700 },
            { "PST", -800 },
            // Note: rfc822 got the signs backwards for the military
            // timezones so some sending clients may mistakenly use the
            // wrong values.
            { "A", 100 },
            { "B", 200 },
            { "C", 300 },
            { "D", 400 },
            { "E", 500 },
            { "F", 600 },
            { "G", 700 },
            { "H", 800 },
            { "I", 900 },
            { "K", 1000 },
            { "L", 1100 },
            { "M", 1200 },
            { "N", -100 },
            { "O", -200 },
            { "P", -300 },
            { "Q", -400 },
            { "R", -500 },
            { "S", -600 },
            { "T", -700 },
            { "U", -800 },
            { "V", -900 },
            { "W", -1000 },
            { "X", -1100 },
            { "Y", -1200 },
            { "Z", 0 },
        };

        datetok = new DateTokenFlags[256];
        var any = new char[2];

        for (int c = 0; c < 256; c++)
        {
            if (c >= 0x41 && c <= 0x5a)
            {
                any[1] = (char) (c + 0x20);
                any[0] = (char) c;
            }
            else if (c >= 0x61 && c <= 0x7a)
            {
                any[0] = (char) (c - 0x20);
                any[1] = (char) c;
            }

#pragma warning disable CA2249
            if (NumericZoneCharacters.IndexOf((char) c) == -1)
                datetok[c] |= DateTokenFlags.NonNumericZone;
            if (AlphaZoneCharacters.IndexOf((char) c) == -1)
                datetok[c] |= DateTokenFlags.NonAlphaZone;
            if (WeekdayCharacters.IndexOfAny(any) == -1)
                datetok[c] |= DateTokenFlags.NonWeekday;
            if (NumericCharacters.IndexOf((char) c) == -1)
                datetok[c] |= DateTokenFlags.NonNumeric;
            if (MonthCharacters.IndexOfAny(any) == -1)
                datetok[c] |= DateTokenFlags.NonMonth;
            if (TimeCharacters.IndexOf((char) c) == -1)
                datetok[c] |= DateTokenFlags.NonTime;
#pragma warning restore CA2249
        }

        datetok[':'] |= DateTokenFlags.HasColon;
        datetok['+'] |= DateTokenFlags.HasSign;
        datetok['-'] |= DateTokenFlags.HasSign;
    }

    private static bool TryGetWeekday(in DateToken token, byte[] text, out DayOfWeek weekday)
    {
        weekday = DayOfWeek.Sunday;

        if (!token.IsWeekday || token.Length < 3)
            return false;

        var name = Encoding.ASCII.GetString(text, token.Start, 3);

        for (int day = 0; day < WeekDays.Length; day++)
        {
            if (WeekDays[day].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                weekday = (DayOfWeek) day;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDayOfMonth(in DateToken token, byte[] text, out int day)
    {
        int endIndex = token.Start + token.Length;
        int index = token.Start;

        day = 0;

        if (!token.IsNumeric)
            return false;

        if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out day))
            return false;

        if (day <= 0 || day > 31)
            return false;

        return true;
    }

    private static bool TryGetMonth(in DateToken token, byte[] text, out int month)
    {
        month = 0;

        if (!token.IsMonth || token.Length < 3)
            return false;

        var name = Encoding.ASCII.GetString(text, token.Start, 3);

        for (int i = 0; i < Months.Length; i++)
        {
            if (Months[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                month = i + 1;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetYear(in DateToken token, byte[] text, out int year)
    {
        int endIndex = token.Start + token.Length;
        int index = token.Start;

        year = 0;

        if (!token.IsNumeric)
            return false;

        if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out year))
            return false;

        if (year < 100)
            year += (year < 70) ? 2000 : 1900;

        return year >= 1969;
    }

    private static bool TryGetTimeOfDay(in DateToken token, byte[] text, out int hour, out int minute, out int second)
    {
        int endIndex = token.Start + token.Length;
        int index = token.Start;

        hour = minute = second = 0;

        if (!token.IsTimeOfDay)
            return false;

        if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out hour) || hour > 23)
            return false;

        if (index >= endIndex || text[index++] != (byte) ':')
            return false;

        if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out minute) || minute > 59)
            return false;

        // Allow just hh:mm (i.e. w/o the :ss?)
        if (index >= endIndex || text[index++] != (byte) ':')
            return true;

        if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out second) || second > 59)
            return false;

        return index == endIndex;
    }

    private static bool TryGetTimeZone(in DateToken token, byte[] text, out int tzone)
    {
        tzone = 0;

        if (token.IsNumericZone)
        {
            int endIndex = token.Start + token.Length;
            int index = token.Start;
            int sign;

            if (text[index] == (byte) '-')
                sign = -1;
            else if (text[index] == (byte) '+')
                sign = 1;
            else
                return false;

            index++;

            if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out tzone) || index != endIndex)
                return false;

            tzone *= sign;
        }
        else if (token.IsAlphaZone)
        {
            if (token.Length > 3)
                return false;

            var name = Encoding.ASCII.GetString(text, token.Start, token.Length);

            if (!timezones.TryGetValue(name, out tzone))
                return false;
        }
        else if (token.IsNumeric)
        {
            int endIndex = token.Start + token.Length;
            int index = token.Start;

            if (!ParseUtils.TryParseInt32(text, ref index, endIndex, out tzone) || index != endIndex)
                return false;
        }

        if (tzone < -1200 || tzone > 1400)
            return false;

        return true;
    }

    private static bool IsTokenDelimeter(byte c)
    {
        return c == (byte) '-' || c == (byte) '/' || c == (byte) ',' || c.IsWhitespace();
    }

    private static IEnumerable<DateToken> TokenizeDate(byte[] text, int startIndex, int length)
    {
        int endIndex = startIndex + length;
        int index = startIndex;
        DateTokenFlags mask;
        int start;

        while (index < endIndex)
        {
            if (!ParseUtils.SkipCommentsAndWhiteSpace(text, ref index, endIndex, false))
                break;

            if (index >= endIndex)
                break;

            // get the initial mask for this token
            if ((mask = datetok[text[index]]) != DateTokenFlags.None)
            {
                start = index++;

                // find the end of this token
                while (index < endIndex && !IsTokenDelimeter(text[index]))
                    mask |= datetok[text[index++]];

                yield return new DateToken(mask, start, index - start);
            }

            // skip over the token delimeter
            index++;
        }

        yield break;
    }

    private static bool TryParseStandardDateFormat(List<DateToken> tokens, byte[] text, out DateTimeOffset date)
    {
        //bool haveWeekday;
        int n = 0;

        date = new DateTimeOffset();

        // we need at least 5 tokens, 6 if we have a weekday
        if (tokens.Count < 5)
            return false;

        // Note: the weekday is not required
        if (TryGetWeekday(tokens[n], text, out _))
        {
            if (tokens.Count < 6)
                return false;

            //haveWeekday = true;
            n++;
        }

        if (!TryGetDayOfMonth(tokens[n++], text, out int day))
            return false;

        if (!TryGetMonth(tokens[n++], text, out int month))
            return false;

        if (!TryGetYear(tokens[n++], text, out int year))
            return false;

        if (!TryGetTimeOfDay(tokens[n++], text, out int hour, out int minute, out int second))
            return false;

        if (!TryGetTimeZone(tokens[n], text, out int tzone))
            tzone = 0;

        int minutes = tzone % 100;
        int hours = tzone / 100;

        var offset = new TimeSpan(hours, minutes, 0);

        try
        {
            date = new DateTimeOffset(year, month, day, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return true;
    }

#pragma warning disable CA1859
    private static bool TryParseUnknownDateFormat(IList<DateToken> tokens, byte[] text, out DateTimeOffset date)
#pragma warning restore CA1859
    {
        int? day = null, month = null, year = null, tzone = null;
        int hour = 0, minute = 0, second = 0;
        bool numericMonth = false;
        bool haveWeekday = false;
        bool haveTime = false;
        TimeSpan offset;

        for (int i = 0; i < tokens.Count; i++)
        {
            int value;

            if (!haveWeekday && TryGetWeekday(tokens[i], text, out _))
            {
                haveWeekday = true;
                continue;
            }

            if ((month == null || numericMonth) && TryGetMonth(tokens[i], text, out value))
            {
                if (numericMonth)
                {
                    numericMonth = false;
                    day = month;
                }

                month = value;
                continue;
            }

            if (!haveTime && TryGetTimeOfDay(tokens[i], text, out hour, out minute, out second))
            {
                haveTime = true;
                continue;
            }

            // Limit TryGetTimeZone to alpha and numeric timezone tokens (do not allow numeric tokens as they are handled below).
            if (tzone == null && tokens[i].IsTimeZone && TryGetTimeZone(tokens[i], text, out value))
            {
                tzone = value;
                continue;
            }

            if (tokens[i].IsNumeric)
            {
                if (tokens[i].Length == 4)
                {
                    if (year == null)
                    {
                        if (TryGetYear(tokens[i], text, out value))
                            year = value;
                    }
                    else if (tzone == null)
                    {
                        if (TryGetTimeZone(tokens[i], text, out value))
                            tzone = value;
                    }

                    continue;
                }

                if (tokens[i].Length > 2)
                    continue;

                // Note: we likely have either YYYY[-/]MM[-/]DD or MM[-/]DD[-/]YY
                int endIndex = tokens[i].Start + tokens[i].Length;
                int index = tokens[i].Start;

#pragma warning disable CA1806
                ParseUtils.TryParseInt32(text, ref index, endIndex, out value);
#pragma warning restore CA1806

                if (month == null && value > 0 && value <= 12)
                {
                    numericMonth = true;
                    month = value;
                    continue;
                }

                if (day == null && value > 0 && value <= 31)
                {
                    day = value;
                    continue;
                }

                if (year == null && value >= 69)
                {
                    year = 1900 + value;
                    continue;
                }
            }

            // WTF is this??
        }

        if (year == null || month == null || day == null)
        {
            date = new DateTimeOffset();
            return false;
        }

        if (!haveTime)
            hour = minute = second = 0;

        if (tzone != null)
        {
            int minutes = tzone.Value % 100;
            int hours = tzone.Value / 100;

            offset = new TimeSpan(hours, minutes, 0);
        }
        else
        {
            offset = new TimeSpan(0);
        }

        try
        {
            date = new DateTimeOffset(year.Value, month.Value, day.Value, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            date = new DateTimeOffset();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Try to parse the given input buffer into a new <see cref="System.DateTimeOffset"/> instance.
    /// </summary>
    /// <remarks>
    /// Parses an rfc822 date and time from the supplied buffer starting at the given index
    /// and spanning across the specified number of bytes.
    /// </remarks>
    /// <returns><c>true</c>, if the date was successfully parsed, <c>false</c> otherwise.</returns>
    /// <param name="buffer">The input buffer.</param>
    /// <param name="startIndex">The starting index of the input buffer.</param>
    /// <param name="length">The number of bytes in the input buffer to parse.</param>
    /// <param name="date">The parsed date.</param>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="buffer"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> and <paramref name="length"/> do not specify
    /// a valid range in the byte array.
    /// </exception>
    public static bool TryParse(byte[] buffer, int startIndex, int length, out DateTimeOffset date)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        if (startIndex < 0 || startIndex > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        if (length < 0 || length > (buffer.Length - startIndex))
            throw new ArgumentOutOfRangeException(nameof(length));

        var tokens = new List<DateToken>(TokenizeDate(buffer, startIndex, length));

        if (TryParseStandardDateFormat(tokens, buffer, out date))
            return true;

        if (TryParseUnknownDateFormat(tokens, buffer, out date))
            return true;

        date = new DateTimeOffset();

        return false;
    }

    /// <summary>
    /// Try to parse the given input buffer into a new <see cref="System.DateTimeOffset"/> instance.
    /// </summary>
    /// <remarks>
    /// Parses an rfc822 date and time from the specified text.
    /// </remarks>
    /// <returns><c>true</c>, if the date was successfully parsed, <c>false</c> otherwise.</returns>
    /// <param name="text">The input text.</param>
    /// <param name="date">The parsed date.</param>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="text"/> is <c>null</c>.
    /// </exception>
    public static bool TryParse(string text, out DateTimeOffset date)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        var buffer = Encoding.UTF8.GetBytes(text);

        return TryParse(buffer, 0, buffer.Length, out date);
    }
}

internal static class ParseUtils
{
    public static bool TryParseInt32(byte[] text, ref int index, int endIndex, out int value)
    {
        int startIndex = index;

        value = 0;

        while (index < endIndex && text[index] >= (byte) '0' && text[index] <= (byte) '9')
        {
            int digit = text[index] - (byte) '0';

            if (value > int.MaxValue / 10)
            {
                // integer overflow
                return false;
            }

            if (value == int.MaxValue / 10 && digit > int.MaxValue % 10)
            {
                // integer overflow
                return false;
            }

            value = (value * 10) + digit;
            index++;
        }

        return index > startIndex;
    }

    public static bool SkipWhiteSpace(byte[] text, ref int index, int endIndex)
    {
        int startIndex = index;

        while (index < endIndex && text[index].IsWhitespace())
            index++;

        return index > startIndex;
    }

    public static bool SkipComment(byte[] text, ref int index, int endIndex)
    {
        bool escaped = false;
        int depth = 1;

        index++;

        while (index < endIndex && depth > 0)
        {
            if (text[index] == (byte) '\\')
            {
                escaped = !escaped;
            }
            else if (!escaped)
            {
                if (text[index] == (byte) '(')
                    depth++;
                else if (text[index] == (byte) ')')
                    depth--;
                escaped = false;
            }
            else
            {
                escaped = false;
            }

            index++;
        }

        return depth == 0;
    }

    public static bool SkipCommentsAndWhiteSpace(byte[] text, ref int index, int endIndex, bool throwOnError)
    {
        SkipWhiteSpace(text, ref index, endIndex);

        while (index < endIndex && text[index] == (byte) '(')
        {
            int startIndex = index;

            if (!SkipComment(text, ref index, endIndex))
            {
                if (throwOnError)
                {
#pragma warning disable MA0015
                    throw new ArgumentException($"Incomplete comment token at offset {startIndex}");
#pragma warning restore MA0015
                }

                return false;
            }

            SkipWhiteSpace(text, ref index, endIndex);
        }

        return true;
    }
}

[Flags]
internal enum CharType : ushort
{
    None = 0,
    IsAscii = (1 << 0),
    IsAtom = (1 << 1),
    IsAttrChar = (1 << 2),
    IsBlank = (1 << 3),
    IsControl = (1 << 4),
    IsDomainSafe = (1 << 5),
    IsEncodedPhraseSafe = (1 << 6),
    IsEncodedWordSafe = (1 << 7),
    IsQuotedPrintableSafe = (1 << 8),
    IsSpace = (1 << 9),
    IsSpecial = (1 << 10),
    IsTokenSpecial = (1 << 11),
    IsWhitespace = (1 << 12),
    IsXDigit = (1 << 13),
    IsPhraseAtom = (1 << 14)
}

internal static class ByteExtensions
{
    private const string AtomSafeCharacters = "!#$%&'*+-/=?^_`{|}~";
    private const string AttributeSpecials = "*'%"; // attribute specials from rfc2184/rfc2231
    private const string DomainSpecials = "[]\\\r \t"; // not allowed in domains
    private const string EncodedWordSpecials = "()<>@,;:\"/[]?.=_"; // rfc2047 5.1
    private const string EncodedPhraseSpecials = "!*+-/=_"; // rfc2047 5.3
    private const string Specials = "()<>[]:;@\\,.\""; // rfc5322 3.2.3
    private const string TokenSpecials = "()<>@,;:\\\"/[]?="; // rfc2045 5.1
    private const string Whitespace = " \t\r\n";

    private static readonly CharType[] table = new CharType[256];

    private static void RemoveFlags(string values, CharType bit)
    {
        for (int i = 0; i < values.Length; i++)
            table[(byte) values[i]] &= ~bit;
    }

    private static void SetFlags(string values, CharType bit, CharType bitcopy, bool remove)
    {
        int i;

        if (remove)
        {
            for (i = 0; i < 128; i++)
                table[i] |= bit;

            for (i = 0; i < values.Length; i++)
                table[values[i]] &= ~bit;

            // Note: not actually used...
            //if (bitcopy != CharType.None) {
            //	for (i = 0; i < 256; i++) {
            //		if ((table[i] & bitcopy) != 0)
            //			table[i] &= ~bit;
            //	}
            //}
        }
        else
        {
            for (i = 0; i < values.Length; i++)
                table[values[i]] |= bit;

            if (bitcopy != CharType.None)
            {
                for (i = 0; i < 256; i++)
                {
                    if ((table[i] & bitcopy) != CharType.None)
                        table[i] |= bit;
                }
            }
        }
    }

    static ByteExtensions()
    {
        for (int i = 0; i < 256; i++)
        {
            if (i < 127)
            {
                if (i < 32)
                    table[i] |= CharType.IsControl;
                if (i > 32)
                    table[i] |= CharType.IsAttrChar;
                if ((i >= 33 && i <= 60) || (i >= 62 && i <= 126) || i == 32)
                    table[i] |= (CharType.IsQuotedPrintableSafe | CharType.IsEncodedWordSafe);
                if ((i >= '0' && i <= '9') || (i >= 'a' && i <= 'z') || (i >= 'A' && i <= 'Z'))
                    table[i] |= CharType.IsEncodedPhraseSafe | CharType.IsAtom | CharType.IsPhraseAtom;
                if ((i >= '0' && i <= '9') || (i >= 'a' && i <= 'f') || (i >= 'A' && i <= 'F'))
                    table[i] |= CharType.IsXDigit;

                table[i] |= CharType.IsAscii;
            }
            else
            {
                if (i == 127)
                    table[i] |= CharType.IsAscii;
                else
                    table[i] |= CharType.IsAtom | CharType.IsPhraseAtom;

                table[i] |= CharType.IsControl;
            }
        }

        table['\t'] |= CharType.IsQuotedPrintableSafe | CharType.IsBlank;
        table[' '] |= CharType.IsSpace | CharType.IsBlank;

        SetFlags(Whitespace, CharType.IsWhitespace, CharType.None, false);
        SetFlags(AtomSafeCharacters, CharType.IsAtom | CharType.IsPhraseAtom, CharType.None, false);
        SetFlags(TokenSpecials, CharType.IsTokenSpecial, CharType.IsControl, false);
        SetFlags(Specials, CharType.IsSpecial, CharType.None, false);
        SetFlags(DomainSpecials, CharType.IsDomainSafe, CharType.None, true);
        RemoveFlags(Specials, CharType.IsAtom | CharType.IsPhraseAtom);
        RemoveFlags(EncodedWordSpecials, CharType.IsEncodedWordSafe);
        RemoveFlags(AttributeSpecials + TokenSpecials, CharType.IsAttrChar);
        SetFlags(EncodedPhraseSpecials, CharType.IsEncodedPhraseSafe, CharType.None, false);

        // Note: Allow '[' and ']' in the display-name of a mailbox address
        table['['] |= CharType.IsPhraseAtom;
        table[']'] |= CharType.IsPhraseAtom;
    }

    public static bool IsWhitespace(this byte c)
    {
        return (table[c] & CharType.IsWhitespace) != CharType.None;
    }
}
