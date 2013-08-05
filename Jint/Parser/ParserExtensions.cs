using System.Collections.Generic;

namespace Jint.Parser
{
    public static class ParserExtensions
    {
        public static string Slice(this string source, int start, int end)
        {
            return source.Substring(start, end - start);
        }

        public static char CharCodeAt(this string source, int index)
        {
            if (index < 0 || index > source.Length - 1)
            {
                // char.MinValue is used as the null value
                return char.MinValue;
            }

            return source[index];
        }
    }
}