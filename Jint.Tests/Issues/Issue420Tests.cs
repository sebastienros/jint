using System;
using System.Collections.Generic;
using Xunit;

namespace Jint.Tests.Issues
{
    public class Issue420Tests
    {
        [Theory]
        [MemberData(nameof(Issue420TestCases.TestItems), MemberType = typeof(Issue420TestCases))]
        public void Issue420(DateTime datetime, int expectedHour)
        {
            // Arrange
            var engine = new Engine();

            // Act
            var result = engine.Execute($"new Date({datetime.FormatForJavaScript()}).getHours()").GetCompletionValue();

            // Assert
            Assert.Equal(expectedHour, result.AsNumber());
        }
    }

    public static class Issue420TestCases
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
