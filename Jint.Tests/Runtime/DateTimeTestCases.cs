using System;
using System.Collections.Generic;

namespace Jint.Tests.Runtime
{
    public static class DateTimeTestCases
    {
        public static readonly List<object[]> Items = new List<object[]>
        {
            new object[] {new DateTime(1969, 1, 1, 19, 45, 0), 19},
            new object[] {new DateTime(1970, 1, 1, 19, 45, 0), 19},
            new object[] {new DateTime(1971, 1, 1, 19, 45, 0), 19}
        };

        public static IEnumerable<object[]> TestItems => Items;

        public static string FormatForJavaScript(this DateTime dateTime) => $"{dateTime.Year}, {dateTime.Month - 1}, {dateTime.Day}, {dateTime.Hour}, {dateTime.Minute}";
    }
}